using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Alertes;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
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

        // GET: api/alertes?resolue=false&page=1&pageSize=20
        // Alerts are an institution-wide management view; teachers have no
        // alerts UI, so reads are restricted server-side (defense in depth).
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Alerte>>> GetAlertes(
            [FromQuery] bool? resolue = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Alertes.AsNoTracking().AsQueryable();
            if (resolue.HasValue) query = query.Where(a => a.Resolue == resolue.Value);

            var ordered = query.OrderByDescending(a => a.CreeLe);
            var total   = await ordered.CountAsync();
            var items   = await ordered
                .Include(a => a.Etudiant)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Alerte>(items, total, page, pageSize);
        }

        // GET: api/alertes/5
        [Authorize(Roles = "Admin,Responsable")]
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
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Alerte>>> GetAlertesByEtudiant(int etudiantId)
        {
            return await _context.Alertes
                .Include(a => a.Etudiant)
                .Where(a => a.EtudiantId == etudiantId)
                .OrderByDescending(a => a.CreeLe)
                .Take(500)           // safety cap — one student's full alert history
                .ToListAsync();
        }

        // POST: api/alertes
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Alerte>> PostAlerte(CreateAlerteDto dto)
        {
            var alerte = new Alerte
            {
                EtudiantId = dto.EtudiantId,
                Type       = dto.Type,
                Niveau     = dto.Niveau,
                Message    = dto.Message,
                Resolue    = false,
                CreeLe     = DateTime.UtcNow
            };

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte créée : Id={Id}, Type={Type}, Niveau={Niveau}, EtudiantId={EtudiantId}",
                alerte.Id, alerte.Type, alerte.Niveau, alerte.EtudiantId);

            return CreatedAtAction(nameof(GetAlerte), new { id = alerte.Id }, alerte);
        }

        // PATCH: api/alertes/5/resoudre
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPatch("{id}/resoudre")]
        public async Task<IActionResult> ResoudreAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            // Audit: capture which Utilisateur clicked the button.
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? resolueeParId = int.TryParse(userIdClaim, out var uid) ? uid : null;

            alerte.Resolue       = true;
            alerte.ResolueeLe    = DateTime.UtcNow;
            alerte.ResolueeParId = resolueeParId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte résolue : Id={Id}, EtudiantId={EtudiantId}, ResolueeParId={UserId}",
                alerte.Id, alerte.EtudiantId, resolueeParId);
            return NoContent();
        }

        // PATCH: api/alertes/batch-resolve
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPatch("batch-resolve")]
        public async Task<IActionResult> BatchResolve([FromBody] BatchResolveRequest request)
        {
            if (request?.Ids == null || request.Ids.Length == 0)
                return BadRequest(new { message = "Aucun ID fourni." });

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? resolueeParId = int.TryParse(userIdClaim, out var uid) ? uid : null;
            var now = DateTime.UtcNow;

            var alertes = await _context.Alertes
                .Where(a => request.Ids.Contains(a.Id) && !a.Resolue)
                .ToListAsync();

            foreach (var alerte in alertes)
            {
                alerte.Resolue       = true;
                alerte.ResolueeLe    = now;
                alerte.ResolueeParId = resolueeParId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Batch resolve: {Count} alertes résolues par userId={UserId}",
                alertes.Count, resolueeParId);

            return Ok(new { resolved = alertes.Count });
        }

        public class BatchResolveRequest
        {
            public int[] Ids { get; set; } = Array.Empty<int>();
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
