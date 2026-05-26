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
    [Authorize]
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

            // 1. Resolve or create the AgentSession.
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
                session.LastActivityAt = DateTime.UtcNow;
            }
            else
            {
                session = new AgentSession { UserId = userId };
                _db.AgentSessions.Add(session);
            }

            // 2. Persist the user's incoming turn.
            var lastTurnIndex = await _db.AgentSessionMessages
                .Where(m => m.SessionId == session.Id)
                .Select(m => (int?)m.TurnIndex)
                .MaxAsync(ct) ?? -1;

            var userTurnIndex = lastTurnIndex + 1;
            _db.AgentSessionMessages.Add(new AgentSessionMessage
            {
                SessionId   = session.Id,
                TurnIndex   = userTurnIndex,
                Role        = "user",
                ContentJson = JsonSerializer.Serialize(new
                {
                    role = "user",
                    content = body.Message,
                }),
            });

            await _db.SaveChangesAsync(ct);

            // 3. Build the agent request (last 20 turns, chronological).
            var recent = await _db.AgentSessionMessages
                .Where(m => m.SessionId == session.Id)
                .OrderByDescending(m => m.TurnIndex)
                .Take(20)
                .Select(m => m.ContentJson)
                .ToListAsync(ct);
            // recent is newest-first; flip back to oldest-first for the agent.
            var history = ((IEnumerable<string>)recent).Reverse().ToList();

            var messages = history
                .Select(json => JsonSerializer.Deserialize<AgentChatMessage>(json, JsonOptions)!)
                .Where(m => m != null)
                .ToList();

            var agentReq = new AgentRunRequest
            {
                TraceId = HttpContext.TraceIdentifier,
                UserCtx = new AgentUserContext
                {
                    UserId      = userId,
                    Role        = role,
                    Jwt         = Request.Headers.Authorization
                                      .ToString().Replace("Bearer ", string.Empty),
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

                // 5. Persist the assistant turn (if we saw at least some text).
                //    Skipped if the stream ended on an error event before done,
                //    or if the client disconnected mid-stream.
                if (doneSeen && assistantText.Length > 0)
                {
                    _db.AgentSessionMessages.Add(new AgentSessionMessage
                    {
                        SessionId   = session.Id,
                        TurnIndex   = userTurnIndex + 1,
                        Role        = "assistant",
                        ContentJson = JsonSerializer.Serialize(new
                        {
                            role    = "assistant",
                            content = assistantText.ToString(),
                        }),
                    });
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected; partial assistant text is dropped on the floor.
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
    }
}
