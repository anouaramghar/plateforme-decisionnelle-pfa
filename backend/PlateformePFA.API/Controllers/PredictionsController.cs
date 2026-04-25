using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
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

        // GET: api/predictions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PredictionML>>> GetPredictions()
        {
            return await _context.PredictionsML
                .Include(p => p.Etudiant)
                .OrderByDescending(p => p.CreeLe)
                .ToListAsync();
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

        // GET: api/predictions/etudiant/5
        [HttpGet("etudiant/{etudiantId}")]
        public async Task<ActionResult<IEnumerable<PredictionML>>> GetPredictionsByEtudiant(int etudiantId)
        {
            return await _context.PredictionsML
                .Include(p => p.Etudiant)
                .Where(p => p.EtudiantId == etudiantId)
                .OrderByDescending(p => p.CreeLe)
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
            decimal moyenneGenerale = notesAvecFinal.Any()
                ? notesAvecFinal.Average(n => n.NoteFinal!.Value)
                : 0m;
            int nbModules = notes.Select(n => n.ModuleId).Distinct().Count();

            // taux_absence = totalAbsenceHours / totalScheduledHours (capped at 1.0)
            // Estimate: each module has ~2h/week for ~16 weeks = 32h per module
            double scheduledHours = nbModules > 0 ? nbModules * 32.0 : 32.0;
            int    absenceHours   = absences.Sum(a => a.NombreHeures);
            double tauxAbsence    = Math.Min(absenceHours / scheduledHours, 1.0);

            var features = new
            {
                moyenne_generale = moyenneGenerale,
                taux_absence     = tauxAbsence,
                nb_modules       = nbModules
            };

            decimal scoreRisque   = 0m;
            string? niveauRisque  = null;
            string  featuresJson  = JsonSerializer.Serialize(features);

            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client   = _httpClientFactory.CreateClient("MLService");

                // Fix 1: add X-Internal-Token header required by the ML microservice
                var request = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict")
                {
                    Content = new StringContent(featuresJson, Encoding.UTF8, "application/json")
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
                    _logger.LogWarning("ML service returned {Code} for etudiantId={Id}", response.StatusCode, etudiantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML service unreachable for etudiantId={Id}, scoreRisque=0", etudiantId);
            }

            var annee = $"{DateTime.UtcNow.Year}/{DateTime.UtcNow.Year + 1}";

            var prediction = new PredictionML
            {
                EtudiantId  = etudiantId,
                TypeModele  = "RisqueEchec",
                ScoreRisque = scoreRisque,
                Annee       = annee,
                CreeLe      = DateTime.UtcNow
            };

            _context.PredictionsML.Add(prediction);
            await _context.SaveChangesAsync();

            // Auto-insert an Alerte when the ML service classifies the student as
            // Eleve (≥0.50) or Critique (≥0.75).  We trust the ML label as the
            // single source of truth for thresholds — no duplicate magic numbers here.
            bool isHighRisk = niveauRisque is "Eleve" or "Critique";

            if (isHighRisk)
            {
                var niveauAlerte = niveauRisque!;   // already "Eleve" or "Critique"
                var alerte = new Alerte
                {
                    EtudiantId  = etudiantId,
                    Type        = "RisqueEchec",
                    Niveau      = niveauAlerte,
                    Message     = $"Risque élevé détecté : {niveauAlerte} ({scoreRisque:P0})",
                    Resolue     = false,
                    CreeLe      = DateTime.UtcNow
                };
                _context.Alertes.Add(alerte);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Alerte créée automatiquement : EtudiantId={Id}, Niveau={Niveau}", etudiantId, niveauAlerte);
            }

            _logger.LogInformation("Prédiction stockée : EtudiantId={Id}, ScoreRisque={Score}", etudiantId, scoreRisque);
            return CreatedAtAction(nameof(GetPrediction), new { id = prediction.Id }, prediction);
        }
    }
}
