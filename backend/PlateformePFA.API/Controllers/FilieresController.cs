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
    public class FilieresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilieresController(AppDbContext context)
        {
            _context = context;
        }

        // 1. READ ALL (GET) : Récupérer toutes les filières
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Filiere>>> GetFilieres()
        {
            // Include permet de joindre la table Utilisateur pour avoir les infos du Responsable
            return await _context.Filieres
                .Include(f => f.Responsable) 
                .ToListAsync();
        }

        // 2. READ ONE (GET) : Récupérer une seule filière par son ID
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

        // 3. CREATE (POST) : Ajouter une nouvelle filière
        [HttpPost]
        public async Task<ActionResult<Filiere>> PostFiliere(Filiere filiere)
        {
            _context.Filieres.Add(filiere);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFiliere), new { id = filiere.Id }, filiere);
        }

        // 4. UPDATE (PUT) : Modifier une filière existante
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFiliere(int id, Filiere filiere)
        {
            if (id != filiere.Id)
            {
                return BadRequest(new { message = "L'ID de l'URL ne correspond pas à l'ID de la filière." });
            }

            _context.Entry(filiere).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FiliereExists(id))
                {
                    return NotFound(new { message = "Filière introuvable." });
                }
                else
                {
                    throw;
                }
            }

            return NoContent(); // 204 No Content = Succès mais rien à renvoyer
        }

        // 5. DELETE : Supprimer une filière
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

        // Méthode utilitaire privée
        private bool FiliereExists(int id)
        {
            return _context.Filieres.Any(e => e.Id == id);
        }
    }
}