using System.Collections.Concurrent;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using System.Text;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorApp.AzureComputerVision;

/// <summary>
/// Imap client that listen wo the SentBox folder and apply image
/// analysis from azure to the image when found. Results are stored 
/// inside the appdata folder
/// </summary>
public class IdleClient : IDisposable, IObservable<ImageInfo>
{
    private readonly CancellationTokenSource _tokenSource;
    private CancellationTokenSource? done;
    private bool messagesArrived;
    private readonly ImapClient _imapClient;
    private readonly LoginInfo _loginInfo;
    private readonly ConcurrentDictionary<Guid, IObserver<ImageInfo>> _observers;
    private readonly IServiceScope _scoped;
    private readonly BlazorDbContext _context;
    private readonly AzureImageMLApi _azureImageML;
    private readonly IConfiguration _config;
    private readonly ILogger<IdleClient> _logger;
    private EmailStates? _emailStateDb;

    /// <summary>
    /// Create a IdleClient base on login config and a base directory 
    /// for store data
    /// </summary>
    public IdleClient(IOptions<LoginInfo> loginInfo, IServiceProvider provider, IConfiguration config, ImapClient imapClient, ILogger<IdleClient> logger)
    {
        _loginInfo = loginInfo.Value;
        if (_loginInfo.EmailLogin == null) throw new ArgumentNullException("config.EmailLogin");
        if (_loginInfo.EmailPassword == null) throw new ArgumentNullException("config.EmailPassword");
        if (_loginInfo.ImapServer == null) throw new ArgumentNullException("config.ImapServer");

        _imapClient = imapClient;
        _tokenSource = new CancellationTokenSource();
        _observers = new ConcurrentDictionary<Guid, IObserver<ImageInfo>>();
        _scoped = provider.CreateScope();
        _context = _scoped.ServiceProvider.GetRequiredService<BlazorDbContext>();
        _azureImageML = _scoped.ServiceProvider.GetRequiredService<AzureImageMLApi>();
        _config = config;
        _logger = logger;
    }

    private IMailFolder? _sentFolder;
    private DateTime _startDate;

    /// <summary>
    /// The SentFolder of the email account
    /// </summary>
    public IMailFolder SentFolder
    {
        get
        {
            if (_sentFolder is not null) return _sentFolder;

            var personal = _imapClient.GetFolder(_imapClient.PersonalNamespaces[0]);

            _sentFolder = personal.GetSubfolder("Sent Items", _tokenSource.Token);

            if (_sentFolder is null)
                throw new InvalidOperationException($"Sent folder cannot be found. The program tries those values: 'Sent Items'");

            return _sentFolder;
        }
    }

    public int MessageCount => SentFolder.Count;

    public bool IsAuthenticated => _imapClient.IsAuthenticated;

    /// <summary>
    /// Run the client.
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        // connect to the IMAP server and get our initial list of messages
        try
        {
            await ReconnectAsync();
            await FetchMessageSummariesAsync(print: false, analyseImage: AnalyseImagesBaseOnDates, token);
        }
        catch (OperationCanceledException)
        {
            await _imapClient.DisconnectAsync(true);
            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            throw;
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

        _logger.LogInformation("Remove events");
        sentFolder.MessageFlagsChanged -= OnMessageFlagsChanged;
        sentFolder.MessageExpunged -= OnMessageExpunged;
        sentFolder.CountChanged -= OnCountChanged;

        _logger.LogInformation("Disconnect client");
        await _imapClient.DisconnectAsync(true);
    }

    private bool AnalyseImagesBaseOnDates(IMessageSummary message)
    {
        var firstCheck = message.InternalDate >= _config.GetValue<DateTimeOffset>("StartDate");

        if (firstCheck)
        {
            return message.InternalDate >= _startDate;
        }

        return firstCheck;
    }

    /// <summary>
    /// Log the call in stdout and communicate a request for cancellation
    /// </summary>
    public void Exit()
    {
        _logger.LogInformation("IdleClient.Exit");
        if (_tokenSource.IsCancellationRequested == false)
        {
            _tokenSource.Cancel();
        }
        else
        {
            _logger.LogInformation("A cancellation request has already been sent. Nothing to do");
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        _logger.LogInformation("Disposing the IdleClient");
        GC.SuppressFinalize(this);
        _imapClient.Dispose();
        _tokenSource.Dispose();
        _scoped?.Dispose();
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
                    await FetchMessageSummariesAsync(print: true, analyseImage: AnalyseImagesBaseOnDates, CancellationToken.None);
                    messagesArrived = false;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        } while (!_tokenSource.IsCancellationRequested);
    }

    async Task ReconnectAsync()
    {
        if (!_imapClient.IsConnected)
            await _imapClient.ConnectAsync(_loginInfo.ImapServer, _loginInfo.ImapPort, SecureSocketOptions.SslOnConnect, _tokenSource.Token);

        if (!_imapClient.IsAuthenticated)
        {
            await _imapClient.AuthenticateAsync(_loginInfo.EmailLogin, _loginInfo.EmailPassword, _tokenSource.Token);

            await SentFolder.OpenAsync(FolderAccess.ReadOnly, _tokenSource.Token);
        }
    }

    /// <param name="print">Print in console few information on new message fetched</param>
    /// <param name="analyseImage">A function that recieve the message uniqueId and indicate if the message need to be analysed</param>
    async Task FetchMessageSummariesAsync(bool print, Func<IMessageSummary, bool> analyseImage, CancellationToken token)
    {
        IList<IMessageSummary>? fetched;
        do
        {
            try
            {
                _emailStateDb = await _context.EmailStates.Where(s => s.Email == _loginInfo.EmailLogin).FirstOrDefaultAsync();

                _startDate = (await _context.ImagesInfo.OrderByDescending(i => i.DateEmail).FirstOrDefaultAsync(token))?.DateEmail?.DateTime ?? _config.GetValue<DateTimeOffset>("StartDate").DateTime;

                if (_emailStateDb == null)
                {
                    _emailStateDb = new EmailStates
                    {
                        Email = _loginInfo.EmailLogin,
                        Id = Guid.NewGuid()
                    };

                    var entry = await _context.EmailStates.AddAsync(_emailStateDb, token);

                    await _context.SaveChangesAsync(token);

                    _emailStateDb = entry.Entity;
                }

                var idList = await SentFolder.SearchAsync(MailKit.Search.SearchQuery.SentSince(_startDate), token);

                fetched = await SentFolder.FetchAsync(idList, MessageSummaryItems.Full |
                                                              MessageSummaryItems.UniqueId |
                                                              MessageSummaryItems.BodyStructure, _tokenSource.Token);
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
                _logger.LogInformation("{0}: new message: {1}", SentFolder, message.Envelope.Subject);

            if (analyseImage(message))
            {
                if (_emailStateDb != null)
                {
                    _emailStateDb.MessagesCount++;

                    _context.Update(_emailStateDb);

                    await _context.SaveChangesAsync(token);
                }

                await MaybeAnalyseImagesAsync(message, message.Attachments, token);
            }
                
        }
    }

    private async Task MaybeAnalyseImagesAsync(IMessageSummary item, IEnumerable<BodyPartBasic> attachments, CancellationToken token)
    {
        var attachmentCount = attachments.Count();

        _logger.LogInformation($"Attachments count: {attachmentCount}");

        if (attachmentCount == 0)
        {
            _logger.LogInformation("Look for a base64 image in the BodyParts");
            foreach (var part in item.BodyParts.Where(p => p.FileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
                                                           p.FileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true))
            {
                _logger.LogInformation(part.ToString());
                _logger.LogInformation(part.GetType().ToString());

                _logger.LogInformation(part.ContentDescription);
                _logger.LogInformation(part.ContentDisposition?.ToString());
                _logger.LogInformation(part.ContentId);
                _logger.LogInformation(part.ContentLocation?.ToString());
                _logger.LogInformation(part.ContentMd5);
                _logger.LogInformation(part.ContentTransferEncoding);
                _logger.LogInformation(part.ContentType?.ToString());
                _logger.LogInformation(part.FileName);
                _logger.LogInformation(part.IsAttachment.ToString());
                _logger.LogInformation(part.Octets.ToString());
                _logger.LogInformation(part.PartSpecifier);

                // note: it's possible for this to be null, but most will specify a filename
                var fileName = part.FileName;

                var entity = await SentFolder.GetBodyPartAsync(item.UniqueId, part, token);

                var imageInfo = new ImageInfo
                {
                    Name = fileName,
                    DateAjout = DateTimeOffset.Now,
                    DateEmail = item.InternalDate ?? item.Date
                };

                if (item.InternalDate.HasValue)
                {
                    _startDate = item.InternalDate.Value.DateTime;
                }
                else
                {
                    _startDate = item.Date.DateTime;
                }

                using (var stream = new MemoryStream())
                {
                    entity.WriteTo(stream, _tokenSource.Token);

                    var imageString = Encoding.ASCII.GetString(stream.ToArray());

                    await ParseAndSaveImageAsync(imageString, imageInfo, token);
                }

                using ComputerVisionClient client = AzureImageMLApi.Authenticate(_loginInfo);
                await _azureImageML.AnalyzeImageAsync(client, imageInfo, _observers);
            }

            return;
        }

        foreach (var attachment in attachments)
        {
            _logger.LogInformation(attachment.ContentDescription);
            _logger.LogInformation(attachment.ContentDisposition?.ToString());
            _logger.LogInformation(attachment.ContentId);
            _logger.LogInformation(attachment.ContentLocation?.ToString());
            _logger.LogInformation(attachment.ContentMd5);
            _logger.LogInformation(attachment.ContentTransferEncoding);
            _logger.LogInformation(attachment.ContentType?.ToString());
            _logger.LogInformation(attachment.FileName);
            _logger.LogInformation(attachment.IsAttachment.ToString());
            _logger.LogInformation(attachment.Octets.ToString());
            _logger.LogInformation(attachment.PartSpecifier);

            if (attachment.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                attachment.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var entity = await SentFolder.GetBodyPartAsync(item.UniqueId, attachment);

                var part = (MimePart)entity;

                string? fileName = part.FileName;

                var imageInfo = new ImageInfo
                {
                    Name = fileName,
                    DateAjout = DateTimeOffset.Now,
                    DateEmail = item.InternalDate ?? item.Date
                };

                if (item.InternalDate.HasValue)
                {
                    _startDate = item.InternalDate.Value.DateTime;
                }
                else
                {
                    _startDate = item.Date.DateTime;
                }

                using (var stream = new MemoryStream())
                {
                    await part.Content.DecodeToAsync(stream);

                    await stream.FlushAsync(token);

                    imageInfo.Images = stream.ToArray();

                    _ = _context.ImagesInfo.AddAsync(imageInfo, token);

                    if (_emailStateDb != null)
                    {
                        _emailStateDb.Size += imageInfo.Images?.Length ?? 0;

                        _context.Update(_emailStateDb);
                    }

                    await _context.SaveChangesAsync(token);
                }

                using ComputerVisionClient client = AzureImageMLApi.Authenticate(_loginInfo);
                await _azureImageML.AnalyzeImageAsync(client, imageInfo, _observers, token);
            }
        }
    }

    async Task WaitForNewMessagesAsync()
    {
        while (true)
        {
            try
            {
                if (_imapClient.Capabilities.HasFlag(ImapCapabilities.Idle))
                {
                    // Note: IMAP servers are only supposed to drop the connection after 30 minutes, so normally
                    // we'd IDLE for a max of, say, ~29 minutes... but GMail seems to drop idle connections after
                    // about 10 minutes, so we'll only idle for 9 minutes.
                    done = new CancellationTokenSource(new TimeSpan(0, 9, 0));
                    try
                    {
                        await _imapClient.IdleAsync(done.Token, _tokenSource.Token);
                    }
                    finally
                    {
                        done.Dispose();
                        done = null;
                    }
                }
                else
                {
                    // Note: we don't want to spam the IMAP server with NOOP commands, so lets wait a minute
                    // between each NOOP command.
                    await Task.Delay(new TimeSpan(0, 1, 0), _tokenSource.Token);
                    await _imapClient.NoOpAsync(_tokenSource.Token);
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
        }
    }

    // Note: the CountChanged event will fire when new messages arrive in the folder and/or when messages are expunged.
    void OnCountChanged(object? sender, EventArgs e)
    {
        var folder = sender as ImapFolder;

        if (folder != null && folder.Count > _emailStateDb?.MessagesCount)
        {
            int arrived = folder.Count - _emailStateDb.MessagesCount;

            if (arrived > 1)
                _logger.LogInformation("\t{0} new messages have arrived.", arrived);

            else
                _logger.LogInformation("\t1 new message has arrived.");

            messagesArrived = true;
            done?.Cancel();
        }
    }

    async void OnMessageExpunged(object? sender, MessageEventArgs e)
    {
        var folder = sender as ImapFolder;

        if (e.Index < _emailStateDb?.MessagesCount)
        {
            _emailStateDb.MessagesCount -= 1;

            _context.Update(_emailStateDb);

            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("{0}: message #{1} has been expunged.", folder, e.Index);
        }
    }

    void OnMessageFlagsChanged(object? sender, MessageFlagsChangedEventArgs e)
    {
        var folder = sender as ImapFolder;

        _logger.LogInformation("{0}: flags have changed for message #{1} ({2}).", folder, e.Index, e.Flags);
    }

    private async Task ParseAndSaveImageAsync(string imageString, ImageInfo imageInfo, CancellationToken token)
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

        imageInfo.Images = Convert.FromBase64String(sb.ToString());

        _ = _context.ImagesInfo.AddAsync(imageInfo, token);

        if (_emailStateDb != null)
        {
            _emailStateDb.Size += imageInfo.Images?.Length ?? 0;

            _context.Update(_emailStateDb);
        }

        await _context.SaveChangesAsync(token);
    }

    public IDisposable Subscribe(IObserver<ImageInfo> observer)
    {
        var type = observer.GetType();
        var idProperty = type.GetProperty("ClientSessionId");
        Guid sessionId = idProperty?.GetValue(observer) as Guid? ?? Guid.Empty;

        if (_observers.TryAdd(sessionId, observer))
        {
            _logger.LogInformation($"Subscribe {sessionId} successfully");
        }
        else
        {
            _logger.LogError($"[WRN] Failed to subscribe {sessionId}");
        }

        return this;
    }

    public IDisposable Unsubscribe(IObserver<ImageInfo> observer)
    {
        var type = observer.GetType();
        var idProperty = type.GetProperty("ClientSessionId");
        var sessionId = idProperty?.GetValue(observer) as Guid?;

        if (_observers.TryRemove(sessionId ?? Guid.Empty, out var observer1))
        {
            _logger.LogInformation($"Unsubscribe {sessionId ?? Guid.Empty} successfully");
        }
        else
        {
            _logger.LogError($"[WRN] Failed to unsubscribe {sessionId ?? Guid.Empty}");
        }

        return this;
    }
}
