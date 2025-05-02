using System.Collections.Concurrent;
using System.Text.Json;
using ComposableAsync;
using RateLimiter;
using BlazorApp.Data;
using Azure.AI.Vision.ImageAnalysis;
using Azure;
using BlazorApp.Model;
using BlazorApp.Notification;

namespace BlazorApp.AzureComputerVision;

/// <summary>
/// AzureVision serivce using the Azure.AI.Vision.ImageAnalysis nuget package.
/// Developpement started in march 2025
/// </summary>
public class AzureVisionApi : AIAlerteService
{
    static readonly CountByIntervalAwaitableConstraint minuteTimeConstraint = new(20, TimeSpan.FromMinutes(1));
    static readonly CountByIntervalAwaitableConstraint daysTimeConstraint = new(5000, TimeSpan.FromDays(1));
    static readonly TimeLimiter azureFreeTimeConstraint = TimeLimiter.Compose(minuteTimeConstraint, daysTimeConstraint);
    private readonly BlazorDbContext _context;
    private readonly ILogger<AzureVisionApi> _logger;
    private readonly IConfiguration _config;

    /// <summary>
    /// Get an authenticated <see cref="ComputerVisionClient" />
    /// </summary>
    public static ImageAnalysisClient Authenticate(LoginInfo loginInfo)
    {
        var client = new ImageAnalysisClient(
            new Uri(loginInfo.AzureVisionEndpoint ?? throw new InvalidOperationException("Please set config.AzureVisionEnoint before trying to initialize a ImageAnalysisClient")),
            new AzureKeyCredential(loginInfo.AzureVisionSubscriptionKey ?? throw new InvalidOperationException("Please set config.AzureVisionSubscriptionKey before trying to initialize a ImageAnalysisClient")));
        
        return client;
    }

    public AzureVisionApi(BlazorDbContext context, ILogger<AzureVisionApi> logger, AlerteClient alerteClient, IConfiguration config) :
        base(context, logger, alerteClient)
    {
        _context = context;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Analyse an image giving and authenticated client and the images itself
    /// </summary>
    /// <param name="client"></param>
    /// <param name="imageInfo"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    public async Task AnalyzeImageAsync(
        ImageAnalysisClient client, 
        Data.ImageInfo imageInfo, 
        ConcurrentDictionary<Guid, IObserver<Data.ImageInfo>>? observer = null, 
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
            await azureFreeTimeConstraint;

            var binaryImage = BinaryData.FromBytes(imageInfo.Images);
            var results = await client.AnalyzeAsync(
                binaryImage,
                visualFeatures: GetVisualFeatureConfigure(),
                cancellationToken: token);

            var jsonResult = JsonSerializer.Serialize(results.Value);

            imageInfo.AzureImageAPIInfo = jsonResult;
            imageInfo.AITypes += "AzureImageMLV2;";

            _logger.LogInformation(jsonResult);

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
        }
        catch (Exception? e)
        {
            _logger.LogError(e, "Error in AzureImageMLApi: {Message}", e.Message);
        }
    }

    private VisualFeatures GetVisualFeatureConfigure()
    {
        var tasks = VisualFeatures.None;

        try
        {
            var configTypes = _config.GetValue<string>("USE_AZURE_VISION_TASKTYPES")?.Split(',');

            if (configTypes != null)
            {
                foreach (var t in configTypes)
                {
                    if (Enum.TryParse<VisualFeatures>(t, out var result))
                    {
                        tasks |= result;
                    }
                }
            }
            else
            {
                tasks = VisualFeatures.Tags;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in GetFlorence2TaskTypesConfigure: {Message}", e.Message);
            tasks = VisualFeatures.Tags;
        }

        return tasks;
    }
}
