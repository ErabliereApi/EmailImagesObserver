using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureComputerVision
{
    public class ImageInfo
    {
        public ImageInfo(DirectoryInfo directoryInfo)
        {
            Directory = directoryInfo;

            Id = int.Parse(Directory.Name);

            CreationTime = Directory.CreationTime;
        }

        public int Id { get; }

        [JsonIgnore]
        public DirectoryInfo Directory { get; }

        private string? _imageInfoAsFormatedJson;
        [JsonIgnore]
        public string? ImageInfoAsFormatedJson 
        {
            get
            {
                if (_imageInfoAsFormatedJson != null) return _imageInfoAsFormatedJson;

                var imageAnalysisFile = Directory.GetFiles().FirstOrDefault(f => f.Name == "info.json");

                if (imageAnalysisFile != null)
                {
                    _imageInfoAsFormatedJson = File.ReadAllText(imageAnalysisFile.FullName);
                }

                return _imageInfoAsFormatedJson;
            }
        }

        public DateTimeOffset CreationTime { get; }

        private ImageAnalysis? _imageAnalysis;
        public ImageAnalysis? ImageAnalysis
        {
            get
            {
                if (_imageAnalysis != null) return _imageAnalysis;

                if (ImageInfoAsFormatedJson != null)
                {
                    _imageAnalysis = JsonSerializer.Deserialize<ImageAnalysis>(ImageInfoAsFormatedJson);
                }

                return _imageAnalysis;
            }
        }

        private string? _imageLink;
        public string? ImageLink
        {
            get
            {
                if (_imageLink != null) return _imageLink;

                var imageFile = Directory.EnumerateFiles().Where(f => f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                                                      f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                                         .FirstOrDefault();

                if (imageFile != null)
                {
                    _imageLink = $"/images/{imageFile.Directory?.Name}/{imageFile.Name}";
                }

                return _imageLink;
            }
        }
    }
}
