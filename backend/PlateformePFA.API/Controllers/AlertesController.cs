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
            _logger  = logger;
        }

        // GET: api/alertes?resolue=false
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alerte>>> GetAlertes([FromQuery] bool? resolue = null)
        {
            var query = _context.Alertes.Include(a => a.Etudiant).AsQueryable();

            if (resolue.HasValue)
                query = query.Where(a => a.Resolue == resolue.Value);

            return await query.OrderByDescending(a => a.CreeLe).ToListAsync();
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
                .OrderByDescending(a => a.CreeLe)
                .ToListAsync();
        }

        // POST: api/alertes
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Alerte>> PostAlerte(Alerte alerte)
        {
            alerte.CreeLe  = DateTime.UtcNow;
            alerte.Resolue = false;

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte créée : Id={Id}, Type={Type}, Niveau={Niveau}, EtudiantId={EtudiantId}",
                alerte.Id, alerte.Type, alerte.Niveau, alerte.EtudiantId);

            return CreatedAtAction(nameof(GetAlerte), new { id = alerte.Id }, alerte);
        }

        // PATCH: api/alertes/5/resoudre
        [HttpPatch("{id}/resoudre")]
        public async Task<IActionResult> ResoudreAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            alerte.Resolue    = true;
            alerte.ResolueeLe = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte résolue : Id={Id}, EtudiantId={EtudiantId}", alerte.Id, alerte.EtudiantId);
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
