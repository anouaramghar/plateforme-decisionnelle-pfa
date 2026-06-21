using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
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

        // GET: api/Utilisateurs?page=1&pageSize=20
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Utilisateur>>> GetUtilisateurs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Utilisateurs.AsNoTracking().OrderBy(u => u.Id);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Utilisateur>(items, total, page, pageSize);
        }

        // GET: api/Utilisateurs/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Utilisateur>> GetUtilisateur(int id)
        {
            var u = await _context.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            return u is null ? NotFound() : Ok(u);
        }

        // POST: api/Utilisateurs
        [HttpPost]
        public async Task<ActionResult<Utilisateur>> PostUtilisateur(PlateformePFA.API.DTOs.Auth.RegisterRequestDto dto)
        {
            // Surface a 409 instead of letting SQL UNIQUE violation bubble up as a 500.
            if (await _context.Utilisateurs.AnyAsync(u => u.Email == dto.Email))
                return Conflict(new { message = $"Email '{dto.Email}' déjà utilisé." });

            var utilisateur = new Utilisateur
            {
                Nom = dto.Nom,
                Prenom = dto.Prenom,
                Email = dto.Email,
                Role = dto.Role,
                ModuleId = dto.Role == "Enseignant" ? dto.ModuleId : null,
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(dto.MotDePasse)
            };

            _context.Utilisateurs.Add(utilisateur);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user created — Email: {Email}, Role: {Role}", utilisateur.Email, utilisateur.Role);

            return CreatedAtAction(nameof(GetUtilisateur), new { id = utilisateur.Id }, utilisateur);
        }
    }
}
