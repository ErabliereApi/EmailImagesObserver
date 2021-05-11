using AzureComputerVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorApp.Data
{
    public class ImageInfoService
    {
        public Task<List<ImageInfo>> GetImageInfo()
        {
            var directory = new DirectoryInfo(Constant.GetBaseDirectory());

            var infoArray = directory.GetDirectories().Select(d => new ImageInfo(d)).OrderByDescending(i => i.Id).ToList();

            return Task.FromResult(infoArray);
        }

        public void DeleteImageInfo(int id)
        {
            Directory.Delete(Path.Combine(Constant.GetBaseDirectory(), id.ToString()), true);
            Console.WriteLine("Image info id " + id + " is deleted");
        }
    }
}
