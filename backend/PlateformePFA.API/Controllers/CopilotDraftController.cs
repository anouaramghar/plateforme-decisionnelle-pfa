using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    /// <summary>
    /// Human-in-the-loop confirmation surface for Copilot-drafted alerts.
    /// The Copilot can only draft (pending_user_confirm); the human must hit
    /// one of these endpoints to promote a draft to a real Alerte or cancel it.
    ///
    /// Route:  POST /api/copilot/drafts/{id}/confirm
    ///         POST /api/copilot/drafts/{id}/cancel
    ///
    /// Auth: same role gate as the rest of the Copilot surface — Admin or Responsable.
    /// No X-Internal-Token required: these are direct user actions, not agent tool calls.
    ///
    /// Implementation note: EF Core InMemory does NOT support ExecuteUpdateAsync
    /// (it is a relational-only bulk-update API). To stay compatible with the
    /// InMemory test provider we use the traditional fetch → mutate → SaveChanges
    /// pattern inside a transaction.  On SQL Server a transaction + row-level
    /// locking gives equivalent idempotency guarantees.
    /// </summary>
    [ApiController]
    [Route("api/copilot/drafts")]
    [Authorize(Roles = "Admin,Responsable")]
    public class CopilotDraftController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CopilotDraftController(AppDbContext db)
        {
            _db = db;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        /// <summary>
        /// Confirm a pending draft: atomically marks it "sent" and creates a real Alerte.
        /// Returns 409 Conflict if the draft is already finalized (sent/cancelled/expired).
        /// </summary>
        [HttpPost("{id}/confirm")]
        public async Task<IActionResult> ConfirmDraftAsync(int id, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Fetch the draft — track it so SaveChanges picks up mutations.
                var draft = await _db.AlertDrafts.FindAsync(new object[] { id }, ct);

                // Idempotency: only act when the draft is still pending AND not expired.
                if (draft is null
                    || draft.Status != "pending_user_confirm"
                    || draft.ExpiresAt <= DateTime.UtcNow)
                {
                    await tx.RollbackAsync(ct);
                    return Conflict(new { message = "Draft is expired, cancelled, or already confirmed." });
                }

                // Mark the draft as sent.
                draft.Status = "sent";
                draft.SentAt = DateTime.UtcNow;

                // Map severity → Niveau (matches the Alerte.Niveau domain values).
                var niveau = draft.Severity switch
                {
                    "high"   => "Eleve",
                    "medium" => "Moyen",
                    _        => "Faible",   // "low" and any future values fall here
                };

                _db.Alertes.Add(new Alerte
                {
                    EtudiantId = draft.StudentId,
                    Type       = "RisqueEchec",
                    Niveau     = niveau,
                    Message    = draft.MessageFr,
                    Resolue    = false,
                    CreeLe     = DateTime.UtcNow,
                });

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Ok(new { message = "Alert confirmed and sent." });
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        /// <summary>
        /// Cancel a pending draft. Returns 409 if already finalized.
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelDraftAsync(int id, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var draft = await _db.AlertDrafts.FindAsync(new object[] { id }, ct);

            if (draft is null || draft.Status != "pending_user_confirm")
                return Conflict(new { message = "Draft not found or already finalized." });

            draft.Status = "cancelled";
            await _db.SaveChangesAsync(ct);

            return Ok(new { message = "Draft cancelled." });
        }
    }
}
