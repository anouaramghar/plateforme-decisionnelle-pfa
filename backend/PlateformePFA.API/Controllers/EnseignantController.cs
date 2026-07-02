using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Enseignant;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [ApiController]
    [Route("api/enseignant")]
    [Authorize(Roles = "Enseignant,Admin")]
    public class EnseignantController : ControllerBase
    {
        private readonly AppDbContext _context;
        private const string TreatedMarker = "[teacher-follow-up:treated]";
        private const string RequestedMarker = "[teacher-follow-up:requested]";

        public EnseignantController(AppDbContext context) => _context = context;

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private string CurrentUserName =>
            User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Systeme";

        private async Task<Utilisateur?> CurrentTeacherAsync()
        {
            var userId = CurrentUserId;
            if (userId is null) return null;

            return await _context.Utilisateurs
                .Include(u => u.Module)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private async Task<InterventionCase?> FindTeacherCaseAsync(int caseId, Utilisateur teacher)
        {
            if (teacher.Module is null) return null;

            return await _context.InterventionCases
                .Include(c => c.Etudiant)
                .FirstOrDefaultAsync(c =>
                    c.Id == caseId &&
                    c.Etudiant.FiliereId == teacher.Module.FiliereId &&
                    c.Etudiant.Niveau == teacher.Module.Niveau);
        }

        private async Task<ActionResult<CaseEvent>> CreateFollowUpNoteAsync(
            int caseId,
            TeacherFollowUpActionDto? dto,
            string? marker,
            string? defaultContent)
        {
            var teacher = await CurrentTeacherAsync();
            if (teacher is null) return CurrentUserId is null ? Unauthorized() : NotFound();
            if (await FindTeacherCaseAsync(caseId, teacher) is null)
                return NotFound(new { message = "Cas introuvable." });

            var content = dto?.Contenu?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                if (defaultContent is null) return BadRequest(new { message = "Contenu requis." });
                content = defaultContent;
            }

            if (marker is not null) content = $"{marker} {content}";
            if (content.Length > 2000) return BadRequest(new { message = "Contenu trop long." });

            var note = new CaseEvent
            {
                CaseId = caseId,
                Type = "Note",
                Contenu = content,
                IsPrivate = false,
                UtilisateurId = teacher.Id,
                UtilisateurNom = CurrentUserName,
            };
            _context.CaseEvents.Add(note);
            await _context.SaveChangesAsync();
            return Created($"/api/enseignant/suivi/{caseId}", note);
        }

        private static string? CleanLastAction(string? content)
        {
            if (content is null) return null;
            var cleaned = content
                .Replace(TreatedMarker, "")
                .Replace(RequestedMarker, "")
                .Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }

        /// <summary>Returns the module (with its filière) assigned to the logged-in enseignant.</summary>
        [HttpGet("mon-module")]
        public async Task<IActionResult> GetMonModule()
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            var utilisateur = await _context.Utilisateurs
                .AsNoTracking()
                .Include(u => u.Module)
                    .ThenInclude(m => m!.Filiere)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (utilisateur is null) return NotFound();
            if (utilisateur.ModuleId is null)
                return Ok(null); // no module assigned yet

            var m = utilisateur.Module!;
            return Ok(new
            {
                moduleId      = m.Id,
                moduleCode    = m.Code,
                moduleNom     = m.Nom,
                semestre      = m.Semestre,
                coefficient   = m.Coefficient,
                filiereId     = m.FiliereId,
                filiereCode   = m.Filiere!.Code,
                filiereIntitule = m.Filiere.Intitule,
                niveau        = m.Niveau,
            });
        }

        /// <summary>Returns students enrolled in the filière of the enseignant's module.</summary>
        [HttpGet("mes-etudiants")]
        public async Task<IActionResult> GetMesEtudiants()
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            var utilisateur = await _context.Utilisateurs
                .AsNoTracking()
                .Include(u => u.Module)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (utilisateur?.ModuleId is null)
                return Ok(Array.Empty<object>());

            var filiereId = utilisateur.Module!.FiliereId;

            var etudiants = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.FiliereId == filiereId && e.DesinscritLe == null)
                .OrderBy(e => e.Nom).ThenBy(e => e.Prenom)
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    nomComplet    = e.Nom + " " + e.Prenom,
                    e.Nom,
                    e.Prenom,
                    e.FiliereId,
                    e.Niveau,
                    e.Annee,
                })
                .ToListAsync();

            return Ok(etudiants);
        }

        [HttpGet("suivi")]
        public async Task<ActionResult<IEnumerable<TeacherFollowUpCardDto>>> GetSuivi()
        {
            var teacher = await CurrentTeacherAsync();
            if (teacher is null) return CurrentUserId is null ? Unauthorized() : NotFound();
            if (teacher.Module is null) return Ok(Array.Empty<TeacherFollowUpCardDto>());

            var cases = await _context.InterventionCases
                .AsNoTracking()
                .Include(c => c.Etudiant)
                .Where(c => c.Etudiant.FiliereId == teacher.Module.FiliereId &&
                            c.Etudiant.Niveau == teacher.Module.Niveau)
                .OrderByDescending(c => c.CreeLe)
                .Take(100)
                .Select(c => new
                {
                    c.Id,
                    c.EtudiantId,
                    StudentName = c.Etudiant.Nom + " " + c.Etudiant.Prenom,
                    c.Motif,
                    c.Priorite,
                    c.Etat,
                    c.CreeLe,
                })
                .ToListAsync();

            var caseIds = cases.Select(c => c.Id).ToArray();
            var notes = await _context.CaseEvents
                .AsNoTracking()
                .Where(n => caseIds.Contains(n.CaseId) && n.Type == "Note" && !n.IsPrivate)
                .OrderByDescending(n => n.CreeLe)
                .ToListAsync();
            var notesByCase = notes.GroupBy(n => n.CaseId).ToDictionary(g => g.Key, g => g.ToList());

            return Ok(cases.Select(c =>
            {
                notesByCase.TryGetValue(c.Id, out var caseNotes);
                caseNotes ??= new List<CaseEvent>();

                var treated = c.Etat is CaseWorkflowState.Resolved or CaseWorkflowState.Closed ||
                              caseNotes.Any(n => n.UtilisateurId == teacher.Id &&
                                                 (n.Contenu ?? "").StartsWith(TreatedMarker, StringComparison.Ordinal));
                var followed = c.Etat != CaseWorkflowState.Open || caseNotes.Any(n => n.UtilisateurId == teacher.Id);
                var column = treated ? "Traite" : followed ? "En suivi" : "A voir";

                return new TeacherFollowUpCardDto(
                    c.Id,
                    c.EtudiantId,
                    c.StudentName,
                    c.Motif,
                    c.Priorite,
                    column,
                    CleanLastAction(caseNotes.FirstOrDefault()?.Contenu),
                    c.CreeLe);
            }));
        }

        [HttpPost("suivi/{caseId:int}/observation")]
        public Task<ActionResult<CaseEvent>> CreateObservation(int caseId, TeacherFollowUpActionDto? dto) =>
            CreateFollowUpNoteAsync(caseId, dto, marker: null, defaultContent: null);

        [HttpPost("suivi/{caseId:int}/treated")]
        public Task<ActionResult<CaseEvent>> MarkTreated(int caseId, TeacherFollowUpActionDto? dto) =>
            CreateFollowUpNoteAsync(caseId, dto, TreatedMarker, "Traite par l'enseignant.");

        [HttpPost("suivi/{caseId:int}/request-intervention")]
        public Task<ActionResult<CaseEvent>> RequestIntervention(int caseId, TeacherFollowUpActionDto? dto) =>
            CreateFollowUpNoteAsync(caseId, dto, RequestedMarker, "Intervention demandee par l'enseignant.");
    }
}
