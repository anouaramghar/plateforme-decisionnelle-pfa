using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
}
