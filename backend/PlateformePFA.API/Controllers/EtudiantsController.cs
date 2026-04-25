using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Etudiants;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EtudiantsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EtudiantsController(AppDbContext context)
        {
            _context = context;
        }

        // Project Etudiant + Filiere into a flat DTO once. Used by both list and detail.
        private static readonly System.Linq.Expressions.Expression<Func<Etudiant, EtudiantDto>>
            ToDto = e => new EtudiantDto
            {
                Id              = e.Id,
                Matricule       = e.Matricule,
                Nom             = e.Nom,
                Prenom          = e.Prenom,
                Email           = e.Email,
                FiliereId       = e.FiliereId,
                FiliereCode     = e.Filiere != null ? e.Filiere.Code     : string.Empty,
                FiliereIntitule = e.Filiere != null ? e.Filiere.Intitule : string.Empty,
                Niveau          = e.Niveau,
                Annee           = e.Annee,
            };

        // 1. GET ALL (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<EtudiantDto>>> GetEtudiants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Etudiants.AsNoTracking().OrderBy(e => e.Id);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToListAsync();

            return new PaginatedResult<EtudiantDto>(items, total, page, pageSize);
        }

        // 2. GET ONE
        [HttpGet("{id}")]
        public async Task<ActionResult<EtudiantDto>> GetEtudiant(int id)
        {
            var dto = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();
            return dto;
        }

        // 3. CREATE (POST)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<EtudiantDto>> PostEtudiant(CreateEtudiantDto dto)
        {
            // Reject up front rather than waiting for SQL to throw a UNIQUE violation.
            if (await _context.Etudiants.AnyAsync(e => e.Matricule == dto.Matricule))
                return Conflict(new { message = $"Matricule '{dto.Matricule}' déjà utilisé." });

            if (!await _context.Filieres.AnyAsync(f => f.Id == dto.FiliereId))
                return BadRequest(new { message = $"FiliereId {dto.FiliereId} introuvable." });

            var etudiant = new Etudiant
            {
                Matricule = dto.Matricule,
                Nom       = dto.Nom,
                Prenom    = dto.Prenom,
                Email     = dto.Email,
                FiliereId = dto.FiliereId,
                Niveau    = dto.Niveau,
                Annee     = dto.Annee,
                CreeLe    = DateTime.UtcNow
            };

            _context.Etudiants.Add(etudiant);
            await _context.SaveChangesAsync();

            // Reload with the joined Filiere so the response is shape-consistent with GET.
            var created = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.Id == etudiant.Id)
                .Select(ToDto)
                .FirstAsync();

            return CreatedAtAction(nameof(GetEtudiant), new { id = etudiant.Id }, created);
        }

        // 4. UPDATE (PUT)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEtudiant(int id, UpdateEtudiantDto dto)
        {
            var etudiant = await _context.Etudiants.FindAsync(id);
            if (etudiant == null) return NotFound();

            etudiant.Matricule = dto.Matricule;
            etudiant.Nom       = dto.Nom;
            etudiant.Prenom    = dto.Prenom;
            etudiant.Email     = dto.Email;
            etudiant.FiliereId = dto.FiliereId;
            etudiant.Niveau    = dto.Niveau;
            etudiant.Annee     = dto.Annee;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Etudiants.AnyAsync(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 5. DELETE
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEtudiant(int id)
        {
            var etudiant = await _context.Etudiants.FindAsync(id);
            if (etudiant == null) return NotFound();

            _context.Etudiants.Remove(etudiant);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
