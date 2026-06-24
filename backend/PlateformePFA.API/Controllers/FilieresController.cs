using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
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
        public async Task<ActionResult<PaginatedResult<Filiere>>> GetFilieres(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Filieres.AsNoTracking().OrderBy(f => f.Id);
            var total = await query.CountAsync();
            var items = await query
                .Include(f => f.Responsable)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Filiere>(items, total, page, pageSize);
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
            if (dto.ResponsableId.HasValue &&
                !await _context.Utilisateurs.AnyAsync(u => u.Id == dto.ResponsableId.Value && u.Role == "Responsable"))
                return BadRequest(new { message = "ResponsableId doit référencer un utilisateur existant avec le rôle Responsable." });

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

            if (dto.ResponsableId.HasValue &&
                !await _context.Utilisateurs.AnyAsync(u => u.Id == dto.ResponsableId.Value && u.Role == "Responsable"))
                return BadRequest(new { message = "ResponsableId doit référencer un utilisateur existant avec le rôle Responsable." });

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

            // FK guard: deletes are Restrict at the EF model level. Surface a useful
            // 409 instead of letting the SQL FK violation bubble up as a 500.
            var nbEtudiants = await _context.Etudiants.CountAsync(e => e.FiliereId == id);
            var nbModules   = await _context.Modules.CountAsync(m => m.FiliereId == id);
            if (nbEtudiants > 0 || nbModules > 0)
            {
                return Conflict(new
                {
                    message = "Filière référencée — déplacez d'abord les étudiants/modules.",
                    etudiants = nbEtudiants,
                    modules   = nbModules,
                });
            }

            _context.Filieres.Remove(filiere);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
