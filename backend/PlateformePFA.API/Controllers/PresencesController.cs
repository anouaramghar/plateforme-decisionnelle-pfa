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
    public class PresencesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PresencesController> _logger;

        public PresencesController(AppDbContext context, ILogger<PresencesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Presence>>> GetPresences()
        {
            return await _context.Presences
                .Include(p => p.Etudiant)
                .Include(p => p.Module)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Presence>> GetPresence(int id)
        {
            var presence = await _context.Presences
                .Include(p => p.Etudiant)
                .Include(p => p.Module)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (presence == null) return NotFound();

            return presence;
        }

        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Presence>>> GetPresencesByEtudiant(int etudiantId)
        {
            return await _context.Presences
                .Include(p => p.Etudiant)
                .Include(p => p.Module)
                .Where(p => p.EtudiantId == etudiantId)
                .ToListAsync();
        }

        [HttpGet("module/{moduleId}")]
        public async Task<ActionResult<IEnumerable<Presence>>> GetPresencesByModule(int moduleId)
        {
            return await _context.Presences
                .Include(p => p.Etudiant)
                .Include(p => p.Module)
                .Where(p => p.ModuleId == moduleId)
                .ToListAsync();
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPost]
        public async Task<ActionResult<Presence>> PostPresence(Presence presence)
        {
            _context.Presences.Add(presence);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Présence créée : Id={PresenceId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}", presence.Id, presence.EtudiantId, presence.ModuleId);

            return CreatedAtAction(nameof(GetPresence), new { id = presence.Id }, presence);
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPresence(int id, Presence presence)
        {
            if (id != presence.Id) return BadRequest();

            _context.Entry(presence).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Presences.Any(p => p.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePresence(int id)
        {
            var presence = await _context.Presences.FindAsync(id);
            if (presence == null) return NotFound();

            _context.Presences.Remove(presence);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Présence supprimée : Id={PresenceId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}", presence.Id, presence.EtudiantId, presence.ModuleId);

            return NoContent();
        }
    }
}
