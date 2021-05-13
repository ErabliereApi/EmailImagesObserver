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

            CreationTime = Directory.CreationTime;

            var imageFile = Directory.GetFiles().FirstOrDefault(f => f.Name.EndsWith(".jpg"));

            if (imageFile != null)
            {
                ImageLink = $"/images/{imageFile.Directory?.Name}/{imageFile.Name}";
            }
        }

        public int Id => int.Parse(Directory.Name);

        [JsonIgnore]
        public DirectoryInfo Directory { get; }

        public DateTimeOffset CreationTime { get; set; }

        private ImageAnalysis? _imageAnalysis;
        public ImageAnalysis? ImageAnalysis
        {
            get
            {
                if (_imageAnalysis != null) return _imageAnalysis;

                var imageAnalysisFile = Directory.GetFiles().FirstOrDefault(f => f.Name == "info.json");

                if (imageAnalysisFile != null)
                {
                    var analysis = File.ReadAllText(imageAnalysisFile.FullName);

                    _imageAnalysis = JsonSerializer.Deserialize<ImageAnalysis>(analysis);
                }

                return _imageAnalysis;
            }
        }

        public string? ImageLink { get; }
    }
}
