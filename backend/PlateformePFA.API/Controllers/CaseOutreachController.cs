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
        private readonly ILogger<CaseOutreachController> _logger;

        public CaseOutreachController(AppDbContext db, IAuditService audit, ILogger<CaseOutreachController> logger)
        {
            _db = db;
            _audit = audit;
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
            var hasPendingCommunication = await _db.CaseCommunications.AnyAsync(x =>
                x.CaseId == caseId && x.TemplateId == OutreachTemplateId &&
                x.Status == CommunicationStatus.Draft);
            if (hasPendingCommunication)
                return Conflict(new { message = "Un brouillon ou un envoi en attente existe déjà pour cette intervention." });

            // Guard: only advance the state when the case is still Open.
            // A case that is already InProgress, WaitingStudent, etc. must not
            // be unconditionally reset; Resolved/Closed cases must go through
            // the proper reopen flow before a new draft can be prepared.
            var mayStart = c.Etat == CaseWorkflowState.Open || c.Etat == CaseWorkflowState.InProgress;
            var mayReschedule = c.Etat == CaseWorkflowState.WaitingStudent &&
                (c.MeetingAttendance == "Absent" || c.MeetingAttendance == "Cancelled");
            if (!mayStart && !mayReschedule)
                return BadRequest(new { message = "L'état actuel du cas ne permet pas de préparer une invitation." });

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
            // A new outreach cycle always starts in InProgress. This includes a
            // reschedule after Absent/Cancelled; the previous schedule remains
            // preserved in the immutable timeline.
            if (c.Etat != CaseWorkflowState.InProgress)
            {
                var facts = new CaseWorkflow.CaseFacts(
                    HasOwner: c.OwnerId.HasValue,
                    HasOutcome: false,
                    HasReason: false,
                    MonitoringComplete: false,
                    MeetingHeld: false);
                var (ok, transitionError) = CaseWorkflow.CanTransition(c.Etat, CaseWorkflowState.InProgress, facts);
                if (!ok) return BadRequest(new { message = transitionError });

                c.Etat = CaseWorkflowState.InProgress;
                if (mayReschedule)
                {
                    c.MeetingScheduledFor = null;
                    c.MeetingLocation = null;
                    c.MeetingAttendance = null;
                    c.MeetingHeldAt = null;
                }
            }
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

        // POST: api/intervention-cases/5/outreach/draft/9/send
        // Marks the draft as sent (no real SMTP delivery — the responsible staff
        // sends the email manually outside the platform). The case advances to
        // WaitingStudent with the meeting details recorded.
        [HttpPost("draft/{commId:int}/send")]
        public async Task<IActionResult> ScheduleAndMarkSent(int caseId, int commId, ScheduleAndSendDto dto)
        {
            var comm = await _db.CaseCommunications.FirstOrDefaultAsync(x => x.Id == commId && x.CaseId == caseId);
            if (comm == null) return NotFound(new { message = "Brouillon introuvable." });
            if (comm.Status != CommunicationStatus.Draft)
                return BadRequest(new { message = "Cet email a déjà été traité." });

            var c = await _db.InterventionCases.FirstOrDefaultAsync(x => x.Id == caseId);
            if (c == null) return NotFound(new { message = "Cas introuvable." });
            if (c.Etat != CaseWorkflowState.InProgress)
                return BadRequest(new { message = "L'intervention doit être en cours pour envoyer l'invitation." });
            if (dto.ScheduledFor <= DateTime.UtcNow)
                return BadRequest(new { message = "La date du rendez-vous doit être dans le futur." });
            if (string.IsNullOrWhiteSpace(dto.Location))
                return BadRequest(new { message = "Le lieu du rendez-vous est requis." });

            var (userId, userName) = CurrentUser();
            c.MeetingScheduledFor = dto.ScheduledFor.ToUniversalTime();
            c.MeetingLocation = dto.Location.Trim();
            _db.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = caseId, Action = "MeetingScheduled",
                Description = $"Rendez-vous le {c.MeetingScheduledFor:dd/MM/yyyy HH:mm} — {c.MeetingLocation}",
                UtilisateurId = userId, UtilisateurNom = userName,
            });

            if (!await MarkAsSentAsync(c, comm, userId, userName))
                return Conflict(new { message = "Cet email a déjà été marqué comme envoyé." });
            return NoContent();
        }

        // POST: api/intervention-cases/5/outreach/meeting-result
        // Records attendance. Held resolves the case (requires outcome + summary);
        // Absent/Cancelled keep it active for a reschedule.
        [HttpPost("meeting-result")]
        public async Task<IActionResult> RecordMeeting(int caseId, RecordMeetingDto dto)
        {
            var c = await _db.InterventionCases.FirstOrDefaultAsync(x => x.Id == caseId);
            if (c == null) return NotFound(new { message = "Cas introuvable." });

            var allowed = new[] { "Held", "Absent", "Cancelled" };
            if (!allowed.Contains(dto.Attendance))
                return BadRequest(new { message = "Présence invalide." });

            if (c.Etat != CaseWorkflowState.WaitingStudent)
                return BadRequest(new { message = "Aucun rendez-vous en attente pour cette intervention." });

            if (dto.Attendance == "Held" &&
                (!dto.HeldAt.HasValue || dto.HeldAt > DateTime.UtcNow ||
                 string.IsNullOrWhiteSpace(dto.Outcome) || string.IsNullOrWhiteSpace(dto.Summary)))
                return BadRequest(new { message = "La date, le résultat et le résumé sont requis." });

            var (userId, userName) = CurrentUser();
            c.MeetingAttendance = dto.Attendance;

            if (dto.Attendance == "Held")
            {
                c.MeetingHeldAt      = dto.HeldAt!.Value.ToUniversalTime();
                c.Outcome           = dto.Outcome;
                c.ResolutionSummary = dto.Summary;
                c.Etat              = CaseWorkflowState.Resolved;
                _db.CaseTimelineEvents.Add(new CaseTimelineEvent
                {
                    CaseId = caseId, Action = "MeetingHeld", Description = dto.Summary,
                    UtilisateurId = userId, UtilisateurNom = userName,
                });
            }
            else
            {
                // Absent/Cancelled: the intervention stays active — a later
                // reschedule/send is required before it can resolve.
                c.MeetingHeldAt = null;
                _db.CaseTimelineEvents.Add(new CaseTimelineEvent
                {
                    CaseId = caseId,
                    Action = dto.Attendance == "Absent" ? "MeetingAbsent" : "MeetingCancelled",
                    Description = dto.Attendance == "Absent" ? "Étudiant absent au rendez-vous." : "Rendez-vous annulé.",
                    UtilisateurId = userId, UtilisateurNom = userName,
                });
            }

            await _db.SaveChangesAsync();
            await _audit.LogAsync($"Meeting{dto.Attendance}", "InterventionCase", caseId, dto.Summary, userId, userName);
            return NoContent();
        }

        // Claims the send slot (Draft → Sent) with optimistic concurrency.
        // No real SMTP delivery — the staff member sends the email manually.
        // The case advances to WaitingStudent with the meeting details.
        private async Task<bool> MarkAsSentAsync(InterventionCase c, CaseCommunication comm, int? userId, string userName)
        {
            comm.Status = CommunicationStatus.Sent;
            comm.Erreur = null;
            comm.EnvoyeLe = DateTime.UtcNow;
            comm.ConcurrencyToken = Guid.NewGuid();
            c.Etat = CaseWorkflowState.WaitingStudent;
            _db.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = c.Id,
                Action = "EmailSent",
                Description = comm.Sujet,
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "MarkAsSentAsync: commId={CommId} already claimed by another request — skipping.",
                    comm.Id);
                return false;
            }
            await _audit.LogAsync("EmailSent", "InterventionCase", c.Id, comm.Sujet, userId, userName);
            _logger.LogInformation("Outreach marqué comme envoyé : caseId={CaseId}, commId={CommId}", c.Id, comm.Id);
            return true;
        }
    }
}
