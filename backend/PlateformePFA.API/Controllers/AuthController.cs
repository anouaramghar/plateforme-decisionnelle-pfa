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
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        // On injecte IConfiguration pour pouvoir lire le appsettings.json
        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var utilisateur = await _context.Utilisateurs.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (utilisateur == null || !BCrypt.Net.BCrypt.Verify(request.MotDePasse, utilisateur.MotDePasseHash))
            {
                return Unauthorized(new { message = "Email ou mot de passe incorrect." });
            }

            // 3. FABRICATION DU TOKEN JWT
            // On prépare les informations (Claims) qu'on va mettre dans le badge
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.Id.ToString()),
                new Claim(ClaimTypes.Email, utilisateur.Email),
                new Claim(ClaimTypes.Role, utilisateur.Role)
            };

            // On récupère notre clé secrète
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            // On crée le Token avec une durée de vie de 24h (comme demandé dans le CDC)
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.Now.AddHours(24),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            // On renvoie le Token au Frontend
            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                role = utilisateur.Role
            });
        }
    }
}