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
    public class NotesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotesController> _logger;

        public NotesController(AppDbContext context, ILogger<NotesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Note>>> GetNotes()
        {
            return await _context.Notes
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Note>> GetNote(int id)
        {
            var note = await _context.Notes
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (note == null) return NotFound();

            return note;
        }

        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Note>>> GetNotesByEtudiant(int etudiantId)
        {
            return await _context.Notes
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .Where(n => n.EtudiantId == etudiantId)
                .ToListAsync();
        }

        [HttpGet("module/{moduleId}")]
        public async Task<ActionResult<IEnumerable<Note>>> GetNotesByModule(int moduleId)
        {
            return await _context.Notes
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .Where(n => n.ModuleId == moduleId)
                .ToListAsync();
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPost]
        public async Task<ActionResult<Note>> PostNote(Note note)
        {
            _context.Notes.Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Note créée : Id={NoteId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}", note.Id, note.EtudiantId, note.ModuleId);

            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutNote(int id, Note note)
        {
            if (id != note.Id) return BadRequest();

            _context.Entry(note).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Notes.AnyAsync(n => n.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Note supprimée : Id={NoteId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}", note.Id, note.EtudiantId, note.ModuleId);

            return NoContent();
        }
    }
}
