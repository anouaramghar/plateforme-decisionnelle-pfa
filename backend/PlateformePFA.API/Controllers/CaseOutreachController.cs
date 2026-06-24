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

        // POST: api/intervention-cases/5/outreach/draft/9/send
        // Schedules the meeting and sends the (reviewed) draft exactly once.
        [HttpPost("draft/{commId:int}/send")]
        public async Task<IActionResult> ScheduleAndSend(int caseId, int commId, ScheduleAndSendDto dto)
        {
            var comm = await _db.CaseCommunications.FirstOrDefaultAsync(x => x.Id == commId && x.CaseId == caseId);
            if (comm == null) return NotFound(new { message = "Brouillon introuvable." });

            // Only a fresh draft or a previously failed send may be (re)sent here.
            // Queued means a send is in flight / its outcome is unknown — never resend.
            if (comm.Status != CommunicationStatus.Draft && comm.Status != CommunicationStatus.Failed)
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

            await DeliverAsync(c, comm, userId, userName);
            return NoContent();
        }

        // POST: api/intervention-cases/5/outreach/draft/9/retry
        // Re-sends the STORED text + STORED meeting details after a failure.
        [HttpPost("draft/{commId:int}/retry")]
        public async Task<IActionResult> Retry(int caseId, int commId)
        {
            var comm = await _db.CaseCommunications.FirstOrDefaultAsync(x => x.Id == commId && x.CaseId == caseId);
            if (comm == null) return NotFound(new { message = "Communication introuvable." });
            if (comm.Status != CommunicationStatus.Failed)
                return BadRequest(new { message = "Seules les communications échouées peuvent être renvoyées." });

            var c = await _db.InterventionCases.FirstOrDefaultAsync(x => x.Id == caseId);
            if (c == null) return NotFound(new { message = "Cas introuvable." });

            var (userId, userName) = CurrentUser();
            await DeliverAsync(c, comm, userId, userName);
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

        // Marks Queued, sends, then persists the authoritative outcome. The case
        // only advances to WaitingStudent on a confirmed Sent.
        private async Task DeliverAsync(InterventionCase c, CaseCommunication comm, int? userId, string userName)
        {
            comm.Status = CommunicationStatus.Queued;
            comm.Erreur = null;
            await _db.SaveChangesAsync(); // persist Queued + meeting details before the attempt

            var (ok, error) = await _email.SendAsync(comm.Destinataire, comm.Sujet, comm.Corps);
            comm.Status = ok ? CommunicationStatus.Sent : CommunicationStatus.Failed;
            comm.Erreur = error;
            if (ok)
            {
                comm.EnvoyeLe = DateTime.UtcNow;
                c.Etat = CaseWorkflowState.WaitingStudent;
            }
            _db.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = c.Id,
                Action = ok ? "EmailSent" : "EmailFailed",
                Description = ok ? comm.Sujet : $"Échec : {comm.Sujet}",
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _db.SaveChangesAsync();
            await _audit.LogAsync(ok ? "EmailSent" : "EmailFailed", "InterventionCase", c.Id, comm.Sujet, userId, userName);
        }
    }
}
