using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Interventions;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    // Focused student-outreach workflow: prepare an editable email draft, schedule
    // a meeting and send the invitation exactly once, then record the outcome.
    // Kept separate from InterventionCasesController so the generic case surface
    // stays small. The backend is authoritative for transitions and delivery
    // state; the Copilot runtime only suggests draft text.
    //
    // Admin/Responsable only — teachers contribute notes via the case controller
    // but never drive outreach.
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/intervention-cases/{caseId:int}/outreach")]
    [ApiController]
    public class CaseOutreachController : ControllerBase
    {
        private const string OutreachTemplateId = "student_outreach";

        private readonly AppDbContext _db;
        private readonly IAuditService _audit;
        private readonly IEmailSender _email;
        private readonly ILogger<CaseOutreachController> _logger;

        public CaseOutreachController(AppDbContext db, IAuditService audit, IEmailSender email, ILogger<CaseOutreachController> logger)
        {
            _db = db;
            _audit = audit;
            _email = email;
            _logger = logger;
        }

        private (int? id, string name) CurrentUser()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? id = int.TryParse(idClaim, out var uid) ? uid : null;
            var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Système";
            return (id, name);
        }

        // POST: api/intervention-cases/5/outreach/draft
        // Creates the single editable draft and moves the case to InProgress.
        [HttpPost("draft")]
        public async Task<ActionResult<CaseCommunication>> CreateDraft(int caseId, SaveOutreachDraftDto dto)
        {
            var c = await _db.InterventionCases.Include(x => x.Etudiant)
                .SingleOrDefaultAsync(x => x.Id == caseId);
            if (c == null) return NotFound(new { message = "Cas introuvable." });
            if (string.IsNullOrWhiteSpace(c.Etudiant.Email))
                return BadRequest(new { message = "L'étudiant ne possède pas d'adresse email." });
            if (await _db.CaseCommunications.AnyAsync(x => x.CaseId == caseId && x.Status == CommunicationStatus.Draft))
                return Conflict(new { message = "Un brouillon existe déjà pour cette intervention." });

            var (userId, userName) = CurrentUser();
            var comm = new CaseCommunication
            {
                CaseId       = caseId,
                Destinataire = c.Etudiant.Email!,
                TemplateId   = OutreachTemplateId,
                Sujet        = dto.Subject.Trim(),
                Corps        = dto.Body.Trim(),
                Status       = CommunicationStatus.Draft,
                CreeParId    = userId,
            };
            c.Etat = CaseWorkflowState.InProgress;
            _db.CaseCommunications.Add(comm);
            _db.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = caseId, Action = "EmailDraftCreated", Description = comm.Sujet,
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("EmailDraftCreated", "InterventionCase", caseId, comm.Sujet, userId, userName);

            _logger.LogInformation("Outreach draft créé : caseId={CaseId}, commId={CommId}", caseId, comm.Id);
            return Created($"/api/intervention-cases/{caseId}/outreach/draft/{comm.Id}", comm);
        }

        // PUT: api/intervention-cases/5/outreach/draft/9
        // Edits subject/body while still a draft. Once sent, the text is immutable.
        [HttpPut("draft/{commId:int}")]
        public async Task<IActionResult> EditDraft(int caseId, int commId, SaveOutreachDraftDto dto)
        {
            var comm = await _db.CaseCommunications.FirstOrDefaultAsync(x => x.Id == commId && x.CaseId == caseId);
            if (comm == null) return NotFound(new { message = "Brouillon introuvable." });
            if (comm.Status != CommunicationStatus.Draft)
                return BadRequest(new { message = "Seul un brouillon peut être modifié." });

            var (userId, userName) = CurrentUser();
            comm.Sujet = dto.Subject.Trim();
            comm.Corps = dto.Body.Trim();
            _db.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = caseId, Action = "EmailDraftEdited", Description = comm.Sujet,
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync("EmailDraftEdited", "InterventionCase", caseId, comm.Sujet, userId, userName);
            return NoContent();
        }
    }
}
