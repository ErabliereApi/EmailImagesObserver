﻿using Florence2;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp.Data;

[Index(nameof(DateAjout), IsUnique = false, Name = "Index_DateAjout")]
[Index(nameof(UniqueId), IsUnique = true, Name = "Index_UniqueId")]
[Index(nameof(ExternalOwner), IsUnique = false, Name = "Index_ExternalOwner")]
public class ImageInfo : IComparable<ImageInfo>
{
    public long Id { get; set; }

    public uint UniqueId { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }

    /// <summary>
    /// Object from the email of the image
    /// </summary>
    [MaxLength(400)]
    public string? Object { get; set; }

    public string? AzureImageAPIInfo { get; set; }

    public byte[]? Images { get; set; }

    public DateTimeOffset DateAjout { get; set; }

    public DateTimeOffset? DateEmail { get; set; }

    public Guid? EmailStatesId { get; set; }

    public Guid? ExternalOwner { get; set; }

    public Guid? ExternalSubOwner { get; set; }

    public EmailStates? EmailStates { get; set; }

    [MaxLength(255)]
    public string AITypes { get; set; } = string.Empty;


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

    private FlorenceResults[]? _florenceResult;

    [JsonIgnore]
    public FlorenceResults[]? FlorenceResults 
    {
        get
        {
            if (_florenceResult == null && AzureImageAPIInfo != null)
            {
                try
                {
                    _florenceResult = JsonSerializer.Deserialize<FlorenceResults[]>(AzureImageAPIInfo);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            return _florenceResult;
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