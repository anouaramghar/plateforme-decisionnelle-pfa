using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Fail-fast: refuse to start if JWT secrets are missing, too weak, or placeholder.
// Reads JWT_SECRET env var first (Docker), falls back to Jwt:Key (appsettings dev).
var jwtSecret   = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Key"];
var jwtIssuer   = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"];

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

if (string.IsNullOrWhiteSpace(jwtSecret) ||
    jwtSecret.Length < 32 ||
    ContainsAny(jwtSecret, knownBadJwtSecrets) ||
    CharClassCount(jwtSecret) < 3)
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
if (string.IsNullOrWhiteSpace(mlToken) || mlToken.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
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
builder.Services.AddScoped<PlateformePFA.API.Services.ReportGenerator>();

builder.Services.AddHttpClient("MLService");

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

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

        // Idempotent runtime migrations — entrypoint.sh only runs init.sql
        // on first boot, so additive schema changes go here. Each block is
        // safe to run on every startup.
        PlateformePFA.API.Data.RuntimeMigrations.Apply(context);

        PlateformePFA.API.Data.DataSeeder.Initialize(context, configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
