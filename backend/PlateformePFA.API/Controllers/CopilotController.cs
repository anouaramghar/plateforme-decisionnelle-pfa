using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Copilot;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services.Copilot;

namespace PlateformePFA.API.Controllers
{
    [ApiController]
    [Route("api/copilot")]
    // Copilot is a decision-support tool for staff. Restrict to the roles that
    // own student outcomes — not every authenticated principal.
    [Authorize(Roles = "Admin,Responsable")]
    public class CopilotController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly AppDbContext _db;
        private readonly IAgentServiceClient _agent;
        private readonly ILogger<CopilotController> _log;

        public CopilotController(
            AppDbContext db,
            IAgentServiceClient agent,
            ILogger<CopilotController> log)
        {
            _db = db;
            _agent = agent;
            _log = log;
        }

        /// <summary>
        /// POST /api/copilot/chat — open a streamed turn against the agent.
        /// Returns text/event-stream (SSE). Each event is one of:
        /// token | tool_call | tool_result | confirm_request | safety_block | done | error.
        /// See spec §4.2.
        /// </summary>
        [HttpPost("chat")]
        public async Task ChatAsync(
            [FromBody] CopilotChatRequest body,
            CancellationToken ct)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId))
            {
                Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Responsable";

            // 1. Resolve or create the AgentSession. A brand-new session is NOT
            //    persisted yet — it (and the turn) are written only once the turn
            //    completes, so an abandoned/disconnected first turn leaves nothing
            //    behind.
            AgentSession session;
            if (body.SessionId is Guid existingId)
            {
                var found = await _db.AgentSessions
                    .FirstOrDefaultAsync(
                        s => s.Id == existingId && s.UserId == userId, ct);
                if (found is null)
                {
                    Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                session = found;
            }
            else
            {
                session = new AgentSession { UserId = userId };
            }

            // 2. Load conversation history (last 20 turns, oldest-first) WITHOUT
            //    persisting the new user turn. The incoming message is appended
            //    in-memory; both turns are persisted together on completion (5).
            var lastTurnIndex = await _db.AgentSessionMessages
                .Where(m => m.SessionId == session.Id)
                .Select(m => (int?)m.TurnIndex)
                .MaxAsync(ct) ?? -1;

            var recent = await _db.AgentSessionMessages
                .Where(m => m.SessionId == session.Id)
                .OrderByDescending(m => m.TurnIndex)
                .Take(20)
                .Select(m => m.ContentJson)
                .ToListAsync(ct);
            // recent is newest-first; flip back to oldest-first for the agent.
            var messages = ((IEnumerable<string>)recent).Reverse()
                .Select(json => JsonSerializer.Deserialize<AgentChatMessage>(json, JsonOptions)!)
                .Where(m => m != null)
                .ToList();

            // Append the new user message in-memory only.
            messages.Add(new AgentChatMessage { Role = "user", Content = body.Message });

            // 3. Build the agent request. Parse the bearer token scheme-aware so
            //    a token never gets mangled and a non-"Bearer " scheme is ignored.
            var authHeader = Request.Headers.Authorization.ToString();
            var jwt = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : null;

            var agentReq = new AgentRunRequest
            {
                TraceId = HttpContext.TraceIdentifier,
                UserCtx = new AgentUserContext
                {
                    UserId      = userId,
                    Role        = role,
                    Jwt         = jwt,
                    PageContext = body.PageContext,
                },
                Messages = messages,
            };

            // 4. Proxy the SSE stream byte-for-byte to the browser, while
            //    tee'ing it through SseParser to capture the assistant's
            //    text for persistence.
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var parser = new SseParser();
            var assistantText = new StringBuilder();
            var doneSeen = false;

            try
            {
                await foreach (var chunk in _agent.StreamAsync(agentReq, ct))
                {
                    // Pass through to the browser first — never block the user
                    // on our local bookkeeping.
                    await Response.Body.WriteAsync(chunk, ct);
                    await Response.Body.FlushAsync(ct);

                    // Tee — parse what we just wrote to extract assistant text.
                    foreach (var ev in parser.Feed(chunk.Span))
                    {
                        if (ev.Name == "token")
                        {
                            // data is a JSON object with a "text" string field.
                            try
                            {
                                using var doc = JsonDocument.Parse(ev.Data);
                                if (doc.RootElement.TryGetProperty("text", out var t)
                                    && t.ValueKind == JsonValueKind.String)
                                {
                                    assistantText.Append(t.GetString());
                                }
                            }
                            catch (JsonException)
                            {
                                // Don't fail the proxy on a malformed token event;
                                // just skip it for persistence purposes.
                            }
                        }
                        else if (ev.Name == "done")
                        {
                            doneSeen = true;
                        }
                    }
                }

                // 5. Persist the completed turn — user + assistant together — only
                //    if we saw a done event with some assistant text. If the stream
                //    errored before done, or the client disconnected mid-stream,
                //    nothing is written (no dangling user-only turns).
                if (doneSeen && assistantText.Length > 0)
                {
                    await PersistTurnAsync(
                        session, lastTurnIndex, body.Message, assistantText.ToString(), ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected; nothing persisted for this turn.
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "copilot chat failed for session {SessionId}", session.Id);

                // L5: use JsonSerializer instead of string-interpolation so that
                // any future change (e.g. logging ex.Message) cannot inject into
                // the JSON payload.
                var errorJson = JsonSerializer.Serialize(new
                {
                    message = ex.GetType().Name,
                });
                await Response.WriteAsync($"event: error\ndata: {errorJson}\n\n", ct);
            }
        }

        /// <summary>
        /// POST /api/copilot/confirm — promote a pending AlertDraft to a real Alerte.
        /// Called by the user clicking "Confirmer" in the chat panel after seeing
        /// a confirm_request SSE event. Draft TTL is enforced server-side.
        /// </summary>
        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmAsync(
            [FromBody] ConfirmBody body,
            CancellationToken ct)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var draft = await _db.AlertDrafts
                .FirstOrDefaultAsync(
                    d => d.Id == body.DraftId
                      && d.CreatedBy == userId
                      && d.Status == "pending_user_confirm"
                      && d.ExpiresAt > DateTime.UtcNow,
                    ct);

            if (draft is null)
                return BadRequest(new { ok = false, error = "draft not found, expired, or not owned by you" });

            // Map draft severity to Alerte.Niveau
            var niveau = draft.Severity switch
            {
                "low"    => "Faible",
                "medium" => "Moyen",
                "high"   => "Eleve",
                _        => "Moyen",
            };

            var alerte = new Alerte
            {
                EtudiantId = draft.StudentId,
                Type       = "RisqueEchec",
                Niveau     = niveau,
                Message    = draft.MessageFr,
                Resolue    = false,
                CreeLe     = DateTime.UtcNow,
            };
            _db.Alertes.Add(alerte);

            draft.Status = "sent";
            draft.SentAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return Ok(new { ok = true, data = new { alerte_id = alerte.Id } });
        }

        public class ConfirmBody
        {
            public int DraftId { get; set; }
        }

        /// <summary>
        /// Persist the user + assistant turns of one completed exchange in a single
        /// SaveChanges. Lazily inserts a brand-new session row here (rather than up
        /// front) so abandoned turns leave no orphan session.
        ///
        /// TurnIndex is derived from MAX(TurnIndex)+1 and guarded by a UNIQUE index
        /// on (SessionId, TurnIndex). Two near-simultaneous turns on the same session
        /// can compute the same indices and collide; on a unique violation we
        /// recompute and retry a few times before giving up.
        /// </summary>
        private async Task PersistTurnAsync(
            AgentSession session,
            int lastTurnIndex,
            string userMessage,
            string assistantMessage,
            CancellationToken ct)
        {
            if (_db.Entry(session).State == EntityState.Detached)
                _db.AgentSessions.Add(session);
            session.LastActivityAt = DateTime.UtcNow;

            var userTurn = new AgentSessionMessage
            {
                SessionId   = session.Id,
                Role        = "user",
                ContentJson = JsonSerializer.Serialize(new { role = "user", content = userMessage }),
            };
            var assistantTurn = new AgentSessionMessage
            {
                SessionId   = session.Id,
                Role        = "assistant",
                ContentJson = JsonSerializer.Serialize(new { role = "assistant", content = assistantMessage }),
            };
            _db.AgentSessionMessages.Add(userTurn);
            _db.AgentSessionMessages.Add(assistantTurn);

            const int maxAttempts = 4;
            for (int attempt = 1; ; attempt++)
            {
                userTurn.TurnIndex      = lastTurnIndex + 1;
                assistantTurn.TurnIndex = lastTurnIndex + 2;
                try
                {
                    await _db.SaveChangesAsync(ct);
                    return;
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    // A concurrent turn grabbed these indices. Recompute the high
                    // water mark from the DB and retry with fresh indices.
                    lastTurnIndex = await _db.AgentSessionMessages
                        .Where(m => m.SessionId == session.Id)
                        .Select(m => (int?)m.TurnIndex)
                        .MaxAsync(ct) ?? -1;
                }
            }
        }
    }
}
