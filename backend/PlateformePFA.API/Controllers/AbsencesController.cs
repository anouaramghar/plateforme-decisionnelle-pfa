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

        public AbsencesController(AppDbContext context, IAlerteService alerteService, ILogger<AbsencesController> logger)
        {
            _context       = context;
            _alerteService = alerteService;
            _logger        = logger;
        }

        // GET: api/absences (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Absence>>> GetAbsences(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Absences.AsNoTracking().OrderByDescending(a => a.DateAbsence);
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
            return absence;
        }

        // GET: api/absences/etudiant/5
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsencesByEtudiant(int etudiantId)
        {
            return await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .Where(a => a.EtudiantId == etudiantId)
                .OrderByDescending(a => a.DateAbsence)
                .Take(500)           // safety cap — no student should ever exceed this
                .ToListAsync();
        }

        // GET: api/absences/module/5
        [HttpGet("module/{moduleId}")]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsencesByModule(int moduleId)
        {
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
        public async Task<ActionResult<Absence>> PostAbsence(CreateAbsenceDto dto)
        {
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
            await _context.SaveChangesAsync();

            _logger.LogInformation("Absence enregistrée : EtudiantId={EtudiantId}, ModuleId={ModuleId}, Heures={H}",
                absence.EtudiantId, absence.ModuleId, absence.NombreHeures);

            // Auto-generate alert if cumulative unjustified absences exceed threshold
            await _alerteService.CheckAbsenceAlertAsync(absence.EtudiantId);

            return CreatedAtAction(nameof(GetAbsence), new { id = absence.Id }, absence);
        }

        // PUT: api/absences/5
        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAbsence(int id, UpdateAbsenceDto dto)
        {
            var absence = await _context.Absences.FindAsync(id);
            if (absence == null) return NotFound(new { message = "Absence introuvable." });

            absence.NombreHeures = dto.NombreHeures;
            absence.Justifiee    = dto.Justifiee;
            absence.DateAbsence  = dto.DateAbsence;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Absences.AnyAsync(a => a.Id == id)) return NotFound();
                else throw;
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
            return NoContent();
        }
    }
}
