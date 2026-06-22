using Microsoft.Extensions.Diagnostics.HealthChecks;
using PlateformePFA.API.Data;
using Microsoft.EntityFrameworkCore;

namespace PlateformePFA.API.Health
{
    /// <summary>
    /// Readiness health check: returns Healthy only when the database is
    /// reachable AND the full OLTP schema has been initialised
    /// (dbo.SchemaState contains a row with Component='core' and Version>=1,
    /// written at the very end of database/init.sql).
    ///
    /// Registered under the "ready" tag → exposed via GET /ready.
    /// The plain GET /health liveness probe is NOT affected.
    /// </summary>
    public class DatabaseReadyHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _db;

        public DatabaseReadyHealthCheck(AppDbContext db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken ct = default)
        {
            try
            {
                if (!await _db.Database.CanConnectAsync(ct))
                    return HealthCheckResult.Unhealthy("Cannot connect to database.");

                var schemaReady = await _db.SchemaStates
                    .AnyAsync(s => s.Component == "core" && s.Version >= 1, ct);

                if (!schemaReady)
                    return HealthCheckResult.Unhealthy(
                        "Schema not ready: dbo.SchemaState marker absent or version < 1.");

                return HealthCheckResult.Healthy("Database connected and schema ready.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Health check failed: {ex.Message}");
            }
        }
    }
}
