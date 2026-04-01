using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PlateformePFA.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private static readonly Dictionary<string, (string userId, DateTime expiry)> _refreshTokens = new();

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var utilisateur = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (utilisateur == null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
            {
                _logger.LogWarning("Failed login attempt for email: {Email}", request.Email);
                return Unauthorized(new { message = "Email ou mot de passe incorrect." });
            }

            var token = GenerateJwtToken(utilisateur);
            var refreshToken = GenerateRefreshToken(utilisateur.Id.ToString());

            _logger.LogInformation("Successful login for user {UserId} ({Email})", utilisateur.Id, utilisateur.Email);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                refreshToken,
                expiration = token.ValidTo,
                role = utilisateur.Role
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (!_refreshTokens.TryGetValue(request.RefreshToken, out var entry))
                return Unauthorized(new { message = "Refresh token invalide." });

            if (entry.expiry < DateTime.UtcNow)
            {
                _refreshTokens.Remove(request.RefreshToken);
                return Unauthorized(new { message = "Refresh token expiré." });
            }

            var utilisateur = await _context.Utilisateurs.FindAsync(int.Parse(entry.userId));
            if (utilisateur == null)
                return Unauthorized(new { message = "Utilisateur introuvable." });

            _refreshTokens.Remove(request.RefreshToken);

            var newToken = GenerateJwtToken(utilisateur);
            var newRefreshToken = GenerateRefreshToken(utilisateur.Id.ToString());

            _logger.LogInformation("Token refreshed for user {UserId}", utilisateur.Id);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(newToken),
                refreshToken = newRefreshToken,
                expiration = newToken.ValidTo,
                role = utilisateur.Role
            });
        }

        private JwtSecurityToken GenerateJwtToken(Utilisateur utilisateur)
        {
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
                new Claim(ClaimTypes.Email, utilisateur.Email),
                new Claim(ClaimTypes.Role, utilisateur.Role)
            };

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            return new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.UtcNow.AddHours(24),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );
        }

        private static string GenerateRefreshToken(string userId)
        {
            var token = Guid.NewGuid().ToString();
            _refreshTokens[token] = (userId, DateTime.UtcNow.AddDays(7));
            return token;
        }
    }

    public class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
