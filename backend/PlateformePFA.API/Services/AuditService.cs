using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action,
                      string entityType = "",
                      int? entityId = null,
                      string? message = null,
                      int? userId = null,
                      string? userName = null);
    }

    /// <summary>
    /// Writes a row to AuditEntries. Failures are swallowed and logged —
    /// audit must never block the caller's primary operation.
    ///
    /// Uses its own scoped <see cref="AppDbContext"/> rather than sharing the
    /// caller's: a failed SaveChanges in the controller would otherwise leave
    /// the context in a tracked-but-errored state, and the audit write would
    /// inherit the failure (or replay the broken tracked entities).
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IServiceScopeFactory scopeFactory, ILogger<AuditService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        public async Task LogAsync(
            string action,
            string entityType = "",
            int? entityId = null,
            string? message = null,
            int? userId = null,
            string? userName = null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.AuditEntries.Add(new AuditEntry
                {
                    UtilisateurId  = userId,
                    UtilisateurNom = userName ?? "Système",
                    Action         = action,
                    EntityType     = entityType ?? string.Empty,
                    EntityId       = entityId,
                    Message        = message,
                    CreeLe         = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed: {Action}", action);
            }
        }
    }
}
