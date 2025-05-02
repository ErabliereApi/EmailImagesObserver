using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using ComposableAsync;
using RateLimiter;
using BlazorApp.Data;
using BlazorApp.Model;
using BlazorApp.Notification;

namespace BlazorApp.AzureComputerVision;

/// <summary>
/// AzureVision serivce using the Microsoft.Azure.CognitiveServices.Vision.ComputerVision nuget package.
/// Developpement started in march 2025
/// </summary>
public class AzureImageMLApi : AIAlerteService
{
    static readonly CountByIntervalAwaitableConstraint minuteTimeConstraint = new(20, TimeSpan.FromMinutes(1));
    static readonly CountByIntervalAwaitableConstraint daysTimeConstraint = new(5000, TimeSpan.FromDays(1));
    static readonly TimeLimiter azureFreeTimeConstraint = TimeLimiter.Compose(minuteTimeConstraint, daysTimeConstraint);
    private readonly BlazorDbContext _context;
    private readonly ILogger<AzureImageMLApi> _logger;

    /// <summary>
    /// Get an authenticated <see cref="ComputerVisionClient" />
    /// </summary>
    public static ComputerVisionClient Authenticate(LoginInfo config)
    {
        ComputerVisionClient client = new(new ApiKeyServiceClientCredentials(config.AzureVisionSubscriptionKey))
        {
            Endpoint = config.AzureVisionEndpoint
        };

        return client;
    }

    public AzureImageMLApi(BlazorDbContext context, ILogger<AzureImageMLApi> logger, AlerteClient alerteClient) :
        base(context, logger, alerteClient)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Analyse an image giving and authenticated client and the images itself
    /// </summary>
    /// <param name="client"></param>
    /// <param name="imageInfo"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    public async Task AnalyzeImageAsync(
        ComputerVisionClient client, 
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

        var features = new List<VisualFeatureTypes?>
        {
            VisualFeatureTypes.Categories,
            VisualFeatureTypes.Description,
            VisualFeatureTypes.Faces,
            VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Tags,
            VisualFeatureTypes.Color,
            VisualFeatureTypes.Brands,
            VisualFeatureTypes.Objects,
        };

        try
        {
            await azureFreeTimeConstraint;

            using var stream = new MemoryStream(imageInfo.Images);

            ImageAnalysis results = await client.AnalyzeImageInStreamAsync(
                stream,
                visualFeatures: features,
                cancellationToken: token);

            var jsonResult = JsonSerializer.Serialize(results);

            imageInfo.AzureImageAPIInfo = jsonResult;
            imageInfo.AITypes += "AzureImageML;";

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
}