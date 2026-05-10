using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class AuthControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public AuthControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test-admin@eniad.ma",
            motDePasse = "TestPassword!2026",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthHelper.LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test-admin@eniad.ma",
            // Must be ≥6 chars to clear DTO validation; otherwise we get 400
            // and never exercise the password mismatch branch.
            motDePasse = "wrongpw",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            motDePasse = "anything",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_valid_token_returns_new_token()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test-admin@eniad.ma",
            motDePasse = "TestPassword!2026",
        });
        var loginBody = await login.Content.ReadFromJsonAsync<AuthHelper.LoginResponse>();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = loginBody!.RefreshToken,
        });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await refresh.Content.ReadFromJsonAsync<AuthHelper.LoginResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }
}
