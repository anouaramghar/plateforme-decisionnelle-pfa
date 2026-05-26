using System.Text.Json.Serialization;

namespace PlateformePFA.API.DTOs.Copilot
{
    // [JsonPropertyName] attributes are critical: agent-service's pydantic
    // models use snake_case (trace_id, user_ctx, tool_calls), but ASP.NET
    // Core's default JsonContent.Create emits camelCase. Without these
    // attributes, pydantic rejects the body with a 422.

    public class AgentRunRequest
    {
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; } = string.Empty;

        [JsonPropertyName("user_ctx")]
        public AgentUserContext UserCtx { get; set; } = new();

        [JsonPropertyName("messages")]
        public List<AgentChatMessage> Messages { get; set; } = new();
    }

    public class AgentUserContext
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("jwt")]
        public string? Jwt { get; set; }

        [JsonPropertyName("page_context")]
        public Dictionary<string, object>? PageContext { get; set; }
    }

    public class AgentChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<object>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
