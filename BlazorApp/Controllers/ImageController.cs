using BlazorApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ImageController : ControllerBase
    {
        private readonly BlazorDbContext _context;

        public ImageController(BlazorDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetImages([FromQuery] Guid? ownerId, [FromQuery] int? take, [FromQuery] int? skip)
        {
            if (take < 0) {
                return BadRequest("The number of images to retrieve must be greater than 0");
            }

            if (skip < 0) {
                return BadRequest("The number of images to skip must be greater than 0");
            }

            if (take > 10) {
                return BadRequest("The maximum number of images that can be retrieved is 10");
            }

            if (take == null) {
                take = 10;
            }

            if (skip == null) {
                skip = 0;
            }

            return Ok(_context.ImagesInfo.Where(i => i.ExternalOwner == ownerId).Skip(skip.Value).Take(take.Value));
        }
    }
}