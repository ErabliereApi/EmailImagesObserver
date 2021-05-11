using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BlazorApp.Data
{
    public class ImageInfo
    {
        public ImageInfo(DirectoryInfo directoryInfo)
        {
            Directory = directoryInfo;

            var imageAnalysisFile = Directory.GetFiles().FirstOrDefault(f => f.Name == "info.json");

            if (imageAnalysisFile != null)
            {
                var analysis = File.ReadAllText(imageAnalysisFile.FullName);

                ImageAnalysis = JsonSerializer.Deserialize<ImageAnalysis>(analysis);
            }

            var imageFile = Directory.GetFiles().FirstOrDefault(f => f.Name.EndsWith(".jpg"));

            if (imageFile != null)
            {
                ImageLink = $"/images/{imageFile.Directory?.Name}/{imageFile.Name}";
            }
        }

        public int Id => int.Parse(Directory.Name);

        public DirectoryInfo Directory { get; }

        public ImageAnalysis? ImageAnalysis { get; }

        public string? ImageLink { get; }
    }
}
