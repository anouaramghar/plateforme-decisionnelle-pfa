using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Interventions;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    // Cases are a management workflow. Teachers contribute in a later slice;
    // for now the whole controller is Admin/Responsable only (defense in depth).
    // ponytail: Responsable sees all filières, matching AlertesController. Per-filière
    // scoping is a refinement — add when a second Responsable exists.
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/intervention-cases")]
    [ApiController]
    public class InterventionCasesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;
        private readonly IEmailSender _email;
        private readonly ILogger<InterventionCasesController> _logger;

        public InterventionCasesController(AppDbContext context, IAuditService audit, IEmailSender email, ILogger<InterventionCasesController> logger)
        {
            _context = context;
            _audit   = audit;
            _email   = email;
            _logger  = logger;
        }

        private (int? id, string name) CurrentUser()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? id = int.TryParse(idClaim, out var uid) ? uid : null;
            var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Système";
            return (id, name);
        }

        // Default owner when the caller doesn't pick one. ponytail: Utilisateur has no
        // filière link, so "the filière Responsable" collapses to "a Responsable, else
        // an Admin". Add filière scoping when a User→Filière mapping and a second
        // Responsable exist.
        private async Task<int?> ResolveDefaultOwnerAsync()
        {
            var responsable = await _context.Utilisateurs.AsNoTracking()
                .Where(u => u.EstActif && u.Role == "Responsable")
                .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync();
            if (responsable != null) return responsable;
            return await _context.Utilisateurs.AsNoTracking()
                .Where(u => u.EstActif && u.Role == "Admin")
                .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync();
        }

        // GET: api/intervention-cases?etat=Open&etudiantId=5&page=1&pageSize=20
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<InterventionCase>>> GetCases(
            [FromQuery] string? etat = null,
            [FromQuery] int? etudiantId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.InterventionCases.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(etat)) query = query.Where(c => c.Etat == etat);
            if (etudiantId.HasValue)              query = query.Where(c => c.EtudiantId == etudiantId.Value);

            var ordered = query.OrderByDescending(c => c.CreeLe);
            var total   = await ordered.CountAsync();
            var items   = await ordered
                .Include(c => c.Etudiant)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<InterventionCase>(items, total, page, pageSize);
        }

        // GET: api/intervention-cases/5  (with full timeline)
        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionCase>> GetCase(int id)
        {
            var c = await _context.InterventionCases
                .Include(x => x.Etudiant)
                .Include(x => x.Timeline!.OrderByDescending(t => t.CreeLe))
                .FirstOrDefaultAsync(x => x.Id == id);

            if (c == null) return NotFound(new { message = "Cas introuvable." });
            return c;
        }

        // POST: api/intervention-cases
        [HttpPost]
        public async Task<ActionResult<InterventionCase>> CreateCase(CreateCaseDto dto)
        {
            var etudiant = await _context.Etudiants.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == dto.EtudiantId);
            if (etudiant == null) return BadRequest(new { message = "Étudiant introuvable." });

            var (userId, userName) = CurrentUser();

            var newCase = new InterventionCase
            {
                EtudiantId = dto.EtudiantId,
                FiliereId  = etudiant.FiliereId,
                Motif      = dto.Motif,
                Priorite   = dto.Priorite,
                Etat       = CaseWorkflowState.Open,
                OwnerId    = dto.OwnerId ?? await ResolveDefaultOwnerAsync(),
                DueDate    = dto.DueDate,
                CreeLe     = DateTime.UtcNow,
                CreeParId  = userId,
            };
            _context.InterventionCases.Add(newCase);
            await _context.SaveChangesAsync(); // need the Id for timeline + alert link

            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = newCase.Id, Action = "Created", Description = dto.Motif,
                UtilisateurId = userId, UtilisateurNom = userName,
            });

            // Link the originating signal, if any.
            if (dto.AlerteId.HasValue)
            {
                var alerte = await _context.Alertes.FindAsync(dto.AlerteId.Value);
                if (alerte != null && alerte.EtudiantId == newCase.EtudiantId)
                {
                    alerte.CaseId = newCase.Id;
                    _context.CaseTimelineEvents.Add(new CaseTimelineEvent
                    {
                        CaseId = newCase.Id, Action = "SignalLinked",
                        Description = $"Alerte #{alerte.Id} liée au cas.",
                        UtilisateurId = userId, UtilisateurNom = userName,
                    });
                }
            }

            await _context.SaveChangesAsync();
            await _audit.LogAsync("CreateCase", "InterventionCase", newCase.Id,
                $"Cas ouvert pour étudiant {newCase.EtudiantId}", userId, userName);

            _logger.LogInformation("Case créé : Id={Id}, EtudiantId={EtudiantId}", newCase.Id, newCase.EtudiantId);
            return CreatedAtAction(nameof(GetCase), new { id = newCase.Id }, newCase);
        }

        // POST: api/intervention-cases/5/link-signals
        // Links un-triaged signals (Alertes) to an existing open case — the
        // "link to existing case" outcome of the Intervention Copilot triage
        // card, and the duplicate-case-prevention path. Each signal must belong
        // to the case's student and not already be linked to a case. Idempotent:
        // validated all-or-nothing — if any signal is missing or already linked,
        // the whole request is rejected (no partial linking).
        [HttpPost("{id}/link-signals")]
        public async Task<IActionResult> LinkSignals(int id, LinkSignalsDto dto)
        {
            if (dto?.AlerteIds == null || dto.AlerteIds.Length == 0)
                return BadRequest(new { message = "Aucun identifiant de signal fourni." });

            var c = await _context.InterventionCases.FindAsync(id);
            if (c == null) return NotFound(new { message = "Cas introuvable." });

            // A resolved/closed case is not a valid link target — force the user
            // to reopen or create a new case instead of burying signals in history.
            if (c.Etat == CaseWorkflowState.Resolved || c.Etat == CaseWorkflowState.Closed)
                return BadRequest(new { message = "Impossible de lier des signaux à un cas résolu ou clôturé." });

            var (userId, userName) = CurrentUser();

            var alertes = await _context.Alertes
                .Where(a => dto.AlerteIds.Contains(a.Id))
                .ToListAsync();

            // Validate every signal before applying any change (all-or-nothing).
            var distinctIds = dto.AlerteIds.Distinct().ToList();
            if (alertes.Count != distinctIds.Count)
                return BadRequest(new { message = "Un ou plusieurs signaux sont introuvables." });

            foreach (var a in alertes)
            {
                if (a.EtudiantId != c.EtudiantId)
                    return BadRequest(new { message = $"Le signal #{a.Id} n'appartient pas à l'étudiant de ce cas." });
                if (a.CaseId.HasValue)
                    return BadRequest(new { message = $"Le signal #{a.Id} est déjà lié à un cas." });
            }

            foreach (var a in alertes)
            {
                a.CaseId = c.Id;
            }

            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = c.Id,
                Action = "SignalLinked",
                Description = $"{alertes.Count} signal(aux) lié(s) au cas : {string.Join(", ", alertes.Select(a => a.Id))}.",
                UtilisateurId = userId,
                UtilisateurNom = userName,
            });

            await _context.SaveChangesAsync();
            await _audit.LogAsync("LinkSignals", "InterventionCase", c.Id,
                $"{alertes.Count} signaux liés", userId, userName);

            _logger.LogInformation("Signaux liés au cas {CaseId} : {Ids}", c.Id,
                string.Join(", ", alertes.Select(a => a.Id)));
            return NoContent();
        }

        // PATCH: api/intervention-cases/5/transition
        [HttpPatch("{id}/transition")]
        public async Task<IActionResult> Transition(int id, TransitionCaseDto dto)
        {
            var c = await _context.InterventionCases.FindAsync(id);
            if (c == null) return NotFound(new { message = "Cas introuvable." });

            var (userId, userName) = CurrentUser();

            // Assignment may arrive with the transition (e.g. assign + start).
            if (dto.OwnerId.HasValue) c.OwnerId = dto.OwnerId;

            var from = c.Etat;
            var to   = dto.Etat;

            // Convenience: starting an unassigned case assigns the actor as owner,
            // so the frontend's "Démarrer" needs no separate assignment step.
            if (c.OwnerId == null && to == CaseWorkflowState.InProgress) c.OwnerId = userId;

            var facts = new CaseWorkflow.CaseFacts(
                HasOwner:   c.OwnerId.HasValue,
                HasOutcome: !string.IsNullOrWhiteSpace(dto.Outcome ?? c.Outcome)
                            && !string.IsNullOrWhiteSpace(dto.ResolutionSummary ?? c.ResolutionSummary),
                HasReason:  !string.IsNullOrWhiteSpace(dto.Raison));

            var (ok, error) = CaseWorkflow.CanTransition(from, to, facts);
            if (!ok) return BadRequest(new { message = error });

            // Apply resolution fields on the way to Resolved.
            if (to == CaseWorkflowState.Resolved)
            {
                c.Outcome           = dto.Outcome ?? c.Outcome;
                c.ResolutionSummary = dto.ResolutionSummary ?? c.ResolutionSummary;
                c.FollowUpDate      = dto.FollowUpDate;
            }
            if (to == CaseWorkflowState.Closed)      c.ClotureLe  = DateTime.UtcNow;
            if (to == CaseWorkflowState.Escalated)   c.EscaladeLe = DateTime.UtcNow;

            var isReopen = (from == CaseWorkflowState.Resolved || from == CaseWorkflowState.Closed)
                           && to == CaseWorkflowState.InProgress;

            c.Etat = to;

            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = c.Id,
                Action = isReopen ? "Reopened" : "StateChanged",
                Description = $"{from} → {to}" + (string.IsNullOrWhiteSpace(dto.Raison) ? "" : $" — {dto.Raison}"),
                UtilisateurId = userId, UtilisateurNom = userName,
            });

            await _context.SaveChangesAsync();
            await _audit.LogAsync("TransitionCase", "InterventionCase", c.Id, $"{from} → {to}", userId, userName);

            _logger.LogInformation("Case transition : Id={Id}, {From} → {To}", c.Id, from, to);
            return NoContent();
        }

        // ── Tasks ─────────────────────────────────────────────────────────────

        // GET: api/intervention-cases/5/tasks
        [HttpGet("{id}/tasks")]
        public async Task<ActionResult<IEnumerable<CaseTask>>> GetTasks(int id)
        {
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });

            return await _context.CaseTasks.AsNoTracking()
                .Where(t => t.CaseId == id)
                .OrderBy(t => t.Done).ThenBy(t => t.DueDate)
                .ToListAsync();
        }

        // POST: api/intervention-cases/5/tasks
        [HttpPost("{id}/tasks")]
        public async Task<ActionResult<CaseTask>> CreateTask(int id, CreateTaskDto dto)
        {
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });

            var (userId, userName) = CurrentUser();
            var task = new CaseTask
            {
                CaseId     = id,
                Titre      = dto.Titre,
                AssigneeId = dto.AssigneeId,
                DueDate    = dto.DueDate,
                CreeParId  = userId,
            };
            _context.CaseTasks.Add(task);
            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = id, Action = "TaskAdded", Description = dto.Titre,
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTasks), new { id }, task);
        }

        // PATCH: api/intervention-cases/5/tasks/9/complete
        [HttpPatch("{id}/tasks/{taskId}/complete")]
        public async Task<IActionResult> CompleteTask(int id, int taskId, CompleteTaskDto dto)
        {
            var task = await _context.CaseTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.CaseId == id);
            if (task == null) return NotFound(new { message = "Tâche introuvable." });
            if (task.Done) return BadRequest(new { message = "Tâche déjà terminée." });

            var (userId, userName) = CurrentUser();
            task.Done               = true;
            task.DoneLe             = DateTime.UtcNow;
            task.CompletionEvidence = dto.CompletionEvidence;

            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = id, Action = "TaskCompleted", Description = task.Titre,
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Notes ─────────────────────────────────────────────────────────────

        // GET: api/intervention-cases/5/notes
        [HttpGet("{id}/notes")]
        public async Task<ActionResult<IEnumerable<CaseNote>>> GetNotes(int id)
        {
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });

            // ponytail: controller is Admin/Responsable only, so all notes are
            // visible here. The IsPrivate filter belongs in the teacher-access
            // slice — add `.Where(n => !n.IsPrivate)` for non-privileged callers then.
            return await _context.CaseNotes.AsNoTracking()
                .Where(n => n.CaseId == id)
                .OrderByDescending(n => n.CreeLe)
                .ToListAsync();
        }

        // POST: api/intervention-cases/5/notes
        [HttpPost("{id}/notes")]
        public async Task<ActionResult<CaseNote>> CreateNote(int id, CreateNoteDto dto)
        {
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });

            var (userId, userName) = CurrentUser();
            var note = new CaseNote
            {
                CaseId    = id,
                Contenu   = dto.Contenu,
                IsPrivate = dto.IsPrivate,
                AuteurId  = userId,
                AuteurNom = userName,
            };
            _context.CaseNotes.Add(note);
            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = id, Action = "NoteAdded",
                Description = dto.IsPrivate ? "Note privée ajoutée." : "Note ajoutée.",
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetNotes), new { id }, note);
        }

        // ── Communications (email) ────────────────────────────────────────────

        // GET: api/intervention-cases/5/communications
        [HttpGet("{id}/communications")]
        public async Task<ActionResult<IEnumerable<CaseCommunication>>> GetCommunications(int id)
        {
            if (!await _context.InterventionCases.AnyAsync(c => c.Id == id))
                return NotFound(new { message = "Cas introuvable." });

            return await _context.CaseCommunications.AsNoTracking()
                .Where(cc => cc.CaseId == id)
                .OrderByDescending(cc => cc.CreeLe)
                .ToListAsync();
        }

        // POST: api/intervention-cases/5/communications
        // The explicit POST is the staff confirmation. Renders server-side from
        // the student, stores the rendered text, sends synchronously, records
        // Sent/Failed. A failure is persisted (not thrown) and stays retryable.
        [HttpPost("{id}/communications")]
        public async Task<ActionResult<CaseCommunication>> SendCommunication(int id, SendCommunicationDto dto)
        {
            var c = await _context.InterventionCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "Cas introuvable." });

            if (!EmailTemplates.All.TryGetValue(dto.TemplateId, out var template))
                return BadRequest(new { message = "Modèle d'email inconnu." });

            var etu = await _context.Etudiants.AsNoTracking().FirstOrDefaultAsync(e => e.Id == c.EtudiantId);
            if (etu == null) return BadRequest(new { message = "Étudiant introuvable." });
            if (string.IsNullOrWhiteSpace(etu.Email))
                return BadRequest(new { message = "L'étudiant n'a pas d'adresse email." });

            var (userId, userName) = CurrentUser();
            var (sujet, corps) = EmailTemplates.Render(template, etu.Prenom, etu.Nom, etu.Matricule);

            var comm = new CaseCommunication
            {
                CaseId       = id,
                Destinataire = etu.Email!,
                TemplateId   = template.Id,
                Sujet        = sujet,
                Corps        = corps,
                Status       = "Queued",
                CreeParId    = userId,
            };
            _context.CaseCommunications.Add(comm);
            await _context.SaveChangesAsync(); // persist Queued before attempting send

            await TrySendAsync(comm, userId, userName);
            return CreatedAtAction(nameof(GetCommunications), new { id }, comm);
        }

        // POST: api/intervention-cases/5/communications/9/retry
        // Re-sends the STORED subject/body (never re-renders) so history is intact.
        [HttpPost("{id}/communications/{commId}/retry")]
        public async Task<IActionResult> RetryCommunication(int id, int commId)
        {
            var comm = await _context.CaseCommunications.FirstOrDefaultAsync(cc => cc.Id == commId && cc.CaseId == id);
            if (comm == null) return NotFound(new { message = "Communication introuvable." });
            if (comm.Status != "Failed")
                return BadRequest(new { message = "Seules les communications échouées peuvent être renvoyées." });

            var (userId, userName) = CurrentUser();
            await TrySendAsync(comm, userId, userName);
            return NoContent();
        }

        // Attempts the SMTP send and persists the outcome + a timeline event.
        private async Task TrySendAsync(CaseCommunication comm, int? userId, string userName)
        {
            var (ok, error) = await _email.SendAsync(comm.Destinataire, comm.Sujet, comm.Corps);
            comm.Status = ok ? "Sent" : "Failed";
            comm.Erreur = error;
            if (ok) comm.EnvoyeLe = DateTime.UtcNow;

            _context.CaseTimelineEvents.Add(new CaseTimelineEvent
            {
                CaseId = comm.CaseId,
                Action = ok ? "EmailSent" : "EmailFailed",
                Description = ok ? comm.Sujet : $"Échec : {comm.Sujet}",
                UtilisateurId = userId, UtilisateurNom = userName,
            });
            await _context.SaveChangesAsync();
        }
    }
}
