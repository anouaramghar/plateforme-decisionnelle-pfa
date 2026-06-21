using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Etudiants;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EtudiantsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RiskScorer _riskScorer;

        public EtudiantsController(AppDbContext context, RiskScorer riskScorer)
        {
            _context = context;
            _riskScorer = riskScorer;
        }

        // Project Etudiant + Filiere into a flat DTO once. Used by both list and detail.
        private static readonly System.Linq.Expressions.Expression<Func<Etudiant, EtudiantDto>>
            ToDto = e => new EtudiantDto
            {
                Id              = e.Id,
                Matricule       = e.Matricule,
                Nom             = e.Nom,
                Prenom          = e.Prenom,
                Email           = e.Email,
                FiliereId       = e.FiliereId,
                FiliereCode     = e.Filiere != null ? e.Filiere.Code     : string.Empty,
                FiliereIntitule = e.Filiere != null ? e.Filiere.Intitule : string.Empty,
                Niveau          = e.Niveau,
                Annee           = e.Annee,
            };

        // 0. GET WITH STATS — returns all students enriched with averages,
        //    absences, validated module count, and ML risk score. Sized for
        //    the Étudiants page which filters/sorts client-side over ~300 rows.
        [HttpGet("with-stats")]
        public async Task<ActionResult<List<EtudiantWithStatsDto>>> GetWithStats()
        {
            // Per-student aggregates in one DB round trip.
            var rows = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.DesinscritLe == null)
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    e.Nom,
                    e.Prenom,
                    e.Email,
                    e.FiliereId,
                    FiliereCode     = e.Filiere != null ? e.Filiere.Code     : string.Empty,
                    FiliereIntitule = e.Filiere != null ? e.Filiere.Intitule : string.Empty,
                    e.Niveau,
                    e.Annee,
                    NotesFinales = e.Notes!
                        .Where(n => n.NoteFinal.HasValue)
                        .Select(n => n.NoteFinal!.Value)
                        .ToList(),
                    AbsencesH = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                })
                .ToListAsync();

            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();

            var result = rows.Select(r =>
            {
                var modulesTotal   = r.NotesFinales.Count;
                var modulesValides = r.NotesFinales.Count(n => n >= 10m);
                var moyenne = modulesTotal > 0
                    ? Math.Round(r.NotesFinales.Average(), 2)
                    : 0m;

                var score = _riskScorer.Score(moyenne, r.AbsencesH, r.Id, latestPredictions);

                var risque = RiskScorer.Bucket(score);
                var statut = RiskScorer.StatutLabel(moyenne);

                return new EtudiantWithStatsDto
                {
                    Id              = r.Id,
                    Matricule       = r.Matricule,
                    Nom             = r.Nom,
                    Prenom          = r.Prenom,
                    NomComplet      = $"{r.Prenom} {r.Nom}".Trim(),
                    Email           = r.Email,
                    FiliereId       = r.FiliereId,
                    FiliereCode     = r.FiliereCode,
                    FiliereIntitule = r.FiliereIntitule,
                    Niveau          = r.Niveau,
                    Annee           = r.Annee,
                    Moyenne         = moyenne,
                    Absences        = r.AbsencesH,
                    ModulesValides  = modulesValides,
                    ModulesTotal    = modulesTotal,
                    ScoreRisque     = Math.Round(score, 3),
                    Risque          = risque,
                    Statut          = statut,
                };
            }).ToList();

            return result;
        }

        // 0b. GET PER-MODULE NOTES — used by the student detail drawer.
        //     Latest semester per (module) is what surfaces; older entries are
        //     kept in the DB for history but not shown here.
        [HttpGet("{id}/notes")]
        public async Task<ActionResult<List<EtudiantNoteDto>>> GetEtudiantNotes(int id)
        {
            var exists = await _context.Etudiants.AnyAsync(e => e.Id == id);
            if (!exists) return NotFound();

            var notes = await _context.Notes
                .AsNoTracking()
                .Where(n => n.EtudiantId == id)
                .OrderByDescending(n => n.Annee)
                .ThenByDescending(n => n.Semestre)
                .ThenBy(n => n.ModuleId)
                .Select(n => new EtudiantNoteDto
                {
                    ModuleId = n.ModuleId,
                    Code     = n.Module.Code,
                    Nom      = n.Module.Nom,
                    Semestre = n.Semestre,
                    Annee    = n.Annee,
                    Coef     = n.Module.Coefficient,
                    Cc       = n.NoteTD,
                    Tp       = n.NoteTP,
                    Exam     = n.NoteExamen,
                    Finale   = n.NoteFinal,
                    Valide   = n.NoteFinal.HasValue && n.NoteFinal.Value >= 10m,
                })
                .ToListAsync();

            // Keep most-recent occurrence per module to avoid duplicating the
            // same module across S1 + S2.
            var byModule = notes
                .GroupBy(n => n.ModuleId)
                .Select(g => g.First())
                .ToList();

            return byModule;
        }

        // 1. GET ALL (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<EtudiantDto>>> GetEtudiants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Etudiants.AsNoTracking().Where(e => e.DesinscritLe == null).OrderBy(e => e.Id);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToListAsync();

            return new PaginatedResult<EtudiantDto>(items, total, page, pageSize);
        }

        // 2. GET ONE
        [HttpGet("{id}")]
        public async Task<ActionResult<EtudiantDto>> GetEtudiant(int id)
        {
            var dto = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(ToDto)
                .FirstOrDefaultAsync();

            if (dto == null) return NotFound();
            return dto;
        }

        // 3. CREATE (POST)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<EtudiantDto>> PostEtudiant(CreateEtudiantDto dto)
        {
            // Reject up front rather than waiting for SQL to throw a UNIQUE violation.
            if (await _context.Etudiants.AnyAsync(e => e.Matricule == dto.Matricule))
                return Conflict(new { message = $"Matricule '{dto.Matricule}' déjà utilisé." });

            if (!await _context.Filieres.AnyAsync(f => f.Id == dto.FiliereId))
                return BadRequest(new { message = $"FiliereId {dto.FiliereId} introuvable." });

            var etudiant = new Etudiant
            {
                Matricule = dto.Matricule,
                Nom       = dto.Nom,
                Prenom    = dto.Prenom,
                Email     = dto.Email,
                FiliereId = dto.FiliereId,
                Niveau    = dto.Niveau,
                Annee     = dto.Annee,
                CreeLe    = DateTime.UtcNow
            };

            _context.Etudiants.Add(etudiant);
            await _context.SaveChangesAsync();

            // Reload with the joined Filiere so the response is shape-consistent with GET.
            var created = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.Id == etudiant.Id)
                .Select(ToDto)
                .FirstAsync();

            return CreatedAtAction(nameof(GetEtudiant), new { id = etudiant.Id }, created);
        }

        // 4. UPDATE (PUT)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEtudiant(int id, UpdateEtudiantDto dto)
        {
            var etudiant = await _context.Etudiants.FindAsync(id);
            if (etudiant == null) return NotFound();

            etudiant.Matricule = dto.Matricule;
            etudiant.Nom       = dto.Nom;
            etudiant.Prenom    = dto.Prenom;
            etudiant.Email     = dto.Email;
            etudiant.FiliereId = dto.FiliereId;
            etudiant.Niveau    = dto.Niveau;
            etudiant.Annee     = dto.Annee;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Etudiants.AnyAsync(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 5. DELETE
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEtudiant(int id)
        {
            var etudiant = await _context.Etudiants.FindAsync(id);
            if (etudiant == null) return NotFound();

            etudiant.DesinscritLe = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
