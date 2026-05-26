using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// One turn in a Copilot conversation. Role is 'user' | 'assistant' | 'tool'.
    /// ContentJson is the raw OpenAI-style message object (includes tool_calls
    /// for assistant turns and tool_call_id + name for tool turns).
    /// </summary>
    public class AgentSessionMessage
    {
        public long Id { get; set; }

        public Guid SessionId { get; set; }

        public int TurnIndex { get; set; }

        [Required] [MaxLength(20)]
        public string Role { get; set; } = string.Empty;

        [Required]
        public string ContentJson { get; set; } = "{}";

        public int? TokensIn { get; set; }
        public int? TokensOut { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public AgentSession? Session { get; set; }
    }
}
