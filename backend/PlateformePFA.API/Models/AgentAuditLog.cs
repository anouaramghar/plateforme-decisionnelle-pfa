using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// Row written for every LLM call, tool call, safety block, confirmation,
    /// and rejection by the Copilot. Payload is JSON, PII-redacted before
    /// persist. Separate from AuditEntry (which logs domain actions) because
    /// the schema is much richer (model, tokens, latency).
    /// </summary>
    public class AgentAuditLog
    {
        public long Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }

        public Guid? SessionId { get; set; }

        // 'llm_call' | 'tool_call' | 'confirm' | 'reject' | 'safety_block'
        [Required] [MaxLength(20)]
        public string Kind { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Model { get; set; }

        [MaxLength(60)]
        public string? ToolName { get; set; }

        public int? TokensIn { get; set; }
        public int? TokensOut { get; set; }
        public int? LatencyMs { get; set; }

        public string? Payload { get; set; }   // JSON, PII-redacted

        // 'success' | 'error' | 'denied'
        [Required] [MaxLength(20)]
        public string Outcome { get; set; } = "success";

        [JsonIgnore] public Utilisateur? User { get; set; }
        [JsonIgnore] public AgentSession? Session { get; set; }
    }
}
