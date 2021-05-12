using AzureComputerVision;
using BlazorApp.Data;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorApp.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageInfoController : ControllerBase
    {
        [EnableQuery(PageSize = 15)]
        public async Task<IEnumerable<ImageInfo>> GetImageInfo()
        {
            return await Task.Run(() =>
            {
                var directory = new DirectoryInfo(Constant.GetBaseDirectory());

                var infoArray = directory.GetDirectories().Select(d => new ImageInfo(d)).OrderByDescending(i => i.Id);

                return infoArray;
            });
        }
    }
}
