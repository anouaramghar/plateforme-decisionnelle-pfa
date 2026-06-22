using System.Net.Http.Json;

namespace PlateformePFA.Tests;

public static class AuthHelper
{
    public record LoginResponse(string Token, string RefreshToken);

    public static async Task<string> GetAdminTokenAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "test-admin@eniad.ma",
            motDePasse = "TestPassword!2026",
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    public static async Task<string> GetTokenAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = email,
            motDePasse = password,
        });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }
}
