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
using System.Text.Json;

namespace BlazorApp.AzureComputerVision;

/// <summary>
/// Imap client that listen wo the SentBox folder and apply image
/// analysis from azure to the image when found. Results are stored 
/// inside the appdata folder
/// </summary>
public class IdleClient : IDisposable, IObservable<ImageInfo>
{
    /// <summary>
    /// A global cancellation token source
    /// </summary>
    private readonly CancellationTokenSource _tokenSource;
    private CancellationTokenSource? _done;
    private bool messagesArrived;
    private readonly IImapClient _imapClient;
    private readonly LoginInfo _loginInfo;
    private readonly ConcurrentDictionary<Guid, IObserver<ImageInfo>> _observers;
    private readonly IServiceScope _scoped;
    private readonly BlazorDbContext _context;
    private readonly AzureImageMLApi _azureImageML;
    private readonly IConfiguration _config;
    private readonly ILogger<IdleClient> _logger;
    private EmailStates? _emailStateDb;
    private long? _uniqueId;

    /// <summary>
    /// Create a IdleClient base on login config and a base directory 
    /// for store data
    /// </summary>
    public IdleClient(IOptions<LoginInfo> loginInfo,
                      IServiceProvider provider,
                      IConfiguration config,
                      IImapClient imapClient,
                      ILogger<IdleClient> logger)
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
    private DateTimeOffset _startDate;

    /// <summary>
    /// The SentFolder of the email account
    /// </summary>
    public IMailFolder SentFolder
    {
        get
        {
            if (_sentFolder is not null) return _sentFolder;

            if (_loginInfo.ImapServer?.Contains("gmail", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Gmail uses the [Gmail]/Sent Mail folder
                _sentFolder = _imapClient.GetFolder(SpecialFolder.Sent);

                _logger?.LogInformation(_sentFolder.ToString());
            }
            else
            {
                var personal = _imapClient.GetFolder(_imapClient.PersonalNamespaces[0]);

                _sentFolder = personal.GetSubfolder("Sent Items", _tokenSource.Token);
            }

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
        _logger.LogInformation("RunAsync");

        // connect to the IMAP server and get our initial list of messages
        try
        {
            await ReconnectAsync();
            await FetchMessageSummariesAsync(print: false, analyseImage: CheckConfigDateAndThenUniqueId, token);
        }
        catch (OperationCanceledException ocEx)
        {
            _logger.LogError(ocEx, "RunAsync Exception: {message}", ocEx.Message);
            await _imapClient.DisconnectAsync(true);
            return;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "RunAsync Exception: {message}", e.Message);
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

        _logger.LogInformation("RunAsync: IdleAsync");
        await IdleAsync(token);

        _logger.LogInformation("RunAsync: Remove events");
        sentFolder.MessageFlagsChanged -= OnMessageFlagsChanged;
        sentFolder.MessageExpunged -= OnMessageExpunged;
        sentFolder.CountChanged -= OnCountChanged;

        _logger.LogInformation("RunAsync: Disconnect client");
        await _imapClient.DisconnectAsync(true);
    }

    private async Task<bool> CheckConfigDateAndThenUniqueId(IMessageSummary message)
    {
        var firstCheck = message.InternalDate >= _config.GetValue<DateTimeOffset>("StartDate");

        if (firstCheck)
        {
            return !await _context.ImagesInfo.AnyAsync(i => i.UniqueId == message.UniqueId.Id);
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

    public async Task IdleAsync(CancellationToken token)
    {
        do
        {
            try
            {
                await WaitForNewMessagesAsync();

                _logger.LogInformation("IdleAsync: messagesArrived: {messagesArrived}", messagesArrived);

                if (messagesArrived)
                {
                    if (CheckDiscardingRateLimiter())
                    {
                        _logger.LogInformation("IdleAsync: FetchMessageSummariesAsync");
                        await FetchMessageSummariesAsync(print: true, analyseImage: CheckConfigDateAndThenUniqueId, token);
                        messagesArrived = false;
                    }
                    else
                    {
                        var rate = _callMemory.Where(c => !c.WasDiscarded).Count() / 10.0;

                        _logger.LogWarning("DiscardingRateLimiter apply. Rate is {rate}", rate);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        } while (!_tokenSource.IsCancellationRequested);
    }

    private List<CallShortMemoryContext> _callMemory = new List<CallShortMemoryContext>();

    private bool CheckDiscardingRateLimiter()
    {
        var rateLimiter = _config.GetValue<double?>("DiscardWhenTPMGreaterThan");

        if (rateLimiter.HasValue && rateLimiter.Value > 0.0)
        {
            var endMemory = DateTime.Now - TimeSpan.FromMinutes(10);

            for (int i = 0; i < _callMemory.Count; i++)
            {
                if (_callMemory[i].Date < endMemory)
                {
                    _callMemory.RemoveAt(i--);
                }
            }

            var analyse = _callMemory.Where(c => !c.WasDiscarded).Count() / 10.0 < rateLimiter.Value;

            _callMemory.Add(new CallShortMemoryContext
            {
                Date = DateTime.Now,
                WasDiscarded = !analyse
            });

            return analyse;
        }

        return true;
    }

    /// <summary>
    /// Reconnect the client to the server and authenticate.
    /// No need to pass the cancellation token because it use the _tokenSource from
    /// the class instance.
    /// </summary>
    /// <returns></returns>
    async Task ReconnectAsync()
    {
        if (!_imapClient.IsConnected)
        {
            _logger.LogInformation("Connect to the imap server");

            await _imapClient.ConnectAsync(_loginInfo.ImapServer, _loginInfo.ImapPort, SecureSocketOptions.SslOnConnect, _tokenSource.Token);

            _logger.LogInformation("Connection established.");
        }

        if (!_imapClient.IsAuthenticated)
        {
            _logger.LogInformation("Authenticate to the imap server");

            await _imapClient.AuthenticateAsync(_loginInfo.EmailLogin, _loginInfo.EmailPassword, _tokenSource.Token);

            _logger.LogInformation("Authenticated. Open the SentFolder.");

            await SentFolder.OpenAsync(FolderAccess.ReadOnly, _tokenSource.Token);

            _logger.LogInformation("SentFolder opened.");
        }
    }

    /// <param name="print">Print in console few information on new message fetched</param>
    /// <param name="analyseImage">A function that recieve the message uniqueId and indicate if the message need to be analysed</param>
    async Task FetchMessageSummariesAsync(bool print, Func<IMessageSummary, Task<bool>> analyseImage, CancellationToken token)
    {
        _logger.LogInformation("FetchMessageSummariesAsync method begin");

        IList<IMessageSummary>? fetched;
        do
        {
            try
            {
                _emailStateDb = await _context.EmailStates.Where(s => s.Email == _loginInfo.EmailLogin).FirstOrDefaultAsync(token);

                ImageInfo? imageInfo = null;

                if (await _context.ImagesInfo.AnyAsync(token))
                {
                    imageInfo = await _context.ImagesInfo
                        .OrderByDescending(i => i.DateEmail)
                        .FirstOrDefaultAsync(token);

                    _uniqueId = imageInfo?.UniqueId;

                    _startDate = imageInfo?.DateEmail ?? _config.GetValue<DateTimeOffset>("StartDate").DateTime;

                    _logger.LogInformation("Unique id: {uniqueId} _startdate: {date}", _uniqueId, _startDate);
                }

                if (_uniqueId == null)
                {
                    _startDate = _config.GetValue<DateTimeOffset>("StartDate").DateTime;

                    _logger.LogInformation("Unique id: {uniqueId} _startdate: {date}", _uniqueId, _startDate);
                }

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
                else 
                {
                    _logger.LogInformation("EmailStateDb is null no need to create a new.");
                }

                if (_uniqueId.HasValue &&
                    _emailStateDb.Email != null &&
                    _emailStateDb.Email.Contains("gmail", StringComparison.OrdinalIgnoreCase) == false)
                {
                    _logger.LogInformation("Fetch message since uniqueId: {uniqueId} datetime: {date}", _uniqueId, imageInfo?.DateEmail);

                    fetched = await SentFolder.FetchAsync(MessageCount - 1, -1, MessageSummaryItems.Full |
                                                                                MessageSummaryItems.UniqueId |
                                                                                MessageSummaryItems.BodyStructure, token);

                    _logger.LogInformation("Fetched {fetched} messages", fetched.Count);
                }
                else
                {
                    _logger.LogInformation("Fetch message since startDate: {startDate}", _startDate);

                    using var searchTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                    var idList = await SentFolder.SearchAsync(MailKit.Search.SearchQuery.SentSince(_startDate.DateTime), searchTokenSource.Token);

                    _logger.LogInformation("idList: {idList}", idList.Count);

                    var preFetch = await SentFolder.FetchAsync(idList, MessageSummaryItems.UniqueId |
                                                                        MessageSummaryItems.Envelope, token);

                    _logger.LogInformation("PreFetched {preFetched} messages", preFetch.Count);

                    var filtredIdList = preFetch.Where(m => m.Date >= _startDate).Select(m => m.UniqueId).ToList();

                    _logger.LogInformation("filtredIdList: {filtredIdList}", filtredIdList.Count);

                    fetched = await SentFolder.FetchAsync(filtredIdList, MessageSummaryItems.Full |
                                                                  MessageSummaryItems.UniqueId |
                                                                  MessageSummaryItems.BodyStructure, _tokenSource.Token);

                    _logger.LogInformation("Fetched {fetched} messages", fetched.Count);
                }

                break;
            }
            catch (ImapProtocolException imapEx)
            {
                _logger.LogWarning(imapEx, imapEx.Message);

                // protocol exceptions often result in the client getting disconnected
                await ReconnectAsync();
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning(ioEx, ioEx.Message);

                // I/O exceptions always result in the client getting disconnected
                await ReconnectAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, $"Unmanaged exception in {nameof(FetchMessageSummariesAsync)} " + e.Message);
            }
        } while (true);

        foreach (IMessageSummary message in fetched)
        {
            if (print) 
            {
                _logger.LogInformation("{SentFolder}: new message: {Subject}", SentFolder.ToString(), message.Envelope.Subject);
            }

            if (await analyseImage(message))
            {
                await MaybeAnalyseImagesAsync(message, message.Attachments, token);
            }
        }
    }

    private async Task MaybeAnalyseImagesAsync(IMessageSummary item, IEnumerable<BodyPartBasic> attachments, CancellationToken token)
    {
        var attachmentCount = attachments.Count();

        _logger.LogInformation("Attachments count: {attachmentCount}", attachmentCount);

        if (attachmentCount == 0)
        {
            _logger.LogInformation("Look for a base64 image in the BodyParts");
            foreach (var part in item.BodyParts.Where(p => p.FileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
                                                           p.FileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true))
            {
                _logger.LogDebug("part: {part}", part.ToString());
                _logger.LogDebug("part.Type: {partType}", part.GetType().ToString());
                _logger.LogDebug("part.Descirptionpart: {contentDescription}", part.ContentDescription);
                _logger.LogDebug("part.ContentDisposition: {contentDisposition}", part.ContentDisposition?.ToString());
                _logger.LogDebug("part.ContentId: {contentId}", part.ContentId);
                _logger.LogDebug("part.ContentLocation: {contentLocation}", part.ContentLocation?.ToString());
                _logger.LogDebug("part.ContentMd5: {contentMd5}", part.ContentMd5);
                _logger.LogDebug("part.ContentTransferEncoding: {transfertEncoding}", part.ContentTransferEncoding);
                _logger.LogDebug("part.ContentType: {contentType}", part.ContentType?.ToString());
                _logger.LogDebug("part.FileName: {fileName}", part.FileName);
                _logger.LogDebug("part.IsAttachment: {isAttachment}", part.IsAttachment.ToString());
                _logger.LogDebug("part.Octets: {octets}", part.Octets.ToString());
                _logger.LogDebug("part.PartSpecifier: {partSpecifier}", part.PartSpecifier);

                // note: it's possible for this to be null, but most will specify a filename
                var fileName = part.FileName;

                var entity = await SentFolder.GetBodyPartAsync(item.UniqueId, part, token);

                var imageInfo = new ImageInfo
                {
                    Name = fileName,
                    DateAjout = DateTimeOffset.Now,
                    DateEmail = item.InternalDate ?? item.Date,
                    UniqueId = item.UniqueId.Id
                };

                _logger.LogInformation("New ImageInfo: {imageInfo}", JsonSerializer.Serialize(imageInfo));

                if (item.InternalDate.HasValue)
                {
                    _startDate = item.InternalDate.Value;
                }
                else
                {
                    _startDate = item.Date;
                }

                _logger.LogInformation("New Start date: {startDate}", _startDate);

                using (var stream = new MemoryStream())
                {
                    entity.WriteTo(stream, _tokenSource.Token);

                    var imageString = Encoding.ASCII.GetString(stream.ToArray());

                    await ParseAndSaveImageAsync(imageString, imageInfo, token);
                }

                using ComputerVisionClient client = AzureImageMLApi.Authenticate(_loginInfo);
                await _azureImageML.AnalyzeImageAsync(client, imageInfo, _observers);

                if (_emailStateDb != null)
                {
                    _emailStateDb.MessagesCount++;

                    _context.Update(_emailStateDb);

                    await _context.SaveChangesAsync(token);
                }
            }

            return;
        }

        foreach (var attachment in attachments)
        {
            _logger.LogDebug("Attachment.ContentDescription: {contentDescription}", attachment.ContentDescription);
            _logger.LogDebug("Attachment.ContentDisposition: {contentDisposition}", attachment.ContentDisposition?.ToString());
            _logger.LogDebug("Attachment.ContentId: {contentId}", attachment.ContentId);
            _logger.LogDebug("Attachment.ContentLocation: {contentLocation}", attachment.ContentLocation?.ToString());
            _logger.LogDebug("Attachment.ContentMd5: {contentMd5}", attachment.ContentMd5);
            _logger.LogDebug("Attachment.ContentTransfertEncoding: {contentTransfertEncoding}", attachment.ContentTransferEncoding);
            _logger.LogDebug("Attachment.ContentType: {contentType}", attachment.ContentType?.ToString());
            _logger.LogDebug("Attachment.FileName: {filename}", attachment.FileName);
            _logger.LogDebug("Attachment.IsAttachement: {isAttachment}", attachment.IsAttachment.ToString());
            _logger.LogDebug("Attachment.Octets: {octets}", attachment.Octets.ToString());
            _logger.LogDebug("Attachment.PartSpecifier: {partSpecifier}", attachment.PartSpecifier);

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
                    DateEmail = item.InternalDate ?? item.Date,
                    UniqueId = item.UniqueId.Id
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

                if (_emailStateDb != null)
                {
                    _emailStateDb.MessagesCount++;

                    _context.Update(_emailStateDb);

                    await _context.SaveChangesAsync(token);
                }
            }
        }
    }

    async Task WaitForNewMessagesAsync()
    {
        while (!messagesArrived && (_done == null || !_done.IsCancellationRequested) && !_tokenSource.IsCancellationRequested)
        {
            try
            {
                if (_imapClient.Capabilities.HasFlag(ImapCapabilities.Idle))
                {
                    _logger.LogInformation("WaitForNewMessagesAsync: Idling...");

                    // Note: IMAP servers are only supposed to drop the connection after 30 minutes, so normally
                    // we'd IDLE for a max of, say, ~29 minutes... but GMail seems to drop idle connections after
                    // about 10 minutes, so we'll only idle for 9 minutes.
                    _done = new CancellationTokenSource(new TimeSpan(0, 9, 0));
                    try
                    {
                        if (!_imapClient.IsConnected)
                        {
                            _logger.LogInformation("_imapClient was not connected during the WaitForNewMessagesAsync method");
                            await ReconnectAsync();
                        }
                        else
                        {
                            _logger.LogInformation($"_imapClient already connected. That's good");
                        }

                        if (!SentFolder.IsOpen)
                        {
                            _logger.LogInformation("SendFolder was not open, now oppening the sentFolder");

                            await SentFolder.OpenAsync(FolderAccess.ReadOnly, _tokenSource.Token);

                            _logger.LogInformation($"SentFolder open. Done");
                        }
                        else
                        {
                            _logger.LogInformation("SentFolder already open. That's good");
                        }

                        _logger.LogInformation("Now calling _imapClient.IdleAsync(_done.Token, _tokenSource.Token");

                        await _imapClient.IdleAsync(_done.Token, _tokenSource.Token);

                        _logger.LogInformation("Done awaiting the _imapClient.IdleAsync");
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Exception when idling: {message}", e.Message);
                    }
                    finally
                    {
                        _logger.LogInformation($"{nameof(WaitForNewMessagesAsync)} Finish Idling.");
                        _done.Dispose();
                        _done = null;
                    }
                }
                else
                {
                    _logger.LogInformation("NOOPing...");

                    // Note: we don't want to spam the IMAP server with NOOP commands, so lets wait a minute
                    // between each NOOP command.
                    await Task.Delay(new TimeSpan(0, 1, 0), _tokenSource.Token);
                    await _imapClient.NoOpAsync(_tokenSource.Token);
                }
            }
            catch (ImapProtocolException ipEx)
            {
                _logger.LogWarning(ipEx, "Error in WaitForNewMessagesAsync: {message}", ipEx.Message);

                // protocol exceptions often result in the client getting disconnected
                await ReconnectAsync();
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning(ioEx, "Error in WaitForNewMessagesAsync: {message}", ioEx.Message);

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
                _logger.LogInformation("\tOnCountChanged: {arrived} new messages have arrived.", arrived);

            else
                _logger.LogInformation("\tOnCountChanged: 1 new message has arrived.");

            messagesArrived = true;
            _done?.Cancel();
        }
        else
        {
            _logger.LogWarning("\tOnCountChanged: {folder}: {Count} messages.", folder, folder?.Count);
            _logger.LogWarning("\tOnCountChanged: Seems like nothing changed...");
        }
    }

    async void OnMessageExpunged(object? sender, MessageEventArgs e)
    {
        _logger.LogInformation("OnMessageExpundeg: {folder}: message #{Index} has been expunged.", sender, e.Index);

        var folder = sender as ImapFolder;

        if (e.Index < _emailStateDb?.MessagesCount)
        {
            _emailStateDb.MessagesCount -= 1;

            _context.Update(_emailStateDb);

            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("OnMessageExpundeg: {folder}: message #{Index} has been expunged.", folder, e.Index);
        }
    }

    void OnMessageFlagsChanged(object? sender, MessageFlagsChangedEventArgs e)
    {
        try
        {
            var folder = sender as ImapFolder;

            _logger.LogInformation("{folder}: flags have changed for message #{Index} ({Flags}).", folder?.ToString(), e.Index, e.Flags);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{sender?.ToString()}: flags have changed for message #{e.Index} ({e.Flags}).");
            Console.Error.WriteLine($"OnMessageFlgsChanged: {ex}");
            Console.Error.WriteLine("Last error should not have crash the program.");
        }
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
            _logger.LogInformation("Subscribe {sessionId} successfully", sessionId);
        }
        else
        {
            _logger.LogError("[WRN] Failed to subscribe {sessionId}", sessionId);
        }

        return this;
    }

    public IDisposable Unsubscribe(IObserver<ImageInfo> observer)
    {
        var type = observer.GetType();
        var idProperty = type.GetProperty("ClientSessionId");
        var sessionId = idProperty?.GetValue(observer) as Guid?;

        if (_observers.TryRemove(sessionId ?? Guid.Empty, out _))
        {
            _logger.LogInformation("Unsubscribe {sessionId} successfully", sessionId);
        }
        else
        {
            _logger.LogError("[WRN] Failed to unsubscribe {sessionId}", sessionId);
        }

        return this;
    }
}
