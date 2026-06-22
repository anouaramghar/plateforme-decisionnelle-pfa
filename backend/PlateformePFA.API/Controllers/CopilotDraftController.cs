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
    /// Implementation note: confirm CLAIMS the draft with a conditional update
    /// (ExecuteUpdateAsync on SQL Server) so concurrent confirmations cannot
    /// both create an Alerte — only one request flips pending_user_confirm.
    /// EF Core InMemory lacks ExecuteUpdateAsync, so the test path emulates the
    /// same conditional claim with fetch → mutate inside the transaction.
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

            var now = DateTime.UtcNow;

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Atomically CLAIM the draft: flip pending_user_confirm -> sent
                // ONLY if it is still pending and unexpired. Under concurrency
                // exactly one request updates a row; the loser updates 0 and
                // gets 409. This closes the read-then-write race where two
                // simultaneous confirms both passed the pending check and each
                // created an Alerte.
                int claimed;
                if (_db.Database.IsRelational())
                {
                    claimed = await _db.AlertDrafts
                        .Where(d => d.Id == id
                            && d.Status == "pending_user_confirm"
                            && d.ExpiresAt > now)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(d => d.Status, "sent")
                            .SetProperty(d => d.SentAt, now), ct);
                }
                else
                {
                    // EF Core InMemory (tests) has no ExecuteUpdateAsync and no
                    // real concurrency; emulate the conditional claim.
                    var pending = await _db.AlertDrafts.FindAsync(new object[] { id }, ct);
                    if (pending is not null
                        && pending.Status == "pending_user_confirm"
                        && pending.ExpiresAt > now)
                    {
                        pending.Status = "sent";
                        pending.SentAt = now;
                        await _db.SaveChangesAsync(ct);
                        claimed = 1;
                    }
                    else
                    {
                        claimed = 0;
                    }
                }

                if (claimed != 1)
                {
                    await tx.RollbackAsync(ct);
                    return Conflict(new { message = "Draft is expired, cancelled, or already confirmed." });
                }

                // Re-read the claimed draft for the fields needed to build the Alerte.
                var draft = await _db.AlertDrafts.AsNoTracking().FirstAsync(d => d.Id == id, ct);

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
