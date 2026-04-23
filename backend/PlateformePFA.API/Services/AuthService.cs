using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlateformePFA.API.Services
{
    public class AuthService : IAuthService
    {
        // Refresh tokens en mémoire (clé = token, valeur = (userId, expiry))
        // ℹ Pour une prod réelle, stocker en BDD ou Redis
        private static readonly Dictionary<string, (string UserId, DateTime Expiry)> _refreshTokens = new();

        private readonly AppDbContext    _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context       = context;
            _configuration = configuration;
            _logger        = logger;
        }

        // ── Login ─────────────────────────────────────────────────
        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var utilisateur = await _context.Utilisateurs
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (utilisateur == null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
            {
                _logger.LogWarning("[AuthService] Tentative échouée pour : {Email}", request.Email);
                return null;
            }

            var token        = BuildJwtToken(utilisateur);
            var refreshToken = BuildRefreshToken(utilisateur.Id.ToString());

            _logger.LogInformation("[AuthService] Login OK — userId={UserId}", utilisateur.Id);

            return new AuthResponseDto
            {
                Token        = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken,
                Expiration   = token.ValidTo,
                Role         = utilisateur.Role,
                Email        = utilisateur.Email,
                NomComplet   = $"{utilisateur.Prenom} {utilisateur.Nom}"
            };
        }

        // ── Refresh ───────────────────────────────────────────────
        public async Task<AuthResponseDto?> RefreshAsync(string refreshToken)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out var entry))
                return null;

            if (entry.Expiry < DateTime.UtcNow)
            {
                _refreshTokens.Remove(refreshToken);
                return null;
            }

            var utilisateur = await _context.Utilisateurs.FindAsync(int.Parse(entry.UserId));
            if (utilisateur == null) return null;

            _refreshTokens.Remove(refreshToken);

            var newToken        = BuildJwtToken(utilisateur);
            var newRefreshToken = BuildRefreshToken(utilisateur.Id.ToString());

            _logger.LogInformation("[AuthService] Token rafraîchi — userId={UserId}", utilisateur.Id);

            return new AuthResponseDto
            {
                Token        = new JwtSecurityTokenHandler().WriteToken(newToken),
                RefreshToken = newRefreshToken,
                Expiration   = newToken.ValidTo,
                Role         = utilisateur.Role,
                Email        = utilisateur.Email,
                NomComplet   = $"{utilisateur.Prenom} {utilisateur.Nom}"
            };
        }

        // ── Helpers privés ────────────────────────────────────────
        private JwtSecurityToken BuildJwtToken(Models.Utilisateur utilisateur)
        {
            var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
                new Claim(ClaimTypes.Email,          utilisateur.Email),
                new Claim(ClaimTypes.Role,           utilisateur.Role),
                new Claim("NomComplet",              $"{utilisateur.Prenom} {utilisateur.Nom}")
            };

            return new JwtSecurityToken(
                issuer:             _configuration["Jwt:Issuer"],
                audience:           _configuration["Jwt:Audience"],
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );
        }

        private static string BuildRefreshToken(string userId)
        {
            var token = Guid.NewGuid().ToString("N"); // sans tirets
            _refreshTokens[token] = (userId, DateTime.UtcNow.AddDays(7));
            return token;
        }
    }
}
