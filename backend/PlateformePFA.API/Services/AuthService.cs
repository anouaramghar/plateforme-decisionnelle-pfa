using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Auth;
using PlateformePFA.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PlateformePFA.API.Services
{
    public class AuthService : IAuthService
    {
        private static readonly string DummyHash =
            BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing", workFactor: 11);
        private readonly AppDbContext    _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IAuditService _audit;

        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IAuditService audit)
        {
            _context       = context;
            _configuration = configuration;
            _logger        = logger;
            _audit         = audit;
        }

        // ── Login ─────────────────────────────────────────────────
        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var utilisateur = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Email == request.Email && u.EstActif);

            if (utilisateur == null)
            {
                _ = BCrypt.Net.BCrypt.Verify(request.MotDePasse, DummyHash);
                _logger.LogWarning("[AuthService] Tentative échouée pour : {Email}", request.Email);
                return null;
            }

            if (!BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
            {
                _logger.LogWarning("[AuthService] Tentative échouée pour : {Email}", request.Email);
                return null;
            }

            var token        = BuildJwtToken(utilisateur);
            var refreshToken = await CreateRefreshTokenAsync(utilisateur.Id);

            _logger.LogInformation("[AuthService] Login OK — userId={UserId}", utilisateur.Id);

            await _audit.LogAsync(
                action: "LoginOk",
                entityType: "Utilisateur",
                entityId: utilisateur.Id,
                userId: utilisateur.Id,
                userName: $"{utilisateur.Prenom} {utilisateur.Nom}".Trim());

            return new AuthResponseDto
            {
                Token        = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken.Token,
                Expiration   = token.ValidTo,
                Role         = utilisateur.Role,
                Email        = utilisateur.Email,
                NomComplet   = $"{utilisateur.Prenom} {utilisateur.Nom}"
            };
        }

        // ── Refresh ───────────────────────────────────────────────
        public async Task<AuthResponseDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.Utilisateur)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

            if (storedToken == null)
            {
                _logger.LogWarning("[AuthService] Refresh token introuvable");
                return null;
            }

            var utilisateur = storedToken.Utilisateur;
            if (utilisateur == null || !utilisateur.EstActif)
            {
                _logger.LogWarning("[AuthService] Refresh refusé — compte désactivé ou introuvable userId={UserId}", storedToken.UtilisateurId);
                return null;
            }

            // Fallback for InMemory provider which does not support ExecuteUpdateAsync or transactions
            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                if (storedToken.RevokedAt != null || storedToken.ExpiresAt <= DateTime.UtcNow)
                {
                    _logger.LogWarning("[AuthService] Refresh token déjà consommé ou expiré (InMemory) — rt.Id={Id}", storedToken.Id);
                    return null;
                }

                storedToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                var newJwt          = BuildJwtToken(utilisateur);
                var newRefreshToken = await CreateRefreshTokenAsync(utilisateur.Id, cancellationToken);

                _logger.LogInformation("[AuthService] Token rafraîchi (InMemory) — userId={UserId}", utilisateur.Id);

                return new AuthResponseDto
                {
                    Token        = new JwtSecurityTokenHandler().WriteToken(newJwt),
                    RefreshToken = newRefreshToken.Token,
                    Expiration   = newJwt.ValidTo,
                    Role         = utilisateur.Role,
                    Email        = utilisateur.Email,
                    NomComplet   = $"{utilisateur.Prenom} {utilisateur.Nom}"
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var rowsUpdated = await _context.RefreshTokens
                    .Where(rt => rt.Id == storedToken.Id && rt.RevokedAt == null && rt.ExpiresAt > now)
                    .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now), cancellationToken);

                if (rowsUpdated == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning("[AuthService] Refresh token déjà consommé ou expiré — rt.Id={Id}", storedToken.Id);
                    return null;
                }

                var newJwt          = BuildJwtToken(utilisateur);
                var newRefreshToken = await CreateRefreshTokenAsync(utilisateur.Id, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("[AuthService] Token rafraîchi — userId={UserId}", utilisateur.Id);

                return new AuthResponseDto
                {
                    Token        = new JwtSecurityTokenHandler().WriteToken(newJwt),
                    RefreshToken = newRefreshToken.Token,
                    Expiration   = newJwt.ValidTo,
                    Role         = utilisateur.Role,
                    Email        = utilisateur.Email,
                    NomComplet   = $"{utilisateur.Prenom} {utilisateur.Nom}"
                };
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "[AuthService] Erreur lors du rollback de la transaction pour rt.Id={Id}", storedToken.Id);
                }
                _logger.LogError(ex, "[AuthService] Erreur lors de la rotation du refresh token — rt.Id={Id}", storedToken.Id);
                throw;
            }
        }

        // ── Helpers privés ────────────────────────────────────────
        private JwtSecurityToken BuildJwtToken(Utilisateur utilisateur)
        {
            var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT_SECRET"] ?? _configuration["Jwt:Key"]!));
            var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
                new Claim(ClaimTypes.Email,          utilisateur.Email),
                new Claim(ClaimTypes.Role,           utilisateur.Role),
                new Claim("NomComplet",              $"{utilisateur.Prenom} {utilisateur.Nom}")
            };

            return new JwtSecurityToken(
                issuer:             _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"],
                audience:           _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"],
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );
        }

        // ── Revoke (logout) ───────────────────────────────────────
        public async Task<bool> RevokeAsync(string refreshToken)
        {
            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (stored == null || stored.RevokedAt != null) return false;

            stored.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("[AuthService] Refresh token révoqué — userId={UserId}", stored.UtilisateurId);
            return true;
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(int utilisateurId, CancellationToken cancellationToken = default)
        {
            // Generate a cryptographically secure random token
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var tokenString = Convert.ToBase64String(tokenBytes);

            var refreshToken = new RefreshToken
            {
                UtilisateurId = utilisateurId,
                Token         = tokenString,
                ExpiresAt     = DateTime.UtcNow.AddDays(7),
                CreeLe        = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync(cancellationToken);

            return refreshToken;
        }
    }
}
