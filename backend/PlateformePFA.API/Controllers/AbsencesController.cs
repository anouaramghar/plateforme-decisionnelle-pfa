using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/absences")]
    [ApiController]
    public class AbsencesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AbsencesController> _logger;

        public AbsencesController(AppDbContext context, ILogger<AbsencesController> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // GET: api/absences
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsences()
        {
            return await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .ToListAsync();
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
                .ToListAsync();
        }

        // POST: api/absences
        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPost]
        public async Task<ActionResult<Absence>> PostAbsence(Absence absence)
        {
            absence.CreeLe = DateTime.UtcNow;
            _context.Absences.Add(absence);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Absence enregistrée : EtudiantId={EtudiantId}, ModuleId={ModuleId}, Heures={H}",
                absence.EtudiantId, absence.ModuleId, absence.NombreHeures);

            return CreatedAtAction(nameof(GetAbsence), new { id = absence.Id }, absence);
        }

        // PUT: api/absences/5
        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAbsence(int id, Absence absence)
        {
            if (id != absence.Id) return BadRequest();
            _context.Entry(absence).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Absences.AnyAsync(a => a.Id == id)) return NotFound();
                else throw;
            }
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
