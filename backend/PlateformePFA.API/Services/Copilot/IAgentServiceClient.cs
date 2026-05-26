namespace PlateformePFA.API.Services.Copilot
{
    /// <summary>
    /// Talks to agent-service over HTTP on the pfa_internal_ml network.
    /// StreamAsync yields raw SSE byte-chunks suitable for re-emission to the
    /// browser unchanged. We deliberately do NOT parse/re-serialize events
    /// here — that would force this layer to know about every event type
    /// (token/tool_call/tool_result/...) which evolves in P1.B. Pure passthrough.
    /// </summary>
    public interface IAgentServiceClient
    {
        IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            DTOs.Copilot.AgentRunRequest request,
            CancellationToken cancellationToken);
    }
}
