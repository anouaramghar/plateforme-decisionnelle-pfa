using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Rapports;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using System.Security.Claims;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RapportsController : ControllerBase
    {
        private readonly AppDbContext     _context;
        private readonly ReportGenerator  _generator;
        private readonly ILogger<RapportsController> _logger;
        private readonly IAuditService    _audit;
        private readonly IAcademicAccessService _academicAccess;

        // Static catalog — kept here so the frontend just sends a templateId.
        private static readonly Dictionary<string, string> Templates = new()
        {
            ["perf-globale"] = "Performance globale",
            ["risque"]       = "Étudiants à risque",
            ["notes"]        = "Notes par module",
            ["absences"]     = "Absences hebdomadaires",
            ["alertes"]      = "Journal des alertes",
            ["bilan-ml"]     = "Bilan modèle ML",
        };

        public RapportsController(
            AppDbContext context,
            ReportGenerator generator,
            ILogger<RapportsController> logger,
            IAuditService audit,
            IAcademicAccessService academicAccess)
        {
            _context   = context;
            _generator = generator;
            _logger    = logger;
            _audit     = audit;
            _academicAccess = academicAccess;
        }

        // GET /api/rapports — recent reports (metadata only).
        [HttpGet]
        public async Task<ActionResult<List<RapportDto>>> List([FromQuery] int limit = 50)
        {
            limit = Math.Clamp(limit, 1, 200);
            var query = _context.Rapports.AsNoTracking();
            if (User.IsInRole("Enseignant"))
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out var userId))
                {
                    query = query.Where(r => r.AuteurId == userId);
                }
            }
            var items = await query
                .OrderByDescending(r => r.CreeLe)
                .Take(limit)
                .Select(r => new RapportDto
                {
                    Id          = r.Id,
                    TemplateId  = r.TemplateId,
                    Titre       = r.Titre,
                    Format      = r.Format,
                    FiliereCode = r.FiliereCode,
                    Periode     = r.Periode,
                    AuteurNom   = r.AuteurNom,
                    Taille      = r.Taille,
                    CreeLe      = r.CreeLe,
                })
                .ToListAsync();
            return items;
        }

        // POST /api/rapports — generate + persist a new report. Returns metadata.
        [HttpPost]
        [Authorize(Roles = "Admin,Responsable,Enseignant")]
        public async Task<ActionResult<RapportDto>> Create([FromBody] CreateRapportDto dto)
        {
            if (!Templates.ContainsKey(dto.TemplateId))
            {
                return BadRequest(new { message = $"Template inconnu : {dto.TemplateId}" });
            }

            try
            {
                var assignedModuleId = await _academicAccess.GetAssignedModuleIdAsync(User);
                if (User.IsInRole("Enseignant"))
                {
                    if (!assignedModuleId.HasValue)
                    {
                        return Forbid();
                    }

                    var module = await _context.Modules
                        .Include(m => m.Filiere)
                        .FirstOrDefaultAsync(m => m.Id == assignedModuleId.Value);

                    if (module == null || module.Filiere == null)
                    {
                        return Forbid();
                    }

                    var userFiliereCode = module.Filiere.Code;

                    if (string.IsNullOrWhiteSpace(dto.FiliereCode) || dto.FiliereCode == "TOUS" || dto.FiliereCode != userFiliereCode)
                    {
                        return Forbid();
                    }
                }

                var result = await _generator.GenerateAsync(
                    dto.TemplateId,
                    dto.Format,
                    dto.FiliereCode,
                    dto.Periode,
                    assignedModuleId);

                var (auteurId, auteurNom) = ResolveAuteur();

                var rapport = new Rapport
                {
                    TemplateId  = dto.TemplateId,
                    Titre       = result.Title,
                    Format      = dto.Format,
                    FiliereCode = string.IsNullOrWhiteSpace(dto.FiliereCode) ? "TOUS" : dto.FiliereCode,
                    Periode     = dto.Periode,
                    AuteurId    = auteurId,
                    AuteurNom   = auteurNom,
                    Taille      = result.Bytes.Length,
                    Contenu     = result.Bytes,
                    CreeLe      = DateTime.UtcNow,
                };

                _context.Rapports.Add(rapport);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Rapport généré : id={Id}, template={Tpl}, format={Fmt}, taille={Size}o",
                    rapport.Id, dto.TemplateId, dto.Format, rapport.Taille);

                await _audit.LogAsync(
                    action: "RapportGenere",
                    entityType: "Rapport",
                    entityId: rapport.Id,
                    message: $"{rapport.Titre} ({rapport.Format})",
                    userId: auteurId,
                    userName: auteurNom);

                return new RapportDto
                {
                    Id          = rapport.Id,
                    TemplateId  = rapport.TemplateId,
                    Titre       = rapport.Titre,
                    Format      = rapport.Format,
                    FiliereCode = rapport.FiliereCode,
                    Periode     = rapport.Periode,
                    AuteurNom   = rapport.AuteurNom,
                    Taille      = rapport.Taille,
                    CreeLe      = rapport.CreeLe,
                };
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec de génération du rapport {Tpl}/{Fmt}", dto.TemplateId, dto.Format);
                return StatusCode(500, new { message = "Erreur lors de la génération du rapport." });
            }
        }

        // GET /api/rapports/{id}/download — stream the file out.
        [HttpGet("{id:int}/download")]
        public async Task<IActionResult> Download(int id)
        {
            var rapport = await _context.Rapports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);
            if (rapport == null) return NotFound();

            if (!User.IsInRole("Admin") && !User.IsInRole("Responsable"))
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId) || rapport.AuteurId != userId)
                {
                    return Forbid();
                }
            }

            var contentType = rapport.Format switch
            {
                "PDF"  => "application/pdf",
                "XLSX" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "CSV"  => "text/csv; charset=utf-8",
                _      => "application/octet-stream",
            };

            var ext = rapport.Format.ToLowerInvariant();
            // Sanitize the filename — strip newlines/quotes that would break the
            // Content-Disposition header, then ASCII-fold for older browsers.
            var safeTitle = SanitizeForFilename(rapport.Titre);
            var fileName  = $"{safeTitle}-{rapport.Id}.{ext}";

            return File(rapport.Contenu, contentType, fileName);
        }

        // DELETE /api/rapports/{id} — Admin only.
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var rapport = await _context.Rapports.FirstOrDefaultAsync(r => r.Id == id);
            if (rapport == null) return NotFound();

            _context.Rapports.Remove(rapport);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private (int? AuteurId, string AuteurNom) ResolveAuteur()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(idClaim, out var id);

            // Try the friendly name claim — DataSeeder writes "NomComplet" — and
            // fall back to email for users created via the API.
            var nom = User.FindFirstValue("NomComplet")
                   ?? User.FindFirstValue(ClaimTypes.Name)
                   ?? User.FindFirstValue(ClaimTypes.Email)
                   ?? "Utilisateur";

            return (id == 0 ? null : id, nom);
        }

        private static string SanitizeForFilename(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in s)
            {
                if (c == ' ' || c == '-' || c == '_') sb.Append('-');
                else if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            }
            var trimmed = sb.ToString().Trim('-');
            return string.IsNullOrEmpty(trimmed) ? "rapport" : trimmed;
        }
    }
}
