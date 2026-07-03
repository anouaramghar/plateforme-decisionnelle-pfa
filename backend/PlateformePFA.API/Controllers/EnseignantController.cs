using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;

namespace PlateformePFA.API.Controllers
{
    [ApiController]
    [Route("api/enseignant")]
    [Authorize(Roles = "Enseignant,Admin")]
    public class EnseignantController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EnseignantController(AppDbContext context) => _context = context;

        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>Returns the module (with its filière) assigned to the logged-in enseignant.</summary>
        [HttpGet("mon-module")]
        public async Task<IActionResult> GetMonModule()
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            var utilisateur = await _context.Utilisateurs
                .AsNoTracking()
                .Include(u => u.Module)
                    .ThenInclude(m => m!.Filiere)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (utilisateur is null) return NotFound();
            if (utilisateur.ModuleId is null)
                return Ok(null); // no module assigned yet

            var m = utilisateur.Module!;
            return Ok(new
            {
                moduleId      = m.Id,
                moduleCode    = m.Code,
                moduleNom     = m.Nom,
                semestre      = m.Semestre,
                coefficient   = m.Coefficient,
                filiereId     = m.FiliereId,
                filiereCode   = m.Filiere!.Code,
                filiereIntitule = m.Filiere.Intitule,
                niveau        = m.Niveau,
            });
        }

        /// <summary>Returns students enrolled in the filière+niveau of the enseignant's module.</summary>
        [HttpGet("mes-etudiants")]
        public async Task<IActionResult> GetMesEtudiants()
        {
            var userId = CurrentUserId;
            if (userId is null) return Unauthorized();

            var utilisateur = await _context.Utilisateurs
                .AsNoTracking()
                .Include(u => u.Module)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (utilisateur?.ModuleId is null)
                return Ok(Array.Empty<object>());

            var filiereId = utilisateur.Module!.FiliereId;
            var niveau    = utilisateur.Module!.Niveau;

            var etudiants = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.FiliereId == filiereId && e.Niveau == niveau && e.DesinscritLe == null)
                .OrderBy(e => e.Nom).ThenBy(e => e.Prenom)
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    nomComplet    = e.Nom + " " + e.Prenom,
                    e.Nom,
                    e.Prenom,
                    e.FiliereId,
                    e.Niveau,
                    e.Annee,
                })
                .ToListAsync();

            return Ok(etudiants);
        }
    }
}
