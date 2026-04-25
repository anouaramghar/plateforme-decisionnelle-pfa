using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PlateformePFA.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    // Splits a T-SQL script on a `GO` batch separator that sits on its own line,
    // tolerating both LF (\n) and CRLF (\r\n) endings and any trailing whitespace.
    // Avoids the `\nGO` literal split, which silently no-ops on Windows checkouts.
    private static readonly Regex GoBatchSeparator =
        new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IConfiguration config, ILogger<AdminController> logger)
    {
        _config = config;
        _logger = logger;
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

            // Change to master so cross-database references (PFA_DB.dbo.* and PFA_DW.dbo.*) resolve.
            await using var cmd = new SqlCommand("USE master", conn);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "DW sync failed");
            return StatusCode(500, new { message = "Erreur lors de la synchronisation.", detail = ex.Message });
        }
    }
}
