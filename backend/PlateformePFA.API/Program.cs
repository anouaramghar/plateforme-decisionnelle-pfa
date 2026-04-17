using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Fail-fast config validation ──────────────────────────────
// Refuse to start if critical secrets are missing, too weak, or left as placeholders.
var jwtSecret   = builder.Configuration["JWT_SECRET"];
var jwtIssuer   = builder.Configuration["JWT_ISSUER"];
var jwtAudience = builder.Configuration["JWT_AUDIENCE"];

if (string.IsNullOrWhiteSpace(jwtSecret) ||
    jwtSecret.Length < 32 ||
    jwtSecret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
    jwtSecret.Contains("change_me", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JWT_SECRET is missing, shorter than 32 characters, or still set to a placeholder. " +
        "Set a strong value in your environment before starting the API.");
}

if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException("JWT_ISSUER and JWT_AUDIENCE must be set.");
}

// Trust reverse-proxy headers so Request.Scheme / Request.RemoteIp reflect the real client.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness probe used by Docker healthchecks and the nginx depends_on gate.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
