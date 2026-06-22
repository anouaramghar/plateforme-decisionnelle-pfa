using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace PlateformePFA.Tests.Controllers
{
    public class CopilotSessionControllerTests : IClassFixture<TestWebFactory>
    {
        private readonly TestWebFactory _factory;

        public CopilotSessionControllerTests(TestWebFactory factory)
        {
            _factory = factory;
            _factory.SeedAdmin();
        }

        [Fact]
        public async Task CreateSession_without_authorization_returns_401()
        {
            var client = _factory.CreateClient();
            var response = await client.PostAsync("/api/copilot/session", null);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CreateSession_with_authorization_returns_200_and_sets_cookie()
        {
            var client = _factory.CreateClient();
            var token = await AuthHelper.GetAdminTokenAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync("/api/copilot/session", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert pfa_copilot_session cookie is set
            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            cookies.Should().NotBeNull();
            cookies!.Any(c => c.Contains("pfa_copilot_session=")).Should().BeTrue();
        }

        [Fact]
        public async Task DeleteSession_with_authorization_returns_200_and_clears_cookie()
        {
            var client = _factory.CreateClient();
            var token = await AuthHelper.GetAdminTokenAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.DeleteAsync("/api/copilot/session");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            cookies.Should().NotBeNull();
            cookies!.Any(c => c.Contains("pfa_copilot_session=")).Should().BeTrue();
        }
    }
}
