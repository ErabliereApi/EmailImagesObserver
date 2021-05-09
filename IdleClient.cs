using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using System.Linq;
using MimeKit;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;

namespace AzureComputerVision 
{
    public class IdleClient : IDisposable
    {
        readonly string host, username, password;
        readonly SecureSocketOptions sslOptions;
        readonly int port;
        List<IMessageSummary> messages;
        CancellationTokenSource cancel;
        CancellationTokenSource? done;
        bool messagesArrived;
        ImapClient client;

        string baseDirectory;
        private readonly LoginInfo config;

        public IdleClient(LoginInfo config, string baseDirectory)
        {
            this.client = new ImapClient(new ProtocolLogger(Console.OpenStandardError()));
            this.messages = new List<IMessageSummary>();
            this.cancel = new CancellationTokenSource();
            this.sslOptions = SecureSocketOptions.SslOnConnect;
            this.username = config.EmailLogin;
            this.password = config.EmailPassword;
            this.host = config.ImapServer;
            this.port = config.ImapPort;
            this.baseDirectory = baseDirectory;
            this.config = config;
        }

        private IMailFolder? _sentFolder;
        public IMailFolder SentFolder
        {
            get {
                if (_sentFolder is not null) return _sentFolder;

                var personal = client.GetFolder(client.PersonalNamespaces[0]);

                _sentFolder = personal.GetSubfolder("Sent Items", cancel.Token);

                if (_sentFolder is null) 
                    throw new InvalidOperationException($"Sent folder cannot be found. The program tries those values: 'Sent Items'");

                return _sentFolder;
            }
        }

        async Task ReconnectAsync()
        {
            if (!client.IsConnected)
                await client.ConnectAsync(host, port, sslOptions, cancel.Token);

            if (!client.IsAuthenticated) 
            {
                await client.AuthenticateAsync(username, password, cancel.Token);

                await SentFolder.OpenAsync(FolderAccess.ReadOnly, cancel.Token);
            }
        }

        async Task FetchMessageSummariesAsync(bool print, bool analyseImage)
        {
            IList<IMessageSummary>? fetched = null;

            do 
            {
                try 
                {
                    // fetch summary information for messages that we don't already have
                    int startIndex = messages.Count;

                    fetched = SentFolder.Fetch(startIndex, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure, cancel.Token);
                    break;
                } 
                catch (ImapProtocolException) 
                {
                    // protocol exceptions often result in the client getting disconnected
                    await ReconnectAsync();
                } 
                catch (IOException) 
                {
                    // I/O exceptions always result in the client getting disconnected
                    await ReconnectAsync();
                }
            } while (true);

            foreach (IMessageSummary message in fetched) 
            {
                if (print)
                    Console.WriteLine ("{0}: new message: {1}", SentFolder, message.Envelope.Subject);
                messages.Add(message);

                if (analyseImage)
                    await MaybeAnalyseImages(message, message.Attachments);
            }
        }

        private async Task MaybeAnalyseImages(IMessageSummary item, IEnumerable<BodyPartBasic> attachments)
        {
            Console.WriteLine($"Attachements count: {attachments.Count()}");

            // determine a directory to save stuff in
            var directory = Path.Combine(baseDirectory, item.UniqueId.ToString ());

            // create the directory
            Directory.CreateDirectory(directory);

            foreach (var attachement in attachments) {
                Console.WriteLine(attachement.ContentDescription);
                Console.WriteLine(attachement.ContentDisposition);
                Console.WriteLine(attachement.ContentId);
                Console.WriteLine(attachement.ContentLocation);
                Console.WriteLine(attachement.ContentMd5);
                Console.WriteLine(attachement.ContentTransferEncoding);
                Console.WriteLine(attachement.ContentType);
                Console.WriteLine(attachement.FileName);
                Console.WriteLine(attachement.IsAttachment);
                Console.WriteLine(attachement.Octets);
                Console.WriteLine(attachement.PartSpecifier);

                if (attachement.FileName.EndsWith(".jpg")) {
                    var entity = SentFolder.GetBodyPart(item.UniqueId, attachement);

                    var part = (MimePart)entity;

                    // note: it's possible for this to be null, but most will specify a filename
                    var fileName = part.FileName;

                    var path = Path.Combine(directory, fileName);

                    using (ComputerVisionClient client = AzureImageMLApi.Authenticate(config)) {

                        using (var stream = File.Create(path)) {
                            await part.Content.DecodeToAsync(stream);
                        }

                        await AzureImageMLApi.AnalyzeImage(client, path);
                    }
                }
            }
        }

        async Task WaitForNewMessagesAsync()
        {
            do 
            {
                try 
                {
                    if (client.Capabilities.HasFlag (ImapCapabilities.Idle)) 
                    {
                        // Note: IMAP servers are only supposed to drop the connection after 30 minutes, so normally
                        // we'd IDLE for a max of, say, ~29 minutes... but GMail seems to drop idle connections after
                        // about 10 minutes, so we'll only idle for 9 minutes.
                        done = new CancellationTokenSource(new TimeSpan (0, 9, 0));
                        try 
                        {
                            await client.IdleAsync(done.Token, cancel.Token);
                        } 
                        finally {
                            done.Dispose ();
                            done = null;
                        }
                    } 
                    else 
                    {
                        // Note: we don't want to spam the IMAP server with NOOP commands, so lets wait a minute
                        // between each NOOP command.
                        await Task.Delay(new TimeSpan (0, 1, 0), cancel.Token);
                        await client.NoOpAsync(cancel.Token);
                    }
                    break;
                } 
                catch (ImapProtocolException) 
                {
                    // protocol exceptions often result in the client getting disconnected
                    await ReconnectAsync();
                } 
                catch (IOException) 
                {
                    // I/O exceptions always result in the client getting disconnected
                    await ReconnectAsync();
                }
            } while (true);
        }

        async Task IdleAsync ()
        {
            do 
            {
                try 
                {
                    await WaitForNewMessagesAsync();

                    if (messagesArrived) 
                    {
                        await FetchMessageSummariesAsync(print: true, analyseImage: true);
                        messagesArrived = false;
                    }
                } 
                catch (OperationCanceledException) 
                {
                    break;
                }
            } while (!cancel.IsCancellationRequested);
        }

        public async Task RunAsync ()
        {
            // connect to the IMAP server and get our initial list of messages
            try 
            {
                await ReconnectAsync ();
                await FetchMessageSummariesAsync(print: false, analyseImage: false);
            } 
            catch (OperationCanceledException) 
            {
                await client.DisconnectAsync (true);
                return;
            }

            // Note: We capture client.Inbox here because cancelling IdleAsync() *may* require
            // disconnecting the IMAP client connection, and, if it does, the `client.Inbox`
            // property will no longer be accessible which means we won't be able to disconnect
            // our event handlers.
            var sentFolder = SentFolder;

            // keep track of changes to the number of messages in the folder (this is how we'll tell if new messages have arrived).
            sentFolder.CountChanged += OnCountChanged;

            // keep track of messages being expunged so that when the CountChanged event fires, we can tell if it's
            // because new messages have arrived vs messages being removed (or some combination of the two).
            sentFolder.MessageExpunged += OnMessageExpunged;

            // keep track of flag changes
            sentFolder.MessageFlagsChanged += OnMessageFlagsChanged;

            await IdleAsync ();

            sentFolder.MessageFlagsChanged -= OnMessageFlagsChanged;
            sentFolder.MessageExpunged -= OnMessageExpunged;
            sentFolder.CountChanged -= OnCountChanged;

            await client.DisconnectAsync(true);
        }

        // Note: the CountChanged event will fire when new messages arrive in the folder and/or when messages are expunged.
        void OnCountChanged (object? sender, EventArgs e)
        {
            var folder = (ImapFolder) sender;

            // Note: because we are keeping track of the MessageExpunged event and updating our
            // 'messages' list, we know that if we get a CountChanged event and folder.Count is
            // larger than messages.Count, then it means that new messages have arrived.
            if (folder.Count > messages.Count) {
                int arrived = folder.Count - messages.Count;

                if (arrived > 1)
                    Console.WriteLine("\t{0} new messages have arrived.", arrived);
                    
                else
                    Console.WriteLine("\t1 new message has arrived.");

                // Note: your first instinct may be to fetch these new messages now, but you cannot do
                // that in this event handler (the ImapFolder is not re-entrant).
                // 
                // Instead, cancel the `done` token and update our state so that we know new messages
                // have arrived. We'll fetch the summaries for these new messages later...
                messagesArrived = true;
                done?.Cancel ();
            }
        }

        void OnMessageExpunged(object? sender, MessageEventArgs e)
        {
            var folder = (ImapFolder) sender;

            if (e.Index < messages.Count) {
                var message = messages[e.Index];

                Console.WriteLine ("{0}: message #{1} has been expunged: {2}", folder, e.Index, message.Envelope.Subject);

                // Note: If you are keeping a local cache of message information
                // (e.g. MessageSummary data) for the folder, then you'll need
                // to remove the message at e.Index.
                messages.RemoveAt(e.Index);
            } else {
                Console.WriteLine("{0}: message #{1} has been expunged.", folder, e.Index);
            }
        }

        void OnMessageFlagsChanged (object? sender, MessageFlagsChangedEventArgs e)
        {
            var folder = (ImapFolder) sender;

            Console.WriteLine ("{0}: flags have changed for message #{1} ({2}).", folder, e.Index, e.Flags);
        }

        public void Exit ()
        {
            cancel.Cancel ();
        }

        public void Dispose ()
        {
            client.Dispose ();
            cancel.Dispose ();
        }
    }
}