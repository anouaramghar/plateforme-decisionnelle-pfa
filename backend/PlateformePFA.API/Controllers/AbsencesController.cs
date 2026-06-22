using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Absences;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/absences")]
    [ApiController]
    public class AbsencesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAlerteService _alerteService;
        private readonly ILogger<AbsencesController> _logger;
        private readonly IAcademicAccessService _academicAccess;

        public AbsencesController(AppDbContext context, IAlerteService alerteService, ILogger<AbsencesController> logger, IAcademicAccessService academicAccess)
        {
            _context       = context;
            _alerteService = alerteService;
            _logger        = logger;
            _academicAccess = academicAccess;
        }

        // GET: api/absences (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Absence>>> GetAbsences(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var assignedModuleId = await _academicAccess.GetAssignedModuleIdAsync(User);
            var query = _context.Absences.AsNoTracking();
            if (assignedModuleId.HasValue)
            {
                query = query.Where(a => a.ModuleId == assignedModuleId.Value);
            }
            query = query.OrderByDescending(a => a.DateAbsence);
            var total = await query.CountAsync();
            var items = await query
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Absence>(items, total, page, pageSize);
        }

        // GET: api/absences/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Absence>> GetAbsence(int id)
        {
            var absence = await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (absence == null) return NotFound(new { message = "Absence introuvable." });

            if (!await _academicAccess.CanAccessModuleAsync(User, absence.ModuleId))
                return Forbid();

            return absence;
        }

        // GET: api/absences/etudiant/5
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsencesByEtudiant(int etudiantId)
        {
            var assignedModuleId = await _academicAccess.GetAssignedModuleIdAsync(User);
            var query = _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .Where(a => a.EtudiantId == etudiantId);

            if (assignedModuleId.HasValue)
            {
                query = query.Where(a => a.ModuleId == assignedModuleId.Value);
            }

            return await query
                .OrderByDescending(a => a.DateAbsence)
                .Take(500)           // safety cap — no student should ever exceed this
                .ToListAsync();
        }

        // GET: api/absences/module/5
        [HttpGet("module/{moduleId}")]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsencesByModule(int moduleId)
        {
            if (!await _academicAccess.CanAccessModuleAsync(User, moduleId))
                return Forbid();

            return await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .Where(a => a.ModuleId == moduleId)
                .OrderByDescending(a => a.DateAbsence)
                .Take(500)           // safety cap — one module's absence rows
                .ToListAsync();
        }

        // POST: api/absences
        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPost]
        public async Task<ActionResult<Absence>> PostAbsence(CreateAbsenceDto dto, CancellationToken ct = default)
        {
            // Validate FKs up front so a bad reference returns 404 instead of a 500.
            if (!await _context.Etudiants.AnyAsync(e => e.Id == dto.EtudiantId && e.DesinscritLe == null, ct))
                return NotFound(new { message = "Etudiant actif introuvable." });
            if (!await _context.Modules.AnyAsync(m => m.Id == dto.ModuleId, ct))
                return NotFound(new { message = "Module introuvable." });

            var compatible = await _context.Etudiants
                .Where(e => e.Id == dto.EtudiantId && e.DesinscritLe == null)
                .Join(_context.Modules.Where(m => m.Id == dto.ModuleId),
                      e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
                .AnyAsync(x => x.e.Niveau == x.m.Niveau, ct);
            if (!compatible)
                return BadRequest(new { message = "L'étudiant et le module doivent appartenir à la même filière et au même niveau." });

            if (!await _academicAccess.CanAccessModuleAsync(User, dto.ModuleId))
                return Forbid();

            var absence = new Absence
            {
                EtudiantId   = dto.EtudiantId,
                ModuleId     = dto.ModuleId,
                NombreHeures = dto.NombreHeures,
                Justifiee    = dto.Justifiee,
                DateAbsence  = dto.DateAbsence,
                CreeLe       = DateTime.UtcNow
            };

            _context.Absences.Add(absence);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Absence enregistrée : EtudiantId={EtudiantId}, ModuleId={ModuleId}, Heures={H}",
                absence.EtudiantId, absence.ModuleId, absence.NombreHeures);

            // Auto-generate alert if cumulative unjustified absences exceed threshold
            await _alerteService.CheckAbsenceAlertAsync(absence.EtudiantId);

            return CreatedAtAction(nameof(GetAbsence), new { id = absence.Id }, absence);
        }

        // PUT: api/absences/5
        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAbsence(int id, UpdateAbsenceDto dto, CancellationToken ct = default)
        {
            var absence = await _context.Absences.FindAsync(new object[] { id }, ct);
            if (absence == null) return NotFound(new { message = "Absence introuvable." });

            var compatible = await _context.Etudiants
                .Where(e => e.Id == absence.EtudiantId && e.DesinscritLe == null)
                .Join(_context.Modules.Where(m => m.Id == absence.ModuleId),
                      e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
                .AnyAsync(x => x.e.Niveau == x.m.Niveau, ct);
            if (!compatible)
                return BadRequest(new { message = "L'étudiant et le module doivent appartenir à la même filière et au même niveau." });

            if (!await _academicAccess.CanAccessModuleAsync(User, absence.ModuleId))
                return Forbid();

            absence.NombreHeures = dto.NombreHeures;
            absence.Justifiee    = dto.Justifiee;
            absence.DateAbsence  = dto.DateAbsence;

            if (!string.IsNullOrEmpty(dto.RowVersion))
            {
                _context.Entry(absence).Property(a => a.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
            }

            try { await _context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { message = "La ressource a été modifiée par un autre utilisateur." });
            }

            // Re-check alert after absence update
            await _alerteService.CheckAbsenceAlertAsync(absence.EtudiantId);

            return NoContent();
        }

        // DELETE: api/absences/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAbsence(int id)
        {
            var absence = await _context.Absences.FindAsync(id);
            if (absence == null) return NotFound();

            _context.Absences.Remove(absence);
            await _context.SaveChangesAsync();

            // Re-evaluate alert status after deleting absence
            await _alerteService.CheckAbsenceAlertAsync(absence.EtudiantId);

            return NoContent();
        }
    }
}
