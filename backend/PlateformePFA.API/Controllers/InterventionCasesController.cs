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
        private readonly ILogger<InterventionCasesController> _logger;

        public InterventionCasesController(AppDbContext context, IAuditService audit, ILogger<InterventionCasesController> logger)
        {
            _context = context;
            _audit   = audit;
            _logger  = logger;
        }

        private (int? id, string name) CurrentUser()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? id = int.TryParse(idClaim, out var uid) ? uid : null;
            var name = User.FindFirstValue("NomComplet") ?? User.FindFirstValue(ClaimTypes.Email) ?? "Système";
            return (id, name);
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
                OwnerId    = dto.OwnerId,
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
    }
}
