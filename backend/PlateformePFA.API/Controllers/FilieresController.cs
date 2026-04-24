using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Filieres;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FilieresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilieresController(AppDbContext context)
        {
            _context = context;
        }

        // 1. READ ALL (paginated)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Filiere>>> GetFilieres(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            pageSize = Math.Min(pageSize, 100);
            return await _context.Filieres
                .Include(f => f.Responsable)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // 2. READ ONE
        [HttpGet("{id}")]
        public async Task<ActionResult<Filiere>> GetFiliere(int id)
        {
            var filiere = await _context.Filieres
                .Include(f => f.Responsable)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (filiere == null)
            {
                return NotFound(new { message = "Filière introuvable." });
            }

            return filiere;
        }

        // 3. CREATE (POST)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<Filiere>> PostFiliere(CreateFiliereDto dto)
        {
            var filiere = new Filiere
            {
                Code          = dto.Code,
                Intitule      = dto.Intitule,
                ResponsableId = dto.ResponsableId
            };

            _context.Filieres.Add(filiere);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFiliere), new { id = filiere.Id }, filiere);
        }

        // 4. UPDATE (PUT)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFiliere(int id, UpdateFiliereDto dto)
        {
            var filiere = await _context.Filieres.FindAsync(id);
            if (filiere == null)
            {
                return NotFound(new { message = "Filière introuvable." });
            }

            filiere.Code          = dto.Code;
            filiere.Intitule      = dto.Intitule;
            filiere.ResponsableId = dto.ResponsableId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Filieres.AnyAsync(e => e.Id == id))
                    return NotFound(new { message = "Filière introuvable." });
                else
                    throw;
            }

            return NoContent();
        }

        // 5. DELETE
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFiliere(int id)
        {
            var filiere = await _context.Filieres.FindAsync(id);
            if (filiere == null)
            {
                return NotFound(new { message = "Filière introuvable." });
            }

            _context.Filieres.Remove(filiere);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
