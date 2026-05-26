using System.Security.Claims;
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

            _db.AgentSessionMessages.Add(new AgentSessionMessage
            {
                SessionId   = session.Id,
                TurnIndex   = lastTurnIndex + 1,
                Role        = "user",
                ContentJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    role = "user",
                    content = body.Message,
                }),
            });

            await _db.SaveChangesAsync(ct);

            // 3. Build the agent request (last 20 turns).
            var historyDesc = await _db.AgentSessionMessages
                .Where(m => m.SessionId == session.Id)
                .OrderByDescending(m => m.TurnIndex)
                .Take(20)
                .Select(m => m.ContentJson)
                .ToListAsync(ct);
            historyDesc.Reverse();  // oldest -> newest

            var messages = historyDesc
                .Select(json => System.Text.Json.JsonSerializer.Deserialize<AgentChatMessage>(
                    json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    })!)
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

            // 4. Proxy the SSE stream byte-for-byte to the browser.
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                await foreach (var chunk in _agent.StreamAsync(agentReq, ct))
                {
                    await Response.Body.WriteAsync(chunk, ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected; nothing to clean up.
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "copilot chat failed for session {SessionId}", session.Id);
                var errorPayload =
                    $"event: error\ndata: {{\"message\": \"{ex.GetType().Name}\"}}\n\n";
                await Response.WriteAsync(errorPayload, ct);
            }
        }
    }
}
