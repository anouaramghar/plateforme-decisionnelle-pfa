using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Alertes;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AlertesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RiskScorer _riskScorer;
        private readonly ILogger<AlertesController> _logger;

        public AlertesController(AppDbContext context, RiskScorer riskScorer, ILogger<AlertesController> logger)
        {
            _context    = context;
            _riskScorer = riskScorer;
            _logger     = logger;
        }

        // GET: api/alertes?resolue=false&page=1&pageSize=20
        // Alerts are an institution-wide management view; teachers have no
        // alerts UI, so reads are restricted server-side (defense in depth).
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<AlerteListDto>>> GetAlertes(
            [FromQuery] bool? resolue = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Alertes.AsNoTracking().AsQueryable();
            if (resolue.HasValue) query = query.Where(a => a.Resolue == resolue.Value);

            var ordered = query.OrderByDescending(a => a.CreeLe);
            var total   = await ordered.CountAsync();
            // Projected to a DTO because Alerte.Etudiant is [JsonIgnore]'d —
            // returning the entity ships rows with a permanently-null student.
            var items   = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AlerteListDto
                {
                    Id         = a.Id,
                    EtudiantId = a.EtudiantId,
                    ModuleId   = a.ModuleId,
                    Type       = a.Type,
                    Niveau     = a.Niveau,
                    Message    = a.Message,
                    Resolue    = a.Resolue,
                    CreeLe     = a.CreeLe,
                    Etudiant   = new AlerteEtudiantDto
                    {
                        Id         = a.Etudiant.Id,
                        Matricule  = a.Etudiant.Matricule,
                        Nom        = a.Etudiant.Nom,
                        Prenom     = a.Etudiant.Prenom,
                        NomComplet = a.Etudiant.Prenom + " " + a.Etudiant.Nom,
                        Filiere    = a.Etudiant.Filiere != null ? a.Etudiant.Filiere.Code : "",
                        Niveau     = a.Etudiant.Niveau,
                        // Lexicographic max is correct for the S1–S9 range.
                        Semestre   = a.Etudiant.Notes!.Max(n => n.Semestre),
                    },
                })
                .ToListAsync();

            return new PaginatedResult<AlerteListDto>(items, total, page, pageSize);
        }

        // GET: api/alertes/stats — global counts for the UI stat cards. The
        // list endpoint is paginated (capped at 100), so per-type totals must
        // come from the database, not from whatever window the client fetched.
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet("stats")]
        public async Task<ActionResult<AlerteStatsDto>> GetStats()
        {
            var parType = await _context.Alertes.AsNoTracking()
                .Where(a => !a.Resolue)
                .GroupBy(a => a.Type)
                .Select(g => new { Type = g.Key, N = g.Count() })
                .ToListAsync();
            var resolue = await _context.Alertes.AsNoTracking().CountAsync(a => a.Resolue);
            var active  = parType.Sum(x => x.N);

            return new AlerteStatsDto
            {
                Active  = active,
                Resolue = resolue,
                Total   = active + resolue,
                ParType = parType.ToDictionary(x => x.Type, x => x.N),
            };
        }

        // GET: api/alertes/5
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet("{id}")]
        public async Task<ActionResult<Alerte>> GetAlerte(int id)
        {
            var alerte = await _context.Alertes
                .Include(a => a.Etudiant)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });
            return alerte;
        }

        // GET: api/alertes/etudiant/5
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<Alerte>>> GetAlertesByEtudiant(int etudiantId)
        {
            return await _context.Alertes
                .Include(a => a.Etudiant)
                .Where(a => a.EtudiantId == etudiantId)
                .OrderByDescending(a => a.CreeLe)
                .Take(500)           // safety cap — one student's full alert history
                .ToListAsync();
        }

        // GET: api/alertes/triage
        // The triage queue: un-triaged signals (unresolved, not linked to a case)
        // grouped by student so multiple alerts become one decision. Severity
        // order Critique > Eleve > Moyen > Faible drives the suggested priority.
        // ponytail: lives here, not a separate /api/intervention-signals controller —
        // a signal IS an Alerte. 7-day clustering deferred; grouping by student
        // already prevents duplicate cases (the actual acceptance criterion).
        [Authorize(Roles = "Admin,Responsable")]
        [HttpGet("triage")]
        public async Task<ActionResult<IEnumerable<TriageGroupDto>>> GetTriageQueue()
        {
            var signals = await _context.Alertes
                .AsNoTracking()
                .Where(a => !a.Resolue && a.CaseId == null)
                .Include(a => a.Etudiant)
                .OrderByDescending(a => a.CreeLe)
                .Take(1000) // safety cap
                .ToListAsync();

            var etudiantIds = signals.Select(s => s.EtudiantId).Distinct().ToList();
            var openCaseByStudent = await _context.InterventionCases
                .AsNoTracking()
                .Where(c => etudiantIds.Contains(c.EtudiantId)
                            && c.Etat != CaseWorkflowState.Resolved
                            && c.Etat != CaseWorkflowState.Closed)
                .GroupBy(c => c.EtudiantId)
                .Select(g => new { EtudiantId = g.Key, CaseId = g.Max(c => c.Id) })
                .ToDictionaryAsync(x => x.EtudiantId, x => x.CaseId);

            // Academic context per student — same aggregation the dashboard uses,
            // so a student's risk/moyenne reads identically across the app.
            var stats = await _context.Etudiants.AsNoTracking()
                .Where(e => etudiantIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Id,
                    Moyenne = (decimal?)e.Notes!.Where(n => n.NoteFinal.HasValue).Average(n => n.NoteFinal),
                    Absences = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                })
                .ToDictionaryAsync(x => x.Id, x => x);

            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();

            var groups = signals
                .GroupBy(s => s.EtudiantId)
                .Select(g =>
                {
                    var maxNiveau = g.Select(s => s.Niveau).OrderByDescending(NiveauRank).First();
                    var e = g.First().Etudiant;
                    stats.TryGetValue(g.Key, out var st);
                    var moyenne  = st?.Moyenne;
                    var absences = st?.Absences ?? 0;
                    var score    = _riskScorer.Score(moyenne, absences, g.Key, latestPredictions);
                    var count    = g.Count();
                    return new TriageGroupDto
                    {
                        EtudiantId        = g.Key,
                        Matricule         = e?.Matricule ?? "",
                        EtudiantNom       = e == null ? "" : $"{e.Prenom} {e.Nom}".Trim(),
                        FiliereId         = e?.FiliereId ?? 0,
                        SignalCount       = count,
                        MaxNiveau         = maxNiveau,
                        SuggestedPriorite = RiskScorer.SuggestedPriorite(score, maxNiveau),
                        ScoreRisque       = Math.Round(score, 2),
                        Moyenne           = moyenne.HasValue ? Math.Round(moyenne.Value, 1) : null,
                        AbsencesH         = absences,
                        Resume            = BuildResume(score, moyenne, absences, count),
                        OpenCaseId        = openCaseByStudent.TryGetValue(g.Key, out var cid) ? cid : (int?)null,
                        Signals           = g.OrderByDescending(s => s.CreeLe).ToList(),
                    };
                })
                // Risk score is the primary sort — the model decides who needs
                // attention first; raw-signal severity and volume break ties.
                .OrderByDescending(grp => grp.ScoreRisque)
                .ThenByDescending(grp => NiveauRank(grp.MaxNiveau))
                .ThenByDescending(grp => grp.SignalCount)
                .Take(50) // ponytail: a focused top-50 queue, not a 500-row wall. Raise if counsellors clear it faster.
                .ToList();

            return groups;
        }

        private static string BuildResume(decimal score, decimal? moyenne, int absencesH, int signalCount)
        {
            var parts = new List<string> { $"Risque {Math.Round(score * 100)}%" };
            if (moyenne.HasValue) parts.Add($"moyenne {Math.Round(moyenne.Value, 1)}");
            if (absencesH > 0)    parts.Add($"{absencesH}h d'absence");
            parts.Add($"{signalCount} signal(s)");
            return string.Join(" · ", parts);
        }

        private static int NiveauRank(string niveau) => niveau switch
        {
            "Critique" => 3, "Eleve" => 2, "Moyen" => 1, _ => 0,
        };

        // PATCH: api/alertes/5/dismiss  — dismiss a signal with a mandatory reason.
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPatch("{id}/dismiss")]
        public async Task<IActionResult> DismissSignal(int id, DismissSignalDto dto)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });
            if (alerte.CaseId != null)
                return BadRequest(new { message = "Signal déjà lié à un cas ; impossible de le rejeter." });

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdClaim, out var uid) ? uid : null;

            alerte.Resolue       = true;
            alerte.ResolueeLe    = DateTime.UtcNow;
            alerte.ResolueeParId = userId;
            alerte.MotifTriage   = dto.Raison;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Signal rejeté : Id={Id}, par userId={UserId}", alerte.Id, userId);
            return NoContent();
        }

        private static readonly HashSet<string> AllowedTypes =
            new() { "RisqueEchec", "AbsenceExcessive", "NoteFaible", "Abandon" };
        private static readonly HashSet<string> AllowedNiveaux =
            new() { "Faible", "Moyen", "Eleve", "Critique" };

        // POST: api/alertes
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<ActionResult<Alerte>> PostAlerte(CreateAlerteDto dto)
        {
            if (!AllowedTypes.Contains(dto.Type))
                return BadRequest(new { message = "Type d'alerte invalide." });
            if (!AllowedNiveaux.Contains(dto.Niveau))
                return BadRequest(new { message = "Niveau d'alerte invalide." });
            if (!await _context.Etudiants.AnyAsync(e => e.Id == dto.EtudiantId))
                return NotFound(new { message = "Etudiant introuvable." });

            var alerte = new Alerte
            {
                EtudiantId = dto.EtudiantId,
                Type       = dto.Type,
                Niveau     = dto.Niveau,
                Message    = dto.Message,
                Resolue    = false,
                CreeLe     = DateTime.UtcNow
            };

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Alerte créée : Id={Id}, Type={Type}, Niveau={Niveau}, EtudiantId={EtudiantId}",
                alerte.Id, alerte.Type, alerte.Niveau, alerte.EtudiantId);

            return CreatedAtAction(nameof(GetAlerte), new { id = alerte.Id }, alerte);
        }

        // PATCH: api/alertes/5/resoudre
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPatch("{id}/resoudre")]
        public async Task<IActionResult> ResoudreAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            // Audit: capture which Utilisateur clicked the button.
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? resolueeParId = int.TryParse(userIdClaim, out var uid) ? uid : null;

            alerte.Resolue       = true;
            alerte.ResolueeLe    = DateTime.UtcNow;
            alerte.ResolueeParId = resolueeParId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte résolue : Id={Id}, EtudiantId={EtudiantId}, ResolueeParId={UserId}",
                alerte.Id, alerte.EtudiantId, resolueeParId);
            return NoContent();
        }

        // PATCH: api/alertes/batch-resolve
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPatch("batch-resolve")]
        public async Task<IActionResult> BatchResolve([FromBody] BatchResolveRequest request)
        {
            if (request?.Ids == null || request.Ids.Length == 0)
                return BadRequest(new { message = "Aucun ID fourni." });

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? resolueeParId = int.TryParse(userIdClaim, out var uid) ? uid : null;
            var now = DateTime.UtcNow;

            var alertes = await _context.Alertes
                .Where(a => request.Ids.Contains(a.Id) && !a.Resolue)
                .ToListAsync();

            foreach (var alerte in alertes)
            {
                alerte.Resolue       = true;
                alerte.ResolueeLe    = now;
                alerte.ResolueeParId = resolueeParId;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Batch resolve: {Count} alertes résolues par userId={UserId}",
                alertes.Count, resolueeParId);

            return Ok(new { resolved = alertes.Count });
        }

        public class BatchResolveRequest
        {
            public int[] Ids { get; set; } = Array.Empty<int>();
        }

        // DELETE: api/alertes/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlerte(int id)
        {
            var alerte = await _context.Alertes.FindAsync(id);
            if (alerte == null) return NotFound(new { message = "Alerte introuvable." });

            if (alerte.CaseId != null)
                return Conflict(new { message = "Impossible de supprimer une alerte liée à un cas." });

            _context.Alertes.Remove(alerte);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
