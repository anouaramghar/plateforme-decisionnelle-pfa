using System.Runtime.CompilerServices;

namespace PlateformePFA.Tests;

/// <summary>
/// Sets the environment variables Program.cs validates BEFORE any test or
/// WebApplicationFactory runs. ModuleInitializer fires once per test assembly
/// load, which is earlier than any static constructor on individual fixtures.
///
/// Why we can't use ConfigureAppConfiguration: Program.cs reads JWT_SECRET in
/// its top-level statements, which run before WebApplicationFactory's host
/// builder gets a chance to layer in test config.
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        SetIfMissing("JWT_SECRET",        "Test-JWT-Secret-With-At-Least-32-Chars-And-Digits-1234567890!");
        SetIfMissing("JWT_ISSUER",        "test");
        SetIfMissing("JWT_AUDIENCE",      "test");
        SetIfMissing("ML_API_URL",        "http://localhost:9999");
        SetIfMissing("ML_INTERNAL_TOKEN", "test-token-not-placeholder");
        SetIfMissing("ADMIN_SEED_EMAIL",  "ignored");
        SetIfMissing("ADMIN_SEED_PASSWORD", "TestPassword!2026");
    }

    private static void SetIfMissing(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
