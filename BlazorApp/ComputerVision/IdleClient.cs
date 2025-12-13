using System.Collections.Concurrent;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;
using System.Text;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using BlazorApp.Extension;
using Florence2;
using BlazorApp.Model;
using BlazorApp.ComputerVision;

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
    public IImapClient _imapClient { get; }
    private readonly LoginInfo _loginInfo;
    private readonly ConcurrentDictionary<Guid, IObserver<ImageInfo>> _observers;
    private readonly IServiceScope _scoped;
    private readonly BlazorDbContext _context;
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

                _logger?.LogInformation("SentFolder toString: {SendFolder}", _sentFolder?.ToString());
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
            _logger.LogInformation("RunAsync: ReconnectAsync complete, now FetchMessageSummariesAsync");
            await FetchMessageSummariesAsync(print: false, analyseImage: CheckConfigDateAndThenUniqueId, token);
        }
        catch (OperationCanceledException ocEx)
        {
            _logger.LogError(ocEx, "RunAsync Exception: {Message}", ocEx.Message);
            await _imapClient.DisconnectAsync(true);
            return;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "RunAsync Exception: {Message} {StackTrace}", e.Message, e.StackTrace);
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
        if (!_tokenSource.IsCancellationRequested)
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

                _logger.LogInformation("IdleAsync: messagesArrived: {MessagesArrived}", messagesArrived);

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
                        var rate = _callMemory.Count(c => !c.WasDiscarded) / 10.0;

                        _logger.LogWarning("DiscardingRateLimiter apply. Rate is {Rate}", rate);

                        await Task.Delay(TimeSpan.FromSeconds(10), token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        } while (!_tokenSource.IsCancellationRequested);
    }

    private readonly List<CallShortMemoryContext> _callMemory = new List<CallShortMemoryContext>();

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

            var analyse = _callMemory.Count(c => !c.WasDiscarded) / 10.0 < rateLimiter.Value;

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

                var anyImageInfo = await _context.ImagesInfo.AnyAsync(token);

                if (anyImageInfo)
                {
                    imageInfo = await _context.ImagesInfo
                        .OrderByDescending(i => i.DateEmail)
                        .FirstOrDefaultAsync(token);

                    _uniqueId = imageInfo?.UniqueId;

                    _startDate = imageInfo?.DateEmail ?? _config.GetValue<DateTimeOffset>("StartDate").DateTime;

                    _logger.LogInformation("Unique id: {UniqueId} _startdate: {Date}", _uniqueId, _startDate);
                }

                if (_uniqueId == null)
                {
                    _startDate = _config.GetValue<DateTimeOffset>("StartDate").DateTime;

                    _logger.LogInformation("Unique id: {UniqueId} _startdate: {Date}", _uniqueId, _startDate);
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
                    !_emailStateDb.Email.Contains("gmail", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Fetch message since uniqueId: {UniqueId} datetime: {Date}", _uniqueId, imageInfo?.DateEmail);

                    fetched = await SentFolder.FetchAsync(MessageCount - 1, -1, MessageSummaryItems.Full |
                                                                                MessageSummaryItems.UniqueId |
                                                                                MessageSummaryItems.BodyStructure, token);

                    _logger.LogInformation("Fetched {Fetched} messages", fetched.Count);
                }
                else
                {
                    var startDateAjusted = _startDate.LocalDateTime;

                    _logger.LogInformation("Fetch message since startDate: {StartDate}", startDateAjusted);

                    using var searchTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

                    var dateFiter = MailKit.Search.SearchQuery.SentSince(startDateAjusted);

                    var idList = await SentFolder.SearchAsync(dateFiter, searchTokenSource.Token);

                    _logger.LogInformation("idList: {IdList}", idList.Count);

                    var preFetch = await SentFolder.FetchAsync(idList, MessageSummaryItems.UniqueId |
                                                                        MessageSummaryItems.Envelope, token);

                    _logger.LogInformation("PreFetched {PreFetched} messages", preFetch.Count);

                    var filtredIdList = preFetch.Where(m => m.Date >= _startDate).Select(m => m.UniqueId).ToList();

                    _logger.LogInformation("filtredIdList: {FiltredIdList}", filtredIdList.Count);

                    fetched = await SentFolder.FetchAsync(filtredIdList, MessageSummaryItems.Full |
                                                                  MessageSummaryItems.UniqueId |
                                                                  MessageSummaryItems.BodyStructure, _tokenSource.Token);

                    _logger.LogInformation("Fetched {Fetched} messages", fetched.Count);
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
                await Task.Delay(10000, token);

                _logger.LogCritical(e, $"Unmanaged exception in {nameof(FetchMessageSummariesAsync)} " + e.Message);

                // try reconnect
                try
                {
                    await ReconnectAsync();
                }
                catch (Exception e2)
                {
                    _logger.LogCritical(e2, $"Unmanaged exception in trying reconnect {nameof(FetchMessageSummariesAsync)} " + e2.Message);
                }
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

        _logger.LogInformation("Attachments count: {AttachmentCount}", attachmentCount);

        if (attachmentCount == 0)
        {
            _logger.LogInformation("Look for a base64 image in the BodyParts");
            foreach (var part in item.BodyParts.Where(p => p.FileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
                                                           p.FileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true))
            {
                _logger.LogDebug("Part details: Type={PartType}, Description={ContentDescription}, ContentDisposition={ContentDisposition}, ContentId={ContentId}, ContentLocation={ContentLocation}, ContentMd5={ContentMd5}, ContentTransferEncoding={ContentTransferEncoding}, ContentType={ContentType}, FileName={FileName}, IsAttachment={IsAttachment}, Octets={Octets}, PartSpecifier={PartSpecifier}",
                    part.GetType().ToString(),
                    part.ContentDescription,
                    part.ContentDisposition?.ToString(),
                    part.ContentId,
                    part.ContentLocation?.ToString(),
                    part.ContentMd5,
                    part.ContentTransferEncoding,
                    part.ContentType?.ToString(),
                    part.FileName,
                    part.IsAttachment.ToString(),
                    part.Octets.ToString(),
                    part.PartSpecifier);

                // note: it's possible for this to be null, but most will specify a filename
                var fileName = part.FileName;

                var entity = await SentFolder.GetBodyPartAsync(item.UniqueId, part, token);

                var imageInfo = new ImageInfo
                {
                    Name = fileName,
                    DateAjout = DateTimeOffset.Now,
                    DateEmail = item.InternalDate ?? item.Date,
                    UniqueId = item.UniqueId.Id,
                    ExternalOwner = await MapExternalOwnerOnSubject(item.Envelope.From.Mailboxes.First().Address, item.Envelope.Subject, token),
                    ExternalSubOwner = await MapExternalSubOwnerOnText(item, token),
                    Object = item.Envelope.Subject,
                    EmailStatesId = _emailStateDb?.Id
                };

                _logger.LogInformation("New ImageInfo: {ImageInfo}", JsonSerializer.Serialize(imageInfo));

                if (item.InternalDate.HasValue)
                {
                    _startDate = item.InternalDate.Value;
                }
                else
                {
                    _startDate = item.Date;
                }

                _logger.LogInformation("New Start date: {StartDate}", _startDate);

                using (var stream = new MemoryStream())
                {
                    await entity.WriteToAsync(stream, _tokenSource.Token);

                    var imageString = Encoding.ASCII.GetString(stream.ToArray());

                    await ParseAndSaveImageAsync(imageString, imageInfo, token);
                }

                await AnalyseImageAsync(imageInfo, token);

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
            _logger.LogDebug("Attachment details: ContentDescription={ContentDescription}, ContentDisposition={ContentDisposition}, ContentId={ContentId}, ContentLocation={ContentLocation}, ContentMd5={ContentMd5}, ContentTransfertEncoding={ContentTransfertEncoding}, ContentType={ContentType}, FileName={Filename}, IsAttachment={IsAttachment}, Octets={Octets}, PartSpecifier={PartSpecifier}",
                attachment.ContentDescription,
                attachment.ContentDisposition?.ToString(),
                attachment.ContentId,
                attachment.ContentLocation?.ToString(),
                attachment.ContentMd5,
                attachment.ContentTransferEncoding,
                attachment.ContentType?.ToString(),
                attachment.FileName,
                attachment.IsAttachment.ToString(),
                attachment.Octets.ToString(),
                attachment.PartSpecifier);

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
                    UniqueId = item.UniqueId.Id,
                    ExternalOwner = await MapExternalOwnerOnSubject(item.Envelope.From.Mailboxes.First().Address, item.Envelope.Subject, token),
                    ExternalSubOwner = await MapExternalSubOwnerOnText(item, token),
                    Object = item.Envelope.Subject,
                    EmailStatesId = _emailStateDb?.Id
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

                await AnalyseImageAsync(imageInfo, token);

                if (_emailStateDb != null)
                {
                    _emailStateDb.MessagesCount++;

                    _context.Update(_emailStateDb);

                    await _context.SaveChangesAsync(token);
                }
            }
        }
    }

    private async Task AnalyseImageAsync(ImageInfo imageInfo, CancellationToken token)
    {
        if (_config.UseAiBridges())
        {
            var aiBridgesApi = _scoped.ServiceProvider.GetRequiredService<AiBridgesApi>();

            await aiBridgesApi.AnalyzeImageAsync(imageInfo, _observers, token);
        }
        else if (_config.UseFlorence2AI())
        {
            var modelSource = _scoped.ServiceProvider.GetRequiredService<FlorenceModelDownloader>();

            while (!modelSource.IsReady)
            {
                await Task.Delay(5000);
            }

            var modelSession = _scoped.ServiceProvider.GetRequiredService<Florence2Model>();
            var florence2Local = _scoped.ServiceProvider.GetRequiredService<Florence2LocalModel>();

            await florence2Local.AnalyzeImageAsync(modelSession, imageInfo, _observers, token);
        }
        else if (_config.UseAzureVision())
        {
            var azureVision = _scoped.ServiceProvider.GetRequiredService<AzureVisionApi>();

            var client = AzureVisionApi.Authenticate(_loginInfo);

            await azureVision.AnalyzeImageAsync(client, imageInfo, _observers, token);
        }
        else
        {
            throw new NotImplementedException("No AI service is configured.");
        }
    }

    /// <summary>
    /// Use the text of the email to map the external sub owner
    /// </summary>
    /// <param name="item"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<Guid?> MapExternalSubOwnerOnText(IMessageSummary item, CancellationToken token)
    {
        var mapping = await _context.Mappings.Where(m => m.Filter == item.Envelope.From.Mailboxes.First().Address &&
                                                         m.SubFilter != null)
                                             .ToArrayAsync(token);

        var textBody = item.TextBody?.ToString();

        _logger.LogInformation("TextBody: {TextBody}", textBody);

        var map = mapping.FirstOrDefault(m => textBody?.Contains(m.SubFilter ?? "") == true);

        return map?.SubValue;
    }

    private async Task<Guid?> MapExternalOwnerOnSubject(string senderEmail, string subject, CancellationToken token)
    {
        senderEmail = senderEmail.Trim();

        var mapping = _context.Mappings.Where(m => m.Filter == senderEmail);

        var map = await mapping.FirstOrDefaultAsync(m => m.Filter == senderEmail && m.Key == subject, token);

        if (map != null)
        {
            return map.Value;
        }

        return null;
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
                        _logger.LogWarning(e, "Exception when idling: {Message}", e.Message);
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
                _logger.LogWarning(ipEx, "Error in WaitForNewMessagesAsync: {Message}", ipEx.Message);

                // protocol exceptions often result in the client getting disconnected
                await ReconnectAsync();
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning(ioEx, "Error in WaitForNewMessagesAsync: {Message}", ioEx.Message);

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
                _logger.LogInformation("\tOnCountChanged: {Arrived} new messages have arrived.", arrived);

            else
                _logger.LogInformation("\tOnCountChanged: 1 new message has arrived.");

            messagesArrived = true;
            _done?.Cancel();
        }
        else
        {
            _logger.LogWarning("\tOnCountChanged: {Folder}: {Count} messages. Seems like nothing changed...", folder, folder?.Count);
        }
    }

    async void OnMessageExpunged(object? sender, MessageEventArgs e)
    {
        _logger.LogInformation("OnMessageExpundeg: {Folder}: message #{Index} has been expunged.", sender, e.Index);

        var folder = sender as ImapFolder;

        if (e.Index < _emailStateDb?.MessagesCount)
        {
            _emailStateDb.MessagesCount -= 1;

            _context.Update(_emailStateDb);

            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("OnMessageExpundeg: {Folder}: message #{Index} has been expunged.", folder, e.Index);
        }
    }

    void OnMessageFlagsChanged(object? sender, MessageFlagsChangedEventArgs e)
    {
        try
        {
            var folder = sender as ImapFolder;

            _logger.LogInformation("{Folder}: flags have changed for message #{Index} ({Flags}).", folder?.ToString(), e.Index, e.Flags);
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

            if (parseImage && !string.IsNullOrWhiteSpace(lines[i]))
            {
                sb.Append(lines[i]);
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
            _logger.LogInformation("Subscribe {SessionId} successfully", sessionId);
        }
        else
        {
            _logger.LogError("[WRN] Failed to subscribe {SessionId}", sessionId);
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
            _logger.LogInformation("Unsubscribe {SessionId} successfully", sessionId);
        }
        else
        {
            _logger.LogError("[WRN] Failed to unsubscribe {SessionId}", sessionId);
        }

        return this;
    }

    private Task? _idleTaskRef;

    internal void SetCurrentTaskRef(Task idleTask)
    {
        _idleTaskRef = idleTask;
    }

    internal Task? GetCurrentTask()
    {
        return _idleTaskRef;
    }

    public string GetBackgroundTaskStatus()
    {
        var task = GetCurrentTask();
        if (task == null)
        {
            return "No background task";
        }
        else if (task.IsCompleted)
        {
            return "Background task completed";
        }
        else if (task.IsCanceled)
        {
            return "Background task canceled";
        }
        else if (task.IsFaulted)
        {
            return "Background task faulted";
        }
        else
        {
            return "Background task running";
        }
    }
}
