using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace AzureComputerVision
{
    public class AzureImageMLApi
    {
        public static ComputerVisionClient Authenticate(LoginInfo config)
        {
            ComputerVisionClient client = new (new ApiKeyServiceClientCredentials(config.AzureVisionSubscriptionKey))
            {
                Endpoint = config.AzureVisionEndpoint
            };

            return client;
        }

        public static async Task AnalyzeImage(ComputerVisionClient client, string path)
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
                using var stream = File.OpenRead(path);                     
                // Analyze the local image.
                ImageAnalysis results = await client.AnalyzeImageInStreamAsync(stream, visualFeatures: features);

                var jsonResult = JsonSerializer.Serialize(results, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                Console.WriteLine(jsonResult);

                await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path), "info.json"), jsonResult);
            } 
            catch (Exception? e) 
            {
                do {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);

                    e = e.InnerException;
                } while (e != null);
            }
        }
    }
}