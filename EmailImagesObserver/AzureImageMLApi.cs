using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using ComposableAsync;
using RateLimiter;

namespace AzureComputerVision;

public class AzureImageMLApi
{
    static readonly CountByIntervalAwaitableConstraint minuteTimeConstraint = new(20, TimeSpan.FromMinutes(1));
    static readonly CountByIntervalAwaitableConstraint daysTimeConstraint = new(5000, TimeSpan.FromDays(1));
    static readonly TimeLimiter azureFreeTimeConstraint = TimeLimiter.Compose(minuteTimeConstraint, daysTimeConstraint);

    public static ComputerVisionClient Authenticate(LoginInfo config)
    {
        ComputerVisionClient client = new(new ApiKeyServiceClientCredentials(config.AzureVisionSubscriptionKey))
        {
            Endpoint = config.AzureVisionEndpoint
        };

        return client;
    }

    public static async Task AnalyzeImage(ComputerVisionClient client, string path, ConcurrentDictionary<Guid, IObserver<ImageInfo>>? observer = null)
    {
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("ANALYZE IMAGE - ATTACHMENT");
        Console.WriteLine(path);
        Console.WriteLine();

        // Creating a list that defines the features to be extracted from the image. 
        var features = new List<VisualFeatureTypes?>
            {
                VisualFeatureTypes.Categories,
                VisualFeatureTypes.Description,
                VisualFeatureTypes.Faces,
                VisualFeatureTypes.ImageType,
                VisualFeatureTypes.Tags,
                VisualFeatureTypes.Adult,
                VisualFeatureTypes.Color,
                VisualFeatureTypes.Brands,
                VisualFeatureTypes.Objects,
            };

        try
        {
            await azureFreeTimeConstraint;

            using var stream = File.OpenRead(path);

            // Analyze the local image.
            ImageAnalysis results = await client.AnalyzeImageInStreamAsync(stream, visualFeatures: features);

            var jsonResult = JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Console.WriteLine(jsonResult);

            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path), "info.json"), jsonResult);

            if (observer is not null)
            {
                var imageInfo = new ImageInfo(new DirectoryInfo(Path.Combine(Constant.GetBaseDirectory(), Path.GetDirectoryName(path))));

                foreach (var value in observer)
                {
                    value.Value.OnNext(imageInfo);
                }
            }
        }
        catch (Exception? e)
        {
            do
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);

                e = e.InnerException;
            } while (e != null);
        }
    }
}
