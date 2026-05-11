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
    /// </summary>
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(AppDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger  = logger;
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
                _context.AuditEntries.Add(new AuditEntry
                {
                    UtilisateurId  = userId,
                    UtilisateurNom = userName ?? "Système",
                    Action         = action,
                    EntityType     = entityType ?? string.Empty,
                    EntityId       = entityId,
                    Message        = message,
                    CreeLe         = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log write failed: {Action}", action);
            }
        }
    }
}
