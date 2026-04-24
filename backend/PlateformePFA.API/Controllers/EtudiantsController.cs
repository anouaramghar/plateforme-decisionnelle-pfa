using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
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

        // 1. GET ALL (paginated)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Etudiant>>> GetEtudiants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            pageSize = Math.Min(pageSize, 100);
            return await _context.Etudiants
                .Include(e => e.Filiere)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // 2. GET ONE
        [HttpGet("{id}")]
        public async Task<ActionResult<Etudiant>> GetEtudiant(int id)
        {
            var etudiant = await _context.Etudiants
                .Include(e => e.Filiere)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (etudiant == null) return NotFound();

            return etudiant;
        }

        // 3. CREATE (POST)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<Etudiant>> PostEtudiant(CreateEtudiantDto dto)
        {
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

            return CreatedAtAction(nameof(GetEtudiant), new { id = etudiant.Id }, etudiant);
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
