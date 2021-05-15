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
using System.Text;

namespace AzureComputerVision 
{
    /// <summary>
    /// Imap client that listen wo the SentBox folder and apply image
    /// analysis from azure to the image when found. Results are stored 
    /// inside the appdata folder
    /// </summary>
    public class IdleClient : IDisposable
    {
        readonly string host, username, password;
        readonly SecureSocketOptions sslOptions;
        readonly int port;
        //readonly List<IMessageSummary> messages;
        int messagesCount;
        readonly CancellationTokenSource cancel;
        CancellationTokenSource? done;
        bool messagesArrived;
        readonly ImapClient client;
        readonly string baseDirectory;
        private readonly LoginInfo config;

        /// <summary>
        /// Create a IdleClient base on login config and a base directory 
        /// for store data
        /// </summary>
        /// <param name="config">Login info to the email and azure congitive service</param>
        /// <param name="baseDirectory">The base path to store the data</param>
        public IdleClient(LoginInfo config, string baseDirectory)
        {
            if (config.EmailLogin == null) throw new NotImplementedException("config.EmailLogin");
            if (config.EmailPassword == null) throw new NotImplementedException("config.EmailPassword");
            if (config.ImapServer == null) throw new NotImplementedException("config.ImapServer");

            this.client = new ImapClient(new ProtocolLogger(Console.OpenStandardError()));
            //this.messages = new List<IMessageSummary>();
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

        /// <summary>
        /// The SentFolder of the email account
        /// </summary>
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

        /// <summary>
        /// Run the client.
        /// </summary>
        public async Task RunAsync()
        {
            var messageCountPath = Path.Combine(baseDirectory, "messageCount.txt");

            if (File.Exists(messageCountPath))
            {
                messagesCount = int.Parse(await File.ReadAllTextAsync(messageCountPath));
            }

            // connect to the IMAP server and get our initial list of messages
            try
            {
                await ReconnectAsync();
                await FetchMessageSummariesAsync(print: false, analyseImage: messageId => Directory.Exists(Path.Combine(Constant.GetBaseDirectory(), messageId)) == false);
            }
            catch (OperationCanceledException)
            {
                await client.DisconnectAsync(true);
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

            await IdleAsync();

            sentFolder.MessageFlagsChanged -= OnMessageFlagsChanged;
            sentFolder.MessageExpunged -= OnMessageExpunged;
            sentFolder.CountChanged -= OnCountChanged;

            await client.DisconnectAsync(true);

            await File.WriteAllTextAsync(messageCountPath, messagesCount.ToString());
        }

        /// <summary>
        /// Log the call in stdout and communicate a request for cancellation
        /// </summary>
        public void Exit()
        {
            Console.WriteLine("IdleClient.Exit");
            cancel.Cancel();
        }

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            client.Dispose();
            cancel.Dispose();
        }

        async Task IdleAsync()
        {
            do
            {
                try
                {
                    await WaitForNewMessagesAsync();

                    if (messagesArrived)
                    {
                        await FetchMessageSummariesAsync(print: true, analyseImage: messageId => true);
                        messagesArrived = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            } while (!cancel.IsCancellationRequested);
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

        /// <param name="print">Print in console few information on new message fetched</param>
        /// <param name="analyseImage">A function that recieve the message uniqueId and indicate if the message need to be analysed</param>
        async Task FetchMessageSummariesAsync(bool print, Func<string, bool> analyseImage)
        {
            IList<IMessageSummary>? fetched;
            do
            {
                try 
                {
                    // fetch summary information for messages that we don't already have
                    int startIndex = messagesCount;

                    fetched = SentFolder.Fetch(startIndex, -1, MessageSummaryItems.Full | 
                                                               MessageSummaryItems.UniqueId | 
                                                               MessageSummaryItems.BodyStructure, cancel.Token);
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
                //messages.Add(message);
                messagesCount++;

                if (analyseImage(message.UniqueId.ToString()))
                    await MaybeAnalyseImages(message, message.Attachments);
            }
        }

        private async Task MaybeAnalyseImages(IMessageSummary item, IEnumerable<BodyPartBasic> attachments)
        {
            var attachmentCount = attachments.Count();

            Console.WriteLine($"Attachments count: {attachmentCount}");

            // determine a directory to save stuff in
            var directory = Path.Combine(baseDirectory, item.UniqueId.ToString());

            // create the directory
            Directory.CreateDirectory(directory);

            if (attachmentCount == 0) {
                Console.WriteLine("Look for a base64 image in the BodyParts");
                foreach (var part in item.BodyParts.Where(p => p.FileName?.EndsWith(".jpg") == true)) {
                    Console.WriteLine(part);
                    Console.WriteLine(part.GetType());

                    Console.WriteLine(part.ContentDescription);
                    Console.WriteLine(part.ContentDisposition);
                    Console.WriteLine(part.ContentId);
                    Console.WriteLine(part.ContentLocation);
                    Console.WriteLine(part.ContentMd5);
                    Console.WriteLine(part.ContentTransferEncoding);
                    Console.WriteLine(part.ContentType);
                    Console.WriteLine(part.FileName);
                    Console.WriteLine(part.IsAttachment);
                    Console.WriteLine(part.Octets);
                    Console.WriteLine(part.PartSpecifier);

                    // note: it's possible for this to be null, but most will specify a filename
                    var fileName = part.FileName;

                    var path = Path.Combine(directory, fileName);

                    var entity = SentFolder.GetBodyPart(item.UniqueId, part);

                    using (var stream = new MemoryStream())
                    {
                        entity.WriteTo(stream, cancel.Token);

                        var imageString = Encoding.ASCII.GetString(stream.ToArray());

                        ParseAndSaveImage(imageString, path);
                    }

                    using ComputerVisionClient client = AzureImageMLApi.Authenticate(config);
                    await AzureImageMLApi.AnalyzeImage(client, path);
                }

                return;
            }

            foreach (var attachment in attachments) {
                Console.WriteLine(attachment.ContentDescription);
                Console.WriteLine(attachment.ContentDisposition);
                Console.WriteLine(attachment.ContentId);
                Console.WriteLine(attachment.ContentLocation);
                Console.WriteLine(attachment.ContentMd5);
                Console.WriteLine(attachment.ContentTransferEncoding);
                Console.WriteLine(attachment.ContentType);
                Console.WriteLine(attachment.FileName);
                Console.WriteLine(attachment.IsAttachment);
                Console.WriteLine(attachment.Octets);
                Console.WriteLine(attachment.PartSpecifier);

                if (attachment.FileName.EndsWith(".jpg")) {
                    var entity = SentFolder.GetBodyPart(item.UniqueId, attachment);

                    var part = (MimePart)entity;

                    // note: it's possible for this to be null, but most will specify a filename
                    var fileName = part.FileName;

                    var path = Path.Combine(directory, fileName);

                    using ComputerVisionClient client = AzureImageMLApi.Authenticate(config);
                    using (var stream = File.Create(path))
                    {
                        await part.Content.DecodeToAsync(stream);
                    }

                    await AzureImageMLApi.AnalyzeImage(client, path);
                }
            }
        }

        async Task WaitForNewMessagesAsync()
        {
            do 
            {
                try 
                {
                    if (client.Capabilities.HasFlag(ImapCapabilities.Idle)) 
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
                            done.Dispose();
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

        // Note: the CountChanged event will fire when new messages arrive in the folder and/or when messages are expunged.
        void OnCountChanged(object? sender, EventArgs e)
        {
            var folder = (ImapFolder) sender;

            // Note: because we are keeping track of the MessageExpunged event and updating our
            // 'messages' list, we know that if we get a CountChanged event and folder.Count is
            // larger than messages.Count, then it means that new messages have arrived.
            if (folder.Count > messagesCount) {
                int arrived = folder.Count - messagesCount;

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
                done?.Cancel();
            }
        }

        void OnMessageExpunged(object? sender, MessageEventArgs e)
        {
            var folder = (ImapFolder) sender;

            if (e.Index < messagesCount) {
                //var message = messages[e.Index];

                //Console.WriteLine ("{0}: message #{1} has been expunged: {2}", folder, e.Index, message.Envelope.Subject);

                // Note: If you are keeping a local cache of message information
                // (e.g. MessageSummary data) for the folder, then you'll need
                // to remove the message at e.Index.
                //messages.RemoveAt(e.Index);
                messagesCount--;
            } else {
                Console.WriteLine("{0}: message #{1} has been expunged.", folder, e.Index);
            }
        }

        void OnMessageFlagsChanged(object? sender, MessageFlagsChangedEventArgs e)
        {
            var folder = (ImapFolder) sender;

            Console.WriteLine ("{0}: flags have changed for message #{1} ({2}).", folder, e.Index, e.Flags);
        }

        private static void ParseAndSaveImage(string imageString, string path)
        {
            var sb = new StringBuilder();

            var lines = imageString.Split('\n');

            var parseImage = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    parseImage = true;
                }

                if (parseImage)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) == false)
                    {
                        sb.Append(lines[i]);
                    }
                }
            }

            using var file = File.Create(path);
            file.Write(Convert.FromBase64String(sb.ToString()));
        }
    }
}