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

    // ── /api/copilot/confirm tests ─────────────────────────────────────────────

    [Fact]
    public async Task Confirm_happy_path_creates_alerte_and_flips_draft_status()
    {
        _factory.SeedAdmin();
        using (var ctx = _factory.CreateContext()) PlateformePFA.Tests.Fixtures.SampleData.SeedOne(ctx);

        // Authenticate first to get the actual userId from the JWT-issuing admin.
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Seed a draft using the same admin ID that the token will carry.
        int draftId;
        {
            using var ctx = _factory.CreateContext();
            var admin = ctx.Utilisateurs.First(u => u.Email == "test-admin@eniad.ma");
            var etu   = ctx.Etudiants.First(e => e.Matricule == "E10001");
            var draft = new PlateformePFA.API.Models.AlertDraft
            {
                StudentId = etu.Id,
                Severity  = "high",
                MessageFr = "Test draft alert confirm.",
                CreatedBy = admin.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Status    = "pending_user_confirm",
            };
            ctx.AlertDrafts.Add(draft);
            ctx.SaveChanges();
            draftId = draft.Id;
        }

        var res = await client.PostAsJsonAsync("/api/copilot/confirm", new { draftId });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("\"alerte_id\"");

        // Verify Alerte was created with correct severity mapping (high → Eleve).
        using var verifyCtx = _factory.CreateContext();
        var alerte = verifyCtx.Alertes
            .Where(a => a.Message == "Test draft alert confirm.")
            .OrderByDescending(a => a.Id)
            .FirstOrDefault();
        alerte.Should().NotBeNull();
        alerte!.Niveau.Should().Be("Eleve");
        alerte.Type.Should().Be("RisqueEchec");

        // Verify draft status flipped to "sent".
        var draft2 = verifyCtx.AlertDrafts.Find(draftId);
        draft2!.Status.Should().Be("sent");
        draft2.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirm_expired_draft_returns_bad_request()
    {
        _factory.SeedAdmin();
        using (var ctx = _factory.CreateContext()) PlateformePFA.Tests.Fixtures.SampleData.SeedOne(ctx);

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        int draftId;
        {
            using var ctx = _factory.CreateContext();
            var admin   = ctx.Utilisateurs.First(u => u.Email == "test-admin@eniad.ma");
            var etu     = ctx.Etudiants.First(e => e.Matricule == "E10001");
            var expired = new PlateformePFA.API.Models.AlertDraft
            {
                StudentId = etu.Id, Severity = "low", MessageFr = "Expired draft.",
                CreatedBy = admin.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
                Status    = "pending_user_confirm",
            };
            ctx.AlertDrafts.Add(expired);
            ctx.SaveChanges();
            draftId = expired.Id;
        }

        var res = await client.PostAsJsonAsync("/api/copilot/confirm", new { draftId });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirm_double_confirm_second_call_returns_bad_request()
    {
        _factory.SeedAdmin();
        using (var ctx = _factory.CreateContext()) PlateformePFA.Tests.Fixtures.SampleData.SeedOne(ctx);

        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        int draftId;
        {
            using var ctx = _factory.CreateContext();
            var admin = ctx.Utilisateurs.First(u => u.Email == "test-admin@eniad.ma");
            var etu   = ctx.Etudiants.First(e => e.Matricule == "E10001");
            var draft = new PlateformePFA.API.Models.AlertDraft
            {
                StudentId = etu.Id, Severity = "medium", MessageFr = "Double confirm test msg.",
                CreatedBy = admin.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Status    = "pending_user_confirm",
            };
            ctx.AlertDrafts.Add(draft);
            ctx.SaveChanges();
            draftId = draft.Id;
        }

        var first  = await client.PostAsJsonAsync("/api/copilot/confirm", new { draftId });
        var second = await client.PostAsJsonAsync("/api/copilot/confirm", new { draftId });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var ctx2 = _factory.CreateContext();
        ctx2.Alertes.Count(a => a.Message == "Double confirm test msg.").Should().Be(1);
    }

    [Fact]
    public async Task Confirm_without_auth_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/copilot/confirm", new { draftId = 1 });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
