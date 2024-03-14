using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorApp.Data;
using Microsoft.AspNetCore.Authorization;

namespace BlazorApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MappingController : ControllerBase
    {
        private readonly BlazorDbContext _context;

        public MappingController(BlazorDbContext context)
        {
            _context = context;
        }

        // GET: api/Mapping
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Mapping>>> GetMappings()
        {
            return await _context.Mappings.ToListAsync();
        }

        // GET: api/Mapping/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Mapping>> GetMapping(Guid id)
        {
            var mapping = await _context.Mappings.FindAsync(id);

            if (mapping == null)
            {
                return NotFound();
            }

            return mapping;
        }

        // POST: api/Mapping
        [HttpPost]
        public async Task<ActionResult<Mapping>> CreateMapping(Mapping mapping)
        {
            _context.Mappings.Add(mapping);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMapping), new { id = mapping.Id }, mapping);
        }

        // PUT: api/Mapping/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMapping(Guid id, Mapping mapping)
        {
            if (id != mapping.Id)
            {
                return BadRequest();
            }

            _context.Entry(mapping).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MappingExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/Mapping/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMapping(Guid id)
        {
            var mapping = await _context.Mappings.FindAsync(id);
            if (mapping == null)
            {
                return NotFound();
            }

            _context.Mappings.Remove(mapping);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MappingExists(Guid id)
        {
            return _context.Mappings.Any(e => e.Id == id);
        }
    }
}