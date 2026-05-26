using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Copilot;
using PlateformePFA.API.Services.Copilot;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class CopilotControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public CopilotControllerTests(TestWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Chat_without_auth_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            "/api/copilot/chat",
            new { message = "Bonjour" });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_with_empty_message_returns_400()
    {
        _factory.SeedAdmin();
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/chat",
            new { message = "" });
        // [Required] on Message triggers model validation -> 400.
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_happy_path_proxies_sse_and_persists_assistant_turn()
    {
        // Replace the real AgentServiceClient with a scripted mock that emits
        // a deterministic SSE stream — no network, no real NIM call.
        var scriptedSse =
            "event: token\ndata: {\"text\": \"Bon\"}\n\n" +
            "event: token\ndata: {\"text\": \"jour\"}\n\n" +
            "event: token\ndata: {\"text\": \" !\"}\n\n" +
            "event: done\ndata: {\"tokens_in\": 5, \"tokens_out\": 4, \"latency_ms\": 42}\n\n";

        // Seed against the underlying TestWebFactory (instance-level DbName is
        // reused by WithWebHostBuilder's wrapped factory).
        _factory.SeedAdmin();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var existing = services.Single(d => d.ServiceType == typeof(IAgentServiceClient));
                services.Remove(existing);
                services.AddSingleton<IAgentServiceClient>(_ => new ScriptedAgentServiceClient(scriptedSse));
            });
        });

        var client = factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/chat",
            new CopilotChatRequest { Message = "Bonjour" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("event: token");
        body.Should().Contain("\"text\": \"Bon\"");
        body.Should().Contain("\"text\": \"jour\"");
        body.Should().Contain("event: done");

        // Verify the assistant turn was persisted by the SSE tee (H2 fix).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assistantMsg = await db.AgentSessionMessages
            .Where(m => m.Role == "assistant")
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        assistantMsg.Should().NotBeNull();
        assistantMsg!.ContentJson.Should().Contain("Bonjour !");

        var userMsg = await db.AgentSessionMessages
            .Where(m => m.Role == "user")
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();
        userMsg.Should().NotBeNull();
        userMsg!.TurnIndex.Should().BeLessThan(assistantMsg.TurnIndex);
    }

    /// <summary>
    /// Test double for IAgentServiceClient: emits a scripted SSE byte stream
    /// in chunks small enough to exercise SseParser's cross-chunk reassembly.
    /// </summary>
    private sealed class ScriptedAgentServiceClient : IAgentServiceClient
    {
        private readonly byte[] _payload;

        public ScriptedAgentServiceClient(string sseBody)
            => _payload = Encoding.UTF8.GetBytes(sseBody);

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
            AgentRunRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            System.Threading.CancellationToken cancellationToken)
        {
            // Emit in 7-byte chunks so event boundaries and UTF-8 sequences
            // can split across chunks — proves SseParser handles it.
            const int chunkSize = 7;
            for (int i = 0; i < _payload.Length; i += chunkSize)
            {
                var slice = _payload.AsMemory(i, Math.Min(chunkSize, _payload.Length - i));
                yield return slice;
                await Task.Yield();
            }
        }
    }
}
