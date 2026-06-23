using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Escalates Critical, past-due, still-active cases to the Escalated state
    /// and reassigns them to an Admin. Idempotent: an already-escalated case is
    /// filtered out, so repeated sweeps are no-ops.
    /// </summary>
    public class CaseEscalationService
    {
        private readonly AppDbContext _db;
        private readonly IAuditService _audit;
        private readonly ILogger<CaseEscalationService> _logger;

        public CaseEscalationService(AppDbContext db, IAuditService audit, ILogger<CaseEscalationService> logger)
        {
            _db = db; _audit = audit; _logger = logger;
        }

        public async Task<int> SweepAsync(DateTime now, CancellationToken ct = default)
        {
            var candidates = await _db.InterventionCases
                .Where(c => c.Priorite == "Critical"
                    && c.Etat != CaseWorkflowState.Resolved
                    && c.Etat != CaseWorkflowState.Closed
                    && c.Etat != CaseWorkflowState.Escalated
                    && c.DueDate != null && c.DueDate < now)
                .ToListAsync(ct);
            if (candidates.Count == 0) return 0;

            var adminId = await _db.Utilisateurs
                .Where(u => u.EstActif && u.Role == "Admin")
                .OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);

            foreach (var c in candidates)
            {
                var from = c.Etat;
                c.Etat = CaseWorkflowState.Escalated;
                c.EscaladeLe = now;
                if (adminId != null) c.OwnerId = adminId;
                _db.CaseTimelineEvents.Add(new CaseTimelineEvent
                {
                    CaseId = c.Id, Action = "Escalated",
                    Description = $"Escalade automatique : cas critique en retard ({from} → Escalated).",
                    UtilisateurNom = "Système",
                });
            }
            await _db.SaveChangesAsync(ct);

            foreach (var c in candidates)
                await _audit.LogAsync("EscalateCase", "InterventionCase", c.Id,
                    "Escalade automatique (cas critique en retard)", null, "Système");

            _logger.LogInformation("Escalade automatique : {Count} cas escaladé(s).", candidates.Count);
            return candidates.Count;
        }
    }
}