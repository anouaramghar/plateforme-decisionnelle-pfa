using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UtilisateursController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UtilisateursController> _logger;

        public UtilisateursController(AppDbContext context, ILogger<UtilisateursController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Utilisateurs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Utilisateur>>> GetUtilisateurs()
        {
            return await _context.Utilisateurs.ToListAsync();
        }

        // POST: api/Utilisateurs
        [HttpPost]
        public async Task<ActionResult<Utilisateur>> PostUtilisateur(PlateformePFA.API.DTOs.Auth.RegisterRequestDto dto)
        {
            var utilisateur = new Utilisateur
            {
                Nom = dto.Nom,
                Prenom = dto.Prenom,
                Email = dto.Email,
                Role = dto.Role,
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(dto.MotDePasse)
            };
            
            _context.Utilisateurs.Add(utilisateur);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user created — Email: {Email}, Role: {Role}", utilisateur.Email, utilisateur.Role);

            return CreatedAtAction(nameof(GetUtilisateurs), new { id = utilisateur.Id }, utilisateur);
        }
    }
}
