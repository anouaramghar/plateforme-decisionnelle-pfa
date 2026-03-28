using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UtilisateursController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UtilisateursController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Utilisateurs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Utilisateur>>> GetUtilisateurs()
        {
            return await _context.Utilisateurs.ToListAsync();
        }

        // POST: api/Utilisateurs
        [HttpPost]
        public async Task<ActionResult<Utilisateur>> PostUtilisateur(Utilisateur utilisateur)
        {
            // 🔒 HACHAGE DU MOT DE PASSE : On utilise BCrypt qu'on vient d'installer
            if (!string.IsNullOrEmpty(utilisateur.MotDePasseHash))
            {
                utilisateur.MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(utilisateur.MotDePasseHash);
            }

            _context.Utilisateurs.Add(utilisateur);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUtilisateurs), new { id = utilisateur.Id }, utilisateur);
        }
    }
}