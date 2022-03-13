using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using ComposableAsync;
using RateLimiter;

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

    public AzureImageMLApi(Data.BlazorDbContext context)
    {
        _context = context;
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
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("ANALYZE IMAGE - ATTACHMENT");
        Console.WriteLine(imageInfo);
        Console.WriteLine();

        if (imageInfo.Images == null)
        {
            Console.WriteLine("Image is null, end function AnalyseImageAsync");
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

            // Analyze the local image.
            ImageAnalysis results = await client.AnalyzeImageInStreamAsync(stream, visualFeatures: features);

            var jsonResult = JsonSerializer.Serialize(results);

            imageInfo.AzureImageAPIInfo = jsonResult;

            Console.WriteLine(jsonResult);

            _context.Update(imageInfo);

            await _context.SaveChangesAsync(token);

            if (observer is not null)
            {
                foreach (var value in observer)
                {
                    value.Value.OnNext(imageInfo);
                }
            }
        }
        catch (Exception? e)
        {
            Console.Error.WriteLine(e);
        }
    }
}