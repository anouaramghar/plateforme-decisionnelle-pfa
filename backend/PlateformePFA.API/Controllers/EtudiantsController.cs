using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Etudiants;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EtudiantsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RiskScorer _riskScorer;
        private readonly IAcademicAccessService _academicAccess;

        public EtudiantsController(AppDbContext context, RiskScorer riskScorer, IAcademicAccessService academicAccess)
        {
            _context = context;
            _riskScorer = riskScorer;
            _academicAccess = academicAccess;
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
            // A teacher only sees the cohort of their assigned module — i.e.
            // students in the same filière + niveau. Admin/Responsable see all.
            var scope = await _academicAccess.GetModuleScopeAsync(User);
            var baseQuery = _context.Etudiants.AsNoTracking().Where(e => e.DesinscritLe == null);
            if (!scope.Unrestricted)
            {
                var module = await _context.Modules.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == scope.FilterModuleId);
                if (module == null) return new List<EtudiantWithStatsDto>();
                baseQuery = baseQuery.Where(e => e.FiliereId == module.FiliereId && e.Niveau == module.Niveau);
            }

            // Per-student aggregates in one DB round trip.
            var rows = await baseQuery
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
            var exists = await _context.Etudiants.AnyAsync(e => e.Id == id && e.DesinscritLe == null);
            if (!exists) return NotFound();

            // A teacher only sees grades for their own module; Admin/Responsable see all.
            var scope = await _academicAccess.GetModuleScopeAsync(User);
            var notesQuery = _context.Notes.AsNoTracking().Where(n => n.EtudiantId == id);
            if (!scope.Unrestricted)
            {
                notesQuery = notesQuery.Where(n => n.ModuleId == scope.FilterModuleId);
            }

            var notes = await notesQuery
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

            // Teachers only see their own cohort (filière + niveau of their module);
            // Admin/Responsable are unrestricted.
            var cohort = await _academicAccess.GetCohortScopeAsync(User);
            var baseQuery = _context.Etudiants.AsNoTracking().Where(e => e.DesinscritLe == null);
            if (!cohort.Unrestricted)
            {
                if (!cohort.HasCohort) return new PaginatedResult<EtudiantDto>(new List<EtudiantDto>(), 0, page, pageSize);
                baseQuery = baseQuery.Where(e => e.FiliereId == cohort.FiliereId && e.Niveau == cohort.Niveau);
            }

            var query = baseQuery.OrderBy(e => e.Id);
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
            var cohort = await _academicAccess.GetCohortScopeAsync(User);
            var query = _context.Etudiants.AsNoTracking().Where(e => e.Id == id && e.DesinscritLe == null);
            if (!cohort.Unrestricted)
            {
                // A teacher outside their cohort gets 404, not a leak of existence.
                if (!cohort.HasCohort) return NotFound();
                query = query.Where(e => e.FiliereId == cohort.FiliereId && e.Niveau == cohort.Niveau);
            }

            var dto = await query.Select(ToDto).FirstOrDefaultAsync();
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

        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost("import")]
        public async Task<ActionResult<ImportEtudiantsResultDto>> ImportEtudiants(
            [FromBody] List<ImportEtudiantRowDto> rows)
        {
            var errors = new List<ImportEtudiantErrorDto>();
            if (rows.Count == 0)
            {
                errors.Add(new ImportEtudiantErrorDto(0, "file", "Le fichier ne contient aucune ligne."));
                return BadRequest(new ImportEtudiantsResultDto(0, errors));
            }

            var filieres = await _context.Filieres.AsNoTracking().ToListAsync();
            var filiereByCode = filieres.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);
            var existingMatricules = (await _context.Etudiants.AsNoTracking()
                .Select(e => e.Matricule)
                .ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var levels = new HashSet<string>(new[] { "CP1", "CP2", "CI1", "CI2", "CI3" }, StringComparer.OrdinalIgnoreCase);
            var batchMatricules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emailValidator = new EmailAddressAttribute();

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var rowNumber = row.RowNumber > 0 ? row.RowNumber : index + 2;
                row.Matricule = row.Matricule.Trim();
                row.Nom = row.Nom.Trim();
                row.Prenom = row.Prenom.Trim();
                row.Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim();
                row.FiliereCode = row.FiliereCode.Trim();
                row.Niveau = row.Niveau.Trim().ToUpperInvariant();
                row.Annee = row.Annee.Trim();

                void Required(string value, string field) { if (string.IsNullOrWhiteSpace(value)) errors.Add(new(rowNumber, field, "Champ obligatoire.")); }
                Required(row.Matricule, "matricule");
                Required(row.Nom, "nom");
                Required(row.Prenom, "prenom");
                Required(row.FiliereCode, "filiereCode");
                Required(row.Niveau, "niveau");
                Required(row.Annee, "annee");

                if (!string.IsNullOrWhiteSpace(row.Matricule) && !batchMatricules.Add(row.Matricule))
                    errors.Add(new(rowNumber, "matricule", "Matricule dupliqué dans le fichier."));
                if (existingMatricules.Contains(row.Matricule))
                    errors.Add(new(rowNumber, "matricule", "Matricule déjà enregistré."));
                if (!string.IsNullOrWhiteSpace(row.FiliereCode) && !filiereByCode.ContainsKey(row.FiliereCode))
                    errors.Add(new(rowNumber, "filiereCode", "Filière introuvable."));
                if (!string.IsNullOrWhiteSpace(row.Niveau) && !levels.Contains(row.Niveau))
                    errors.Add(new(rowNumber, "niveau", "Niveau invalide."));
                if (!string.IsNullOrWhiteSpace(row.Annee) && !Regex.IsMatch(row.Annee, Validation.AnneePattern))
                    errors.Add(new(rowNumber, "annee", Validation.AnneeError));
                if (row.Email != null && !emailValidator.IsValid(row.Email))
                    errors.Add(new(rowNumber, "email", "Adresse email invalide."));
            }

            if (errors.Count > 0)
                return BadRequest(new ImportEtudiantsResultDto(0, errors));

            var students = rows.Select(row => new Etudiant
            {
                Matricule = row.Matricule,
                Nom = row.Nom,
                Prenom = row.Prenom,
                Email = row.Email,
                FiliereId = filiereByCode[row.FiliereCode].Id,
                Niveau = row.Niveau,
                Annee = row.Annee,
                CreeLe = DateTime.UtcNow,
            }).ToList();

            _context.Etudiants.AddRange(students);
            await _context.SaveChangesAsync();
            return Ok(new ImportEtudiantsResultDto(
                students.Count,
                Array.Empty<ImportEtudiantErrorDto>()));
        }

        // 4. UPDATE (PUT)
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEtudiant(int id, UpdateEtudiantDto dto)
        {
            var etudiant = await _context.Etudiants
                .FirstOrDefaultAsync(e => e.Id == id && e.DesinscritLe == null);
            if (etudiant == null) return NotFound();

            // Mirror the POST guards: reject a matricule collision or unknown filière
            // up front instead of letting SQL throw an unhandled 500 on save.
            if (await _context.Etudiants.AnyAsync(e => e.Matricule == dto.Matricule && e.Id != id))
                return Conflict(new { message = $"Matricule '{dto.Matricule}' déjà utilisé." });
            if (!await _context.Filieres.AnyAsync(f => f.Id == dto.FiliereId))
                return BadRequest(new { message = $"FiliereId {dto.FiliereId} introuvable." });

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

            if (etudiant.DesinscritLe == null)
            {
                etudiant.DesinscritLe = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }
    }
}
