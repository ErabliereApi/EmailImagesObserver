using BlazorApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [EnableUsecureController]
    public class ImageController : ControllerBase
    {
        private readonly BlazorDbContext _context;

        public ImageController(BlazorDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetImages(
            [FromQuery] Guid? ownerId, 
            [FromQuery] int? take, 
            [FromQuery] int? skip,
            [FromQuery] string? search)
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

            var baseQuery = _context.ImagesInfo.Where(i => i.ExternalOwner == ownerId);

            if (!string.IsNullOrWhiteSpace(search)) {
                baseQuery = baseQuery.Where(i => (i.AzureImageAPIInfo != null && 
                                                 i.AzureImageAPIInfo.Contains(search)) ||
                                                 i.DateAjout.ToString().Contains(search));
            }

            return Ok(baseQuery.OrderByDescending(i => i.DateEmail)
                               .Skip(skip.Value)
                               .Take(take.Value));
        }

        [HttpPost]
        public async Task<IActionResult> PostImage([FromBody] ImageInfo postImage, CancellationToken token)
        {
            await _context.ImagesInfo.AddAsync(postImage, token);
            
            await _context.SaveChangesAsync(token);

            return Ok(postImage);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchImage(long id, [FromBody] ImageInfo patch, CancellationToken token)
        {
            var image = await _context.ImagesInfo.FirstOrDefaultAsync(i => i.Id == id, token);

            if (image == null) {
                return NotFound();
            }

            if (patch.ExternalOwner.HasValue) {
                image.ExternalOwner = patch.ExternalOwner;
            }
            
            await _context.SaveChangesAsync(token);

            return Ok(image);
        }
    }
}