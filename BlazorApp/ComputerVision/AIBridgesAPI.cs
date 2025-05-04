using System.Collections.Concurrent;
using BlazorApp.Data;
using BlazorApp.Notification;

namespace BlazorApp.ComputerVision;

public class AiBridgesApi : AIAlerteService
{
    private readonly ILogger<AiBridgesApi> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly BlazorDbContext _context;

    public AiBridgesApi(BlazorDbContext context, ILogger<AiBridgesApi> logger, AlerteClient alerteClient, IConfiguration config, IHttpClientFactory httpClientFactory)
        : base(context, logger, alerteClient)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _context = context;
    }

    public async Task AnalyzeImageAsync(
        ImageInfo imageInfo,
        ConcurrentDictionary<Guid, IObserver<ImageInfo>>? observer = null,
        CancellationToken token = default)
    {
        _logger.LogInformation("------------- ANALYZE IMAGE - ATTACHMENT ---------------");

        if (imageInfo.Images == null)
        {
            _logger.LogInformation("Image is null, end function AnalyseImageAsync");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("AiBridgesClient");
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/Florence2AI/1.0/{_config.GetValue<string>("USE_FLORENCE2_TASKTYPES")}");
            request.Content = new ByteArrayContent(imageInfo.Images);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            var response = await client.SendAsync(request, token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error calling AiBridges API: {StatusCode}", response.StatusCode);
                return;
            }

            var jsonResult = await response.Content.ReadAsStringAsync(token);

            imageInfo.AzureImageAPIInfo = jsonResult;
            imageInfo.AITypes += "Florence2;";

            _context.Update(imageInfo);

            await _context.SaveChangesAsync(token);

            if (observer is not null)
            {
                foreach (var value in observer)
                {
                    value.Value.OnNext(imageInfo);
                }
            }

            await SendAlerteAsync(imageInfo, jsonResult, token);

            _logger.LogInformation("Analyzed image with ID: {ImageId}", imageInfo.Id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error analyzing image in AIBridgesApi: {Message}", e.Message);
        }
    }
}
