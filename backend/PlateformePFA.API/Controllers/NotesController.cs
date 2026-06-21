using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Notes;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAlerteService _alerteService;
        private readonly ILogger<NotesController> _logger;

        public NotesController(AppDbContext context, IAlerteService alerteService, ILogger<NotesController> logger)
        {
            _context       = context;
            _alerteService = alerteService;
            _logger        = logger;
        }

        // GET all (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Note>>> GetNotes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Notes.AsNoTracking().OrderByDescending(n => n.CreeLe);
            var total = await query.CountAsync();
            var items = await query
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Note>(items, total, page, pageSize);
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
                .OrderByDescending(n => n.Annee).ThenBy(n => n.Semestre)
                .Take(500)           // safety cap — one student's full note history
                .ToListAsync();
        }

        [HttpGet("module/{moduleId}")]
        public async Task<ActionResult<IEnumerable<Note>>> GetNotesByModule(int moduleId)
        {
            return await _context.Notes
                .Include(n => n.Etudiant)
                .Include(n => n.Module)
                .Where(n => n.ModuleId == moduleId)
                .OrderByDescending(n => n.Annee).ThenBy(n => n.Semestre)
                .Take(500)           // safety cap — one module's cohort notes
                .ToListAsync();
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPost]
        public async Task<ActionResult<Note>> PostNote(CreateNoteDto dto)
        {
            var note = new Note
            {
                EtudiantId = dto.EtudiantId,
                ModuleId   = dto.ModuleId,
                NoteExamen = dto.NoteExamen,
                NoteTD     = dto.NoteTD,
                NoteTP     = dto.NoteTP,
                NoteFinal  = dto.NoteFinal,
                Annee      = dto.Annee,
                Semestre   = dto.Semestre,
                CreeLe     = DateTime.UtcNow
            };

            _context.Notes.Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Note créée : Id={NoteId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}",
                note.Id, note.EtudiantId, note.ModuleId);

            // Auto-generate alert if grade is below threshold
            await _alerteService.CheckNoteAlertAsync(note.EtudiantId, note.ModuleId);

            return CreatedAtAction(nameof(GetNote), new { id = note.Id }, note);
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("upsert")]
        public async Task<ActionResult<UpsertNoteResultDto>> UpsertNote(UpsertNoteDto dto)
        {
            var studentExists = await _context.Etudiants
                .AnyAsync(e => e.Id == dto.EtudiantId && e.DesinscritLe == null);
            if (!studentExists)
                return NotFound(new { message = "Etudiant actif introuvable." });

            if (!await _context.Modules.AnyAsync(m => m.Id == dto.ModuleId))
                return NotFound(new { message = "Module introuvable." });

            var note = await _context.Notes.SingleOrDefaultAsync(n =>
                n.EtudiantId == dto.EtudiantId
                && n.ModuleId == dto.ModuleId
                && n.Annee == dto.Annee
                && n.Semestre == dto.Semestre);

            var created = note == null;
            note ??= new Note
            {
                EtudiantId = dto.EtudiantId,
                ModuleId = dto.ModuleId,
                Annee = dto.Annee,
                Semestre = dto.Semestre,
                CreeLe = DateTime.UtcNow,
            };

            note.NoteTD = dto.NoteTD;
            note.NoteTP = dto.NoteTP;
            note.NoteExamen = dto.NoteExamen;
            note.NoteFinal = dto.NoteFinal;

            if (created) _context.Notes.Add(note);
            await _context.SaveChangesAsync();
            await _alerteService.CheckNoteAlertAsync(note.EtudiantId, note.ModuleId);

            var result = new UpsertNoteResultDto { Id = note.Id, Created = created };
            return created
                ? StatusCode(StatusCodes.Status201Created, result)
                : Ok(result);
        }

        [Authorize(Roles = "Admin,Enseignant")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutNote(int id, UpdateNoteDto dto)
        {
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();

            note.NoteExamen = dto.NoteExamen;
            note.NoteTD     = dto.NoteTD;
            note.NoteTP     = dto.NoteTP;
            note.NoteFinal  = dto.NoteFinal;
            note.Annee      = dto.Annee;
            note.Semestre   = dto.Semestre;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Notes.AnyAsync(n => n.Id == id)) return NotFound();
                else throw;
            }

            // Re-check alert after grade update
            await _alerteService.CheckNoteAlertAsync(note.EtudiantId, note.ModuleId);

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

            _logger.LogInformation("Note supprimée : Id={NoteId}, EtudiantId={EtudiantId}, ModuleId={ModuleId}",
                note.Id, note.EtudiantId, note.ModuleId);

            return NoContent();
        }
    }
}
