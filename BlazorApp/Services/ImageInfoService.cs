using AzureComputerVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorApp.Services
{
    public class ImageInfoService
    {
        public Task<IEnumerable<ImageInfo>> GetImageInfo(int? take, int? skip = 0, string? search = null)
        {
            return Task.Run(() =>
            {
                Console.WriteLine("Loading data from directory...");

                var directory = new DirectoryInfo(Constant.GetBaseDirectory());

                IEnumerable<ImageInfo> infoArray = directory.EnumerateDirectories()
                                                            .Where(d => d.GetFiles().Any(f => f.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                                                              f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                                                            .Select(d => new ImageInfo(d))
                                                            .OrderByDescending(i => i.Id);

                if (string.IsNullOrWhiteSpace(search) == false)
                {
                    infoArray = infoArray.Where(d => search.Split(' ').All(w => d.ImageInfoAsFormatedJson?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
                }

                if (skip.HasValue)
                {
                    infoArray = infoArray.Skip(skip.Value);
                }

                if (take.HasValue)
                {
                    infoArray = infoArray.Take(take.Value);
                }

                return infoArray;
            });
        }

        public ImageInfo? GetImageInfo(string? id)
        {
            if (id is null) return null;

            Console.WriteLine($"Loading {id} from directory...");

            var path = Path.Combine(Constant.GetBaseDirectory(), id.ToString());

            if (Directory.Exists(path) == false)
            {
                return null;
            }

            return new ImageInfo(new DirectoryInfo(path));
        }

        public void DeleteImageInfo(int id)
        {
            var filesLocation = Path.Combine(Constant.GetBaseDirectory(), id.ToString());

            foreach (var file in Directory.GetFiles(filesLocation))
            {
                File.Delete(file);
            }

            Console.WriteLine("Image info id " + id + " is deleted");
        }
    }
}
