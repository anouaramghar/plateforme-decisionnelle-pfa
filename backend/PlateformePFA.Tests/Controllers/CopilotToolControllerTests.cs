using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class CopilotToolControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public CopilotToolControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        // Seeds student E10001 WITH a note (NoteFinal 12.2) + no absences. Using
        // the shared seeder avoids a note-less student, whose empty .Average()
        // projection can throw under the EF InMemory provider.
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    // The internal token the in-memory test config injects (see TestWebFactory).
    private const string InternalToken = "test-agent-token";

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Tool_without_internal_token_returns_401_even_with_jwt()
    {
        var client = await AuthedClientAsync();
        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "E10001" } });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_student_happy_path_returns_ok_envelope()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "E10001" } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("\"matricule\":\"E10001\"");
        body.Should().Contain("\"risque\":\"faible\"");  // moy 12.2, 0 absences
    }

    [Fact]
    public async Task Get_student_not_found_returns_ok_false()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "NOPE-9999" } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":false");
    }

    [Fact]
    public async Task Unknown_tool_returns_ok_false()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/does_not_exist",
            new { args = new { } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"ok\":false");
    }

    // ── list_at_risk tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task List_at_risk_threshold_zero_returns_all_students()
    {
        using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/list_at_risk",
            new { args = new { threshold = 0.0 } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("\"students\"");
        body.Should().Contain("E10001");
        body.Should().Contain("E10002");
    }

    [Fact]
    public async Task List_at_risk_high_threshold_filters_out_low_risk_students()
    {
        using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        // 0.60 threshold: only the high-risk student (E10002, moy 6.0 + 40h abs)
        // E10001 (moy 12.2, 0 abs, score ~0.12) must be absent.
        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/list_at_risk",
            new { args = new { threshold = 0.60 } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("E10002");
        body.Should().NotContain("E10001");
    }

    [Fact]
    public async Task List_at_risk_filiere_filter_scopes_results()
    {
        using (var ctx = _factory.CreateContext()) SampleData.SeedHighRisk(ctx);

        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/list_at_risk",
            new { args = new { threshold = 0.0, filiere = "GI" } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("\"students\"");
    }

    [Fact]
    public async Task List_at_risk_invalid_threshold_returns_ok_false()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/list_at_risk",
            new { args = new { threshold = 1.5 } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"ok\":false");
    }
}
