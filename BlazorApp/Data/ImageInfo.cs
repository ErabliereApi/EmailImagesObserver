using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp.Data;

[Index(nameof(DateAjout), IsUnique = false, Name = "Index_DateAjout")]
public class ImageInfo
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public string? AzureImageAPIInfo { get; set; }

    public byte[]? Images { get; set; }

    public DateTimeOffset DateAjout { get; set; }

    public DateTimeOffset? DateEmail { get; set; }

    private ImageAnalysis? _imagesAnalysis;

    [JsonIgnore]
    public ImageAnalysis? ImageAnalysis
    {
        get
        {
            if (_imagesAnalysis == null && AzureImageAPIInfo != null)
            {
                try
                {
                    _imagesAnalysis = JsonSerializer.Deserialize<ImageAnalysis>(AzureImageAPIInfo);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            return _imagesAnalysis;
        }
    }
}

public class NullImageInfo : ImageInfo { }