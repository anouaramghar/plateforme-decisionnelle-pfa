using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Copilot
{
    /// <summary>
    /// POST body for /api/copilot/chat. SessionId may be null on the first
    /// turn — the controller creates a new AgentSession in that case.
    /// </summary>
    public class CopilotChatRequest
    {
        public Guid? SessionId { get; set; }

        [Required] [MaxLength(4000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Free-form JSON describing the current frontend page (route, IDs in view).
        /// Auto-injected into the agent's system prompt. See spec §4.5.
        /// </summary>
        public Dictionary<string, object>? PageContext { get; set; }
    }
}
