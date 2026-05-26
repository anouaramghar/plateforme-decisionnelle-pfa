using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using PlateformePFA.API.DTOs.Copilot;

namespace PlateformePFA.API.Services.Copilot
{
    public class AgentServiceClient : IAgentServiceClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<AgentServiceClient> _log;
        private readonly string _internalToken;

        public AgentServiceClient(
            HttpClient http,
            IConfiguration config,
            ILogger<AgentServiceClient> log)
        {
            _http = http;
            _log = log;
            _internalToken = config["ML_INTERNAL_TOKEN"]
                ?? throw new InvalidOperationException(
                    "ML_INTERNAL_TOKEN missing — required to authenticate to agent-service");
        }

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            AgentRunRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/agent/run")
            {
                Content = JsonContent.Create(request),
            };
            req.Headers.Add("X-Internal-Token", _internalToken);
            req.Headers.Accept.ParseAdd("text/event-stream");

            using var resp = await _http.SendAsync(
                req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _log.LogError("agent-service returned {Status}: {Body}", resp.StatusCode, body);
                throw new HttpRequestException(
                    $"agent-service returned {(int)resp.StatusCode}: {body}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                // Copy the slice we actually read so the buffer can be reused.
                var chunk = new byte[read];
                Buffer.BlockCopy(buffer, 0, chunk, 0, read);
                yield return chunk;
            }
        }
    }
}
