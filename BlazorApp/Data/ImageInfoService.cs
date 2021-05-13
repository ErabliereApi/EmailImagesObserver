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
        public async Task<IEnumerable<ImageInfo>> GetImageInfo()
        {
            return await Task.Run(() =>
            {
                Console.WriteLine("Loading data from directory...");

                var directory = new DirectoryInfo(Constant.GetBaseDirectory());

                var infoArray = directory.GetDirectories().Select(d => new ImageInfo(d)).OrderByDescending(i => i.Id);

                return infoArray;
            });
        }

        public void DeleteImageInfo(int id)
        {
            Directory.Delete(Path.Combine(Constant.GetBaseDirectory(), id.ToString()), true);
            Console.WriteLine("Image info id " + id + " is deleted");
        }
    }
}
