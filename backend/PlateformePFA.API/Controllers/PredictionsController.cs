using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.Models;
using System.Text;
using System.Text.Json;

namespace PlateformePFA.API.Controllers
{
    [Authorize(Roles = "Admin,Responsable")]
    [Route("api/[controller]")]
    [ApiController]
    public class PredictionsController : ControllerBase
    {
        private readonly AppDbContext        _context;
        private readonly IHttpClientFactory  _httpClientFactory;
        private readonly IConfiguration      _configuration;
        private readonly ILogger<PredictionsController> _logger;

        public PredictionsController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PredictionsController> logger)
        {
            _context           = context;
            _httpClientFactory = httpClientFactory;
            _configuration     = configuration;
            _logger            = logger;
        }

        // GET: api/predictions?page=1&pageSize=20&etudiantId=5&status=Ok
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<PredictionML>>> GetPredictions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? etudiantId = null,
            [FromQuery] string? status = null)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.PredictionsML.AsNoTracking().AsQueryable();
            if (etudiantId.HasValue) query = query.Where(p => p.EtudiantId == etudiantId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

            var ordered = query.OrderByDescending(p => p.CreeLe);
            var total   = await ordered.CountAsync();
            var items   = await ordered
                .Include(p => p.Etudiant)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<PredictionML>(items, total, page, pageSize);
        }

        // GET: api/predictions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PredictionML>> GetPrediction(int id)
        {
            var prediction = await _context.PredictionsML
                .Include(p => p.Etudiant)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (prediction == null) return NotFound(new { message = "Prédiction introuvable." });
            return prediction;
        }

        // GET: api/predictions/etudiant/5  (latest 200 for one student)
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<PredictionML>>> GetPredictionsByEtudiant(int etudiantId)
        {
            return await _context.PredictionsML
                .AsNoTracking()
                .Include(p => p.Etudiant)
                .Where(p => p.EtudiantId == etudiantId)
                .OrderByDescending(p => p.CreeLe)
                .Take(200)
                .ToListAsync();
        }

        // POST: api/predictions/predict/5
        [HttpPost("predict/{etudiantId}")]
        public async Task<ActionResult<PredictionML>> PredictForEtudiant(int etudiantId)
        {
            var etudiant = await _context.Etudiants
                .Include(e => e.Notes)
                .Include(e => e.Absences)
                .FirstOrDefaultAsync(e => e.Id == etudiantId);

            if (etudiant == null) return NotFound(new { message = "Étudiant introuvable." });

            var notes    = etudiant.Notes    ?? new List<Note>();
            var absences = etudiant.Absences ?? new List<Absence>();

            var notesAvecFinal = notes.Where(n => n.NoteFinal.HasValue).ToList();
            int nbModules = notes.Select(n => n.ModuleId).Distinct().Count();

            // Refuse to call ML when we'd violate its `nb_modules >= 1` schema —
            // ML would 422 and the previous code silently saved a polluted
            // ScoreRisque=0 row. Be explicit: short-circuit with InsufficientData.
            if (nbModules == 0 || notesAvecFinal.Count == 0)
            {
                return UnprocessableEntity(new
                {
                    message = "Données insuffisantes : aucune note finale enregistrée pour cet étudiant.",
                    nbModules,
                    nbNotesFinal = notesAvecFinal.Count,
                });
            }

            decimal moyenneGenerale = notesAvecFinal.Average(n => n.NoteFinal!.Value);

            // taux_absence = unjustified hours / scheduled hours (capped at 1.0).
            // 32h/module is a coarse estimate (2h/week × 16 weeks); replace with
            // module.HeuresParSemaine when that field exists.
            double scheduledHours = nbModules * 32.0;
            int    absenceHours   = absences.Where(a => !a.Justifiee).Sum(a => a.NombreHeures);
            double tauxAbsence    = Math.Min(absenceHours / scheduledHours, 1.0);

            var features = new
            {
                moyenne_generale = moyenneGenerale,
                taux_absence     = tauxAbsence,
                nb_modules       = nbModules,
            };

            decimal? scoreRisque  = null;
            string?  niveauRisque = null;
            string   status       = "Ok";
            string   featuresJson = JsonSerializer.Serialize(features);

            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client   = _httpClientFactory.CreateClient("MLService");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict")
                {
                    Content = new StringContent(featuresJson, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"]);

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("probabilite", out var prob))
                        scoreRisque = prob.GetDecimal();
                    if (doc.RootElement.TryGetProperty("niveau_risque", out var niveau))
                        niveauRisque = niveau.GetString();
                }
                else
                {
                    status = "MlUnavailable";
                    _logger.LogWarning(
                        "ML service returned {Code} for etudiantId={Id}", response.StatusCode, etudiantId);
                }
            }
            catch (Exception ex)
            {
                status = "MlUnavailable";
                _logger.LogWarning(ex, "ML service unreachable for etudiantId={Id}", etudiantId);
            }

            // If ML failed, surface the failure rather than persisting a misleading 0.
            if (status != "Ok")
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Service ML indisponible. Aucune prédiction enregistrée.",
                    status,
                });
            }

            // Read the academic year from configuration; fall back to a UTC computation.
            var annee = _configuration["CurrentAcademicYear"] ?? CurrentAcademicYear();

            var prediction = new PredictionML
            {
                EtudiantId  = etudiantId,
                TypeModele  = "RisqueEchec",
                ScoreRisque = scoreRisque,
                Niveau      = niveauRisque,
                Status      = status,
                Annee       = annee,
                CreeLe      = DateTime.UtcNow,
            };

            _context.PredictionsML.Add(prediction);
            await _context.SaveChangesAsync();

            // Insert an Alerte for high-risk predictions. Trust the ML label —
            // single source of truth for thresholds, no duplicated magic numbers.
            if (niveauRisque is "Eleve" or "Critique")
            {
                _context.Alertes.Add(new Alerte
                {
                    EtudiantId = etudiantId,
                    Type       = "RisqueEchec",
                    Niveau     = niveauRisque,
                    Message    = $"Risque élevé détecté : {niveauRisque} ({scoreRisque:P0})",
                    Resolue    = false,
                    CreeLe     = DateTime.UtcNow,
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Alerte créée automatiquement : EtudiantId={Id}, Niveau={N}",
                    etudiantId, niveauRisque);
            }

            _logger.LogInformation(
                "Prédiction stockée : EtudiantId={Id}, ScoreRisque={Score}, Niveau={N}",
                etudiantId, scoreRisque, niveauRisque);
            return CreatedAtAction(nameof(GetPrediction), new { id = prediction.Id }, prediction);
        }

        /// <summary>
        /// Academic year in "YYYY/YYYY" form. The school year flips on 1 September:
        /// dates before September belong to the previous start year.
        /// </summary>
        private static string CurrentAcademicYear()
        {
            var now = DateTime.UtcNow;
            var start = now.Month >= 9 ? now.Year : now.Year - 1;
            return $"{start}/{start + 1}";
        }
    }
}
