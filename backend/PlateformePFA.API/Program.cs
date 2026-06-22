using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Health;
using PlateformePFA.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Text;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Test runs (xUnit + WebApplicationFactory<Program>) host the API in-process
// and inject configuration AFTER the top-level statements run, so the strict
// validation below would always trip. Detect the Testing environment and use
// hardcoded test-only stand-ins instead. Production paths are unchanged.
var isTestEnv = string.Equals(
    builder.Environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

// Fail-fast: refuse to start if JWT secrets are missing, too weak, or placeholder.
// Reads JWT_SECRET env var first (Docker), falls back to Jwt:Key (appsettings dev).
var jwtSecret   = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Key"];
var jwtIssuer   = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"];

if (isTestEnv)
{
    jwtSecret   ??= "Test-JWT-Secret-With-At-Least-32-Chars-And-Digits-1234567890!";
    jwtIssuer   ??= "test";
    jwtAudience ??= "test";
}

// Known-leaked or placeholder secrets that must never be accepted, even if they
// happen to satisfy the length check. Match case-insensitively and ignore
// whitespace so trivial obfuscation doesn't slip past.
var knownBadJwtSecrets = new[]
{
    "CHANGE_ME",
    "pfa-eniad-2026-secret-key-super-secure-lhiadi", // historic leaked value
    "secret", "changeme", "placeholder",
};

static bool ContainsAny(string value, IEnumerable<string> needles) =>
    needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

// Reject low-entropy secrets: at least 3 of {lower, upper, digit, symbol} required.
static int CharClassCount(string s) =>
    (s.Any(char.IsLower)                 ? 1 : 0) +
    (s.Any(char.IsUpper)                 ? 1 : 0) +
    (s.Any(char.IsDigit)                 ? 1 : 0) +
    (s.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);

if (!isTestEnv && (string.IsNullOrWhiteSpace(jwtSecret) ||
    jwtSecret.Length < 32 ||
    ContainsAny(jwtSecret, knownBadJwtSecrets) ||
    CharClassCount(jwtSecret) < 3))
{
    throw new InvalidOperationException(
        "JWT secret is missing, shorter than 32 characters, matches a known-leaked / placeholder value, " +
        "or lacks character-class diversity (need ≥3 of: lower, upper, digit, symbol). " +
        "Generate a fresh value with: openssl rand -base64 48");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("JWT issuer and audience must be set.");
}

// Fail-fast: ML_INTERNAL_TOKEN must be set and not a placeholder.
// Without this, every prediction call silently returns a 401 from the ML service.
var mlToken = builder.Configuration["ML_INTERNAL_TOKEN"];
if (isTestEnv) mlToken ??= "test-token-not-placeholder";
if (!isTestEnv && (string.IsNullOrWhiteSpace(mlToken) || mlToken.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException(
        "ML_INTERNAL_TOKEN is missing or still set to the placeholder value. " +
        "Generate a fresh value with: openssl rand -hex 32");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "PlateformePFA API",
        Version     = "v1",
        Description = "API de la Plateforme Décisionnelle ENIAD 2025/2026.\n\n"
                    + "Gère les étudiants, notes, absences, alertes automatiques et les prédictions ML de risque d'échec.\n\n"
                    + "**Authentification** : JWT Bearer — obtenez un token via `POST /api/Auth/login`.",
        Contact     = new OpenApiContact { Name = "Équipe PFA ENIAD", Email = "contact@eniad.ma" }
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer eyJhb...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAlerteService, AlerteService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<RiskScorer>();
builder.Services.AddScoped<PlateformePFA.API.Services.ReportGenerator>();
builder.Services.AddScoped<IAcademicAccessService, AcademicAccessService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddHttpClient("MLService");

// Readiness health check — returns Healthy only after the full schema is
// initialised (dbo.SchemaState marker written at the end of init.sql).
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database_ready", tags: new[] { "ready" });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!))
    };

    // Re-validate the live account on every request: a 24h JWT must NOT keep a
    // user signed in (or at a stale role) after they are deactivated or demoted.
    // One indexed lookup per request — cheap at this scale, closes the window
    // that a security stamp would otherwise be needed for.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var idClaim = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!int.TryParse(idClaim, out var userId))
            {
                ctx.Fail("Invalid subject claim.");
                return;
            }

            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var account = await db.Utilisateurs
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.EstActif, u.Role })
                .FirstOrDefaultAsync();

            if (account is null || !account.EstActif || account.Role != roleClaim)
            {
                ctx.Fail("Account is no longer active or its role has changed.");
            }
        }
    };
});

// CORS — only matters when the frontend talks to the backend directly (Vite dev
// server). In Docker the browser hits nginx which proxies /api server-side, so
// no preflight is involved.
// Configurable via CORS_ALLOWED_ORIGINS (comma-separated) for production overrides.
var corsOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"]
        ?? "http://localhost:5173,http://localhost:3000,http://localhost,http://localhost:80")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy",
        policy =>
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors("FrontendPolicy");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness probe used by Docker healthchecks and the nginx depends_on gate.
// AllowAnonymous is explicit so that any future global `RequireAuthorization()`
// default policy (e.g. fallback policy on AddAuthorization) doesn't accidentally
// 401 the healthcheck — that would brick the container under Docker's gate.
// Liveness probe — always answers 200 if the process is alive.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Readiness probe — 200 only when database is reachable AND schema is ready.
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).WithMetadata(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute());

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context       = services.GetRequiredService<AppDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        // Idempotent runtime migrations + seeding — production schema/provisioning
        // that must roll forward on every startup. Both use raw SQL / sample-data
        // seeding meant for the real relational database; the InMemory test
        // provider already has the current model and tests seed their own data,
        // so the whole block is skipped there. A real failure rethrows below.
        if (context.Database.IsRelational())
        {
            PlateformePFA.API.Data.RuntimeMigrations.Apply(context);
            PlateformePFA.API.Data.DataSeeder.Initialize(context, configuration);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        // Fail fast: a failed migration or seed must NOT leave the app running
        // and reporting healthy on a half-provisioned schema. Rethrow so the
        // process exits non-zero and the container is restarted/flagged.
        logger.LogError(ex, "Database migration/seeding failed — aborting startup.");
        throw;
    }
}

app.Run();

// Expose the implicit Program class so WebApplicationFactory<Program> in
// PlateformePFA.Tests can host the API in-process.
public partial class Program { }
