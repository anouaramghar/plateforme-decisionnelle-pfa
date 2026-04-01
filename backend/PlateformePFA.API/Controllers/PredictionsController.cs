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
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PredictionsController> _logger;

        public PredictionsController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PredictionsController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: api/predictions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PredictionML>>> GetPredictions()
        {
            return await _context.PredictionsML
                .Include(p => p.Etudiant)
                .OrderByDescending(p => p.DatePrediction)
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
                .OrderByDescending(p => p.DatePrediction)
                .ToListAsync();
        }

        // POST: api/predictions/predict/5
        [HttpPost("predict/{etudiantId}")]
        public async Task<ActionResult<PredictionML>> PredictForEtudiant(int etudiantId)
        {
            var etudiant = await _context.Etudiants
                .Include(e => e.Notes)
                .Include(e => e.Presences)
                .FirstOrDefaultAsync(e => e.Id == etudiantId);

            if (etudiant == null) return NotFound(new { message = "Étudiant introuvable." });

            var notes = etudiant.Notes ?? new List<Note>();
            var presences = etudiant.Presences ?? new List<Presence>();

            float moyenneGenerale = notes.Any() ? notes.Average(n => n.Valeur) : 0f;
            int totalPresences = presences.Count;
            int presentsCount = presences.Count(p => p.Statut == "Present");
            float tauxPresence = totalPresences > 0 ? (float)presentsCount / totalPresences : 0f;
            int nbModules = notes.Select(n => n.ModuleId).Distinct().Count();

            var features = new
            {
                moyenne_generale = moyenneGenerale,
                taux_presence = tauxPresence,
                nb_modules = nbModules
            };

            float probabilite = 0f;
            string featuresJson = JsonSerializer.Serialize(features);

            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client = _httpClientFactory.CreateClient("MLService");

                var content = new StringContent(featuresJson, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{mlApiUrl}/predict", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("probabilite", out var prob))
                        probabilite = prob.GetSingle();
                }
                else
                {
                    _logger.LogWarning("ML service returned {StatusCode} for etudiantId={EtudiantId}", response.StatusCode, etudiantId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML service unreachable for etudiantId={EtudiantId}, storing prediction with probabilite=0", etudiantId);
            }

            var prediction = new PredictionML
            {
                EtudiantId = etudiantId,
                Modele = "RisqueEchec",
                Probabilite = probabilite,
                DatePrediction = DateTime.UtcNow,
                FeaturesJSON = featuresJson
            };

            _context.PredictionsML.Add(prediction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Prédiction stockée : EtudiantId={EtudiantId}, Probabilite={Probabilite}", etudiantId, probabilite);

            return CreatedAtAction(nameof(GetPrediction), new { id = prediction.Id }, prediction);
        }
    }
}
