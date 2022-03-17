using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp.Data;

[Index(nameof(DateAjout), IsUnique = false, Name = "Index_DateAjout")]
[Index(nameof(UniqueId), IsUnique = true, Name = "Index_UniqueId")]
public class ImageInfo : IComparable<ImageInfo>
{
    public long Id { get; set; }

    public uint UniqueId { get; set; }

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

    public int CompareTo(ImageInfo? other)
    {
        if (DateEmail == null)
        {
            return other?.DateEmail == null ? 0 : -1;
        }

        if (other == null)
        {
            return 1;
        }

        return DateEmail.Value.CompareTo(other.DateEmail.GetValueOrDefault(default)) * -1;
    }

    public override bool Equals(object? obj)
    {
        return obj is ImageInfo img && img.Id == Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Id} {DateEmail} {Name}";
    }
}

public class NullImageInfo : ImageInfo { }