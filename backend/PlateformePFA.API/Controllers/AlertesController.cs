using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/[controller]")]
    [ApiController]
    public class AlertesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AlertesController> _logger;

        public AlertesController(AppDbContext context, ILogger<AlertesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/alertes?statut=NonResolue
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alerte>>> GetAlertes([FromQuery] string? statut = null)
        {
            var query = _context.Alertes.Include(a => a.Etudiant).AsQueryable();

            if (!string.IsNullOrEmpty(statut))
                query = query.Where(a => a.Statut == statut);

            return await query.OrderByDescending(a => a.DateGeneration).ToListAsync();
        }

        // GET: api/alertes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Alerte>> GetAlerte(int id)
        {
            var alerte = await _context.Alertes
                .Include(a => a.Etudiant)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            return alerte;
        }

        // GET: api/alertes/etudiant/5
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Alerte>>> GetAlertesByEtudiant(int etudiantId)
        {
            return await _context.Alertes
                .Include(a => a.Etudiant)
                .Where(a => a.EtudiantId == etudiantId)
                .OrderByDescending(a => a.DateGeneration)
                .ToListAsync();
        }

        // POST: api/alertes
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Alerte>> PostAlerte(Alerte alerte)
        {
            alerte.DateGeneration = DateTime.UtcNow;
            alerte.Statut = "NonResolue";

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte créée : Id={AlerteId}, Type={Type}, EtudiantId={EtudiantId}",
                alerte.Id, alerte.TypeAlerte, alerte.EtudiantId);

            return CreatedAtAction(nameof(GetAlerte), new { id = alerte.Id }, alerte);
        }

        // PATCH: api/alertes/5/resoudre
        [HttpPatch("{id}/resoudre")]
        public async Task<IActionResult> ResoudreAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            alerte.Statut = "Resolue";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte résolue : Id={AlerteId}, EtudiantId={EtudiantId}", alerte.Id, alerte.EtudiantId);

            return NoContent();
        }

        // DELETE: api/alertes/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            _context.Alertes.Remove(alerte);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
