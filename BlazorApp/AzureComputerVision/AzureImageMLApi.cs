using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using ComposableAsync;
using RateLimiter;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp.AzureComputerVision;

/// <summary>
/// Helper class for AzureImageMLApi
/// </summary>
public class AzureImageMLApi
{
    static readonly CountByIntervalAwaitableConstraint minuteTimeConstraint = new(20, TimeSpan.FromMinutes(1));
    static readonly CountByIntervalAwaitableConstraint daysTimeConstraint = new(5000, TimeSpan.FromDays(1));
    static readonly TimeLimiter azureFreeTimeConstraint = TimeLimiter.Compose(minuteTimeConstraint, daysTimeConstraint);
    private readonly Data.BlazorDbContext _context;
    private readonly ILogger<AzureImageMLApi> _logger;
    private readonly AlerteClient alerteClient;

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

    public AzureImageMLApi(Data.BlazorDbContext context, ILogger<AzureImageMLApi> logger, AlerteClient alerteClient)
    {
        _context = context;
        _logger = logger;
        this.alerteClient = alerteClient;
    }

    /// <summary>
    /// Analyse an image giving and authenticated client and the images itself
    /// </summary>
    /// <param name="client"></param>
    /// <param name="imageInfo"></param>
    /// <param name="observer"></param>
    /// <returns></returns>
    public async Task AnalyzeImageAsync(ComputerVisionClient client, Data.ImageInfo imageInfo, ConcurrentDictionary<Guid, IObserver<Data.ImageInfo>>? observer = null, CancellationToken token = default)
    {
        _logger.LogInformation("----------------------------------------------------------");
        _logger.LogInformation("ANALYZE IMAGE - ATTACHMENT");
        _logger.LogInformation(imageInfo.ToString());
        _logger.LogInformation("");

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
            _logger.LogError(e, "Error in AzureImageMLApi: {message}", e.Message);
        }
    }

    private async Task SendAlerteAsync(ImageInfo imageInfo, string jsonResult, CancellationToken token)
    {
        var alertes = await _context.Alertes
            .Where(a => a.ExternalOwnerId == null || a.ExternalOwnerId == imageInfo.ExternalOwner.ToString())
            .ToArrayAsync(token);

        var anyAlerte = false;

        foreach (var alerte in alertes)
        {
            if (alerte.Keywords == null)
            {
                continue;
            }

            var searchJson = jsonResult;

            // first remove the RemoveKeywords for the json result
            if (alerte.RemoveKeywords != null)
            {
                var removeKeywords = alerte.RemoveKeywords.Split(';');

                foreach (var removeKeyword in removeKeywords)
                {
                    searchJson = searchJson.Replace(removeKeyword, string.Empty);
                }
            }

            var keywords = alerte.Keywords.Split(';');

            foreach (var keyword in keywords)
            {
                if (searchJson.Contains(keyword))
                {
                    await alerteClient.SendAlertAsync(alerte, imageInfo, token);
                    anyAlerte = true;
                }
            }
        }

        if (anyAlerte)
        {
            _logger.LogInformation("Alerte was sent");
        }
        else
        {
            _logger.LogInformation("No alerte was sent");
        }
    }
}
