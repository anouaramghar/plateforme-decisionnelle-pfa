using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private static readonly Regex GoBatchSeparator =
        new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;
    private readonly IAlerteService _alerteService;

    public AdminController(IConfiguration config, ILogger<AdminController> logger, IAlerteService alerteService)
    {
        _config        = config;
        _logger        = logger;
        _alerteService = alerteService;
    }

    /// <summary>
    /// Runs the OLTP → DW ETL sync (MERGE statements in etl.sql logic).
    /// Call after bulk note imports or on a nightly schedule.
    /// </summary>
    [HttpPost("sync-dw")]
    public async Task<IActionResult> SyncDataWarehouse(CancellationToken ct)
    {
        var connStr = _config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not configured.");

        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            // The ETL script uses cross-database references (PFA_DB.dbo.* → PFA_DW.dbo.*).
            // pfa_app has read/write access on both databases so no USE master is needed —
            // USE master would fail because pfa_app has no user in master.
            await using var cmd = new SqlCommand("USE PFA_DW", conn);
            await cmd.ExecuteNonQueryAsync(ct);

            var etlSql = await System.IO.File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Scripts", "etl.sql"), ct);

            foreach (var batch in GoBatchSeparator.Split(etlSql))
            {
                var trimmed = batch.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                cmd.CommandText = trimmed;
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            _logger.LogInformation("DW sync completed successfully at {Time}", DateTime.UtcNow);
            return Ok(new { message = "Synchronisation DW terminée.", timestamp = DateTime.UtcNow });
        }
        catch (SqlException ex) when (ex.Number == 51002 || ex.Message.Contains("ETL already running"))
        {
            _logger.LogWarning("DW sync skipped: ETL lock held by another session at {Time}", DateTime.UtcNow);
            return Conflict(new { message = "ETL synchronization is already in progress. Try again later." });
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "DW sync failed (correlationId={CorrelationId})", correlationId);
            return StatusCode(500, new { message = "Erreur interne.", correlationId });
        }
    }

    /// <summary>
    /// Scans all students and generates missing NoteFaible / AbsenceExcessive alerts.
    /// Call once after seeding or bulk imports.
    /// </summary>
    [HttpPost("generate-alerts")]
    public async Task<IActionResult> GenerateAlerts()
    {
        try
        {
            var (notes, absences) = await _alerteService.ScanAllAsync();
            return Ok(new
            {
                message  = $"Scan terminé : {notes} alerte(s) NoteFaible, {absences} alerte(s) AbsenceExcessive créées.",
                notes,
                absences,
                timestamp = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid();
            _logger.LogError(ex, "generate-alerts failed (correlationId={CorrelationId})", correlationId);
            return StatusCode(500, new { message = "Erreur interne.", correlationId });
        }
    }
}
