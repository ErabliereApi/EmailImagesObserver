using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorApp.Data
{
    public class ImageInfo
    {
        public ImageInfo(DirectoryInfo directoryInfo)
        {
            Directory = directoryInfo;

            Id = int.Parse(Directory.Name);

            CreationTime = Directory.CreationTime;

            var imageFile = Directory.GetFiles("*.jpg").FirstOrDefault();

            if (imageFile != null)
            {
                ImageLink = $"/images/{imageFile.Directory?.Name}/{imageFile.Name}";
            }
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

        public string? ImageLink { get; }
    }
}
