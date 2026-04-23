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
    public class AbsencesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AbsencesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Absences
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Absence>>> GetAbsences()
        {
            return await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .ToListAsync();
        }

        // GET: api/Absences/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Absence>> GetAbsence(int id)
        {
            var absence = await _context.Absences
                .Include(a => a.Etudiant)
                .Include(a => a.Module)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (absence == null) return NotFound();
            return absence;
        }

        // POST: api/Absences
        [HttpPost]
        public async Task<ActionResult<Absence>> PostAbsence(Absence absence)
        {
            _context.Absences.Add(absence);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAbsence), new { id = absence.Id }, absence);
        }

        // PUT: api/Absences/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAbsence(int id, Absence absence)
        {
            if (id != absence.Id) return BadRequest();

            _context.Entry(absence).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Absences.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Absences/5
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