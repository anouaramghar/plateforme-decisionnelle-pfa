using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.Tests;

/// <summary>
/// Hosts the API in-process backed by EF Core InMemory so each test class
/// gets an isolated DB. The real RuntimeMigrations + DataSeeder don't run —
/// we never touch SQL Server here. Tests own their own seed data.
/// </summary>
public class TestWebFactory : WebApplicationFactory<Program>
{
    // Instance-level (not static) so each test class that constructs its own
    // TestWebFactory gets a fresh InMemory store. A static field would share one
    // store across the entire test run and let earlier classes leak state into
    // later ones — exactly the kind of flake xUnit's collection isolation is
    // meant to prevent.
    private readonly string DbName = $"test-{Guid.NewGuid()}";

    /// <summary>
    /// Program.cs reads JWT_SECRET / ML_INTERNAL_TOKEN BEFORE the WAF host
    /// builder runs, so ConfigureAppConfiguration is too late to inject them.
    /// We set them as process env vars before any WAF instance is created.
    /// </summary>
    static TestWebFactory()
    {
        Environment.SetEnvironmentVariable(
            "JWT_SECRET",
            "Test-JWT-Secret-With-At-Least-32-Chars-And-Digits-1234567890!");
        Environment.SetEnvironmentVariable("JWT_ISSUER",        "test");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE",      "test");
        Environment.SetEnvironmentVariable("ML_API_URL",        "http://localhost:9999");
        Environment.SetEnvironmentVariable("ML_INTERNAL_TOKEN", "test-token-not-placeholder");
        Environment.SetEnvironmentVariable("ADMIN_SEED_EMAIL",  "ignored");
        Environment.SetEnvironmentVariable(
            "ADMIN_SEED_PASSWORD",
            "TestPassword!2026");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            // JWT_SECRET must be ≥32 chars or Program.cs refuses to boot.
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"]        = "Test-JWT-Secret-With-At-Least-32-Chars-And-Digits-1234567890!",
                ["JWT_ISSUER"]        = "test",
                ["JWT_AUDIENCE"]      = "test",
                ["ML_API_URL"]        = "http://localhost:9999",
                ["ML_INTERNAL_TOKEN"] = "x",
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["ADMIN_SEED_EMAIL"]    = "ignored",
                ["ADMIN_SEED_PASSWORD"] = "ignored-but-must-be-12-chars",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the SQL Server context with InMemory.
            var ctxDescriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(ctxDescriptor);
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(DbName));
        });
    }

    public AppDbContext CreateContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    /// <summary>
    /// Seeds an Admin user the test suite logs in as. Idempotent.
    /// </summary>
    public void SeedAdmin()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!ctx.Utilisateurs.Any(u => u.Email == "test-admin@eniad.ma"))
        {
            ctx.Utilisateurs.Add(new Utilisateur
            {
                Email = "test-admin@eniad.ma",
                Nom = "Admin",
                Prenom = "Test",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("TestPassword!2026"),
                Role = "Admin",
                EstActif = true,
            });
            ctx.SaveChanges();
        }
    }
}
