using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
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

        // 1. GET ALL
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Etudiant>>> GetEtudiants()
        {
            // On inclut la Filière pour que le Frontend puisse afficher "Génie Informatique" au lieu de juste "1"
            return await _context.Etudiants
                .Include(e => e.Filiere)
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
        [HttpPost]
        public async Task<ActionResult<Etudiant>> PostEtudiant(Etudiant etudiant)
        {
            _context.Etudiants.Add(etudiant);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEtudiant), new { id = etudiant.Id }, etudiant);
        }

        // 4. UPDATE (PUT)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEtudiant(int id, Etudiant etudiant)
        {
            if (id != etudiant.Id) return BadRequest();

            _context.Entry(etudiant).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Etudiants.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 5. DELETE
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