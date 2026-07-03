using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.ML;
using PlateformePFA.API.DTOs.Predictions;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.API.Helpers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PredictionsController : ControllerBase
    {
        private readonly AppDbContext        _context;
        private readonly IHttpClientFactory  _httpClientFactory;
        private readonly IConfiguration      _configuration;
        private readonly ILogger<PredictionsController> _logger;
        private readonly IAuditService _audit;
        private readonly RiskScorer _riskScorer;
        private readonly IAlerteService _alertes;

        public PredictionsController(
            AppDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PredictionsController> logger,
            IAuditService audit,
            RiskScorer riskScorer,
            IAlerteService alertes)
        {
            _context           = context;
            _httpClientFactory = httpClientFactory;
            _configuration     = configuration;
            _logger            = logger;
            _audit             = audit;
            _riskScorer        = riskScorer;
            _alertes           = alertes;
        }

        // GET: api/predictions/metrics — proxies the ML service's /metrics so
        // the frontend never sees ML_INTERNAL_TOKEN. Returns last-known values
        // from metadata.json or live-computed values from eval_set.parquet
        // depending on what the ML service has on disk.
        [HttpGet("metrics")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<MlMetricsDto>> GetMlMetrics()
        {
            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client   = _httpClientFactory.CreateClient("MLService");

                var request = new HttpRequestMessage(HttpMethod.Get, $"{mlApiUrl}/metrics");
                request.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"]);

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ML /metrics returned {Code}", response.StatusCode);
                    return StatusCode(StatusCodes.Status502BadGateway,
                        new { message = "ML metrics unavailable" });
                }

                var body = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<MlMetricsDto>(body, new JsonSerializerOptions
                {
                    // FastAPI returns snake_case (n_samples, model_version, trained_at);
                    // map them onto our PascalCase properties at deserialize time.
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true,
                });

                return dto ?? new MlMetricsDto();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML metrics fetch failed");
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "ML service unreachable" });
            }
        }

        // POST: api/predictions/retrain — admin-only proxy to /predict/retrain.
        // The ML service kicks off a fresh training run, atomically swaps the
        // new artefacts over the live ones, and returns a summary blob.
        [HttpPost("retrain")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Retrain()
        {
            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client   = _httpClientFactory.CreateClient("MLService");
                var req = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict/retrain");
                req.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"]);

                // Retraining can take a while — bump the per-call timeout above
                // the default 100s so we don't 502 the user mid-run.
                client.Timeout = TimeSpan.FromMinutes(10);

                using var resp = await client.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ML retrain returned {Code}: {Body}", resp.StatusCode, body);
                    return StatusCode((int)resp.StatusCode, body);
                }

                await _audit.LogAsync(
                    action: "MlRetrain",
                    entityType: "ML",
                    message: "Modèles réentraînés",
                    userId: GetUserId(),
                    userName: GetUserName());

                return Content(body, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retrain proxy failed");
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "ML service unreachable" });
            }
        }

        // GET: api/predictions/summary — feeds the Predictions page
        [HttpGet("summary")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<PredictionsSummaryDto>> GetSummary()
        {
            // Per-student snapshot: moyenne + absence hours + filière + niveau.
            var students = await _context.Etudiants
                .AsNoTracking()
                .Where(e => e.DesinscritLe == null)
                .Select(e => new
                {
                    e.Id,
                    e.Nom,
                    e.Prenom,
                    e.Matricule,
                    e.Niveau,
                    Filiere = e.Filiere != null ? e.Filiere.Code : string.Empty,
                    Moyenne = (decimal?)e.Notes!
                        .Where(n => n.NoteFinal.HasValue)
                        .Average(n => n.NoteFinal),
                    Absences = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                })
                .ToListAsync();

            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();

            var enriched = students.Select(s => new
            {
                s.Id,
                s.Nom,
                s.Prenom,
                s.Matricule,
                s.Niveau,
                s.Filiere,
                Moyenne = s.Moyenne ?? 0m,
                s.Absences,
                Score = _riskScorer.Score(s.Moyenne, s.Absences, s.Id, latestPredictions),
            }).ToList();

            // Scatter: cap at 400 points to keep the SVG renderer happy.
            var scatter = enriched
                .Where(s => s.Moyenne > 0m)
                .Take(400)
                .Select(s => new ScatterPointDto
                {
                    X = s.Absences,
                    Y = Math.Round(s.Moyenne, 2),
                    R = RiskScorer.Bucket(s.Score),
                })
                .ToList();

            // KPIs. AUC + model version come from the ML service so the page
            // header and the runs table never drift from the dashboard card.
            var (liveAuc, liveModelVersion) = await TryFetchMlSummaryAsync();
            var kpis = new PredictionKpisDto
            {
                Evalues = enriched.Count,
                RisqueEleve = enriched.Count(s => s.Score >= 0.65m),
                RisqueModere = enriched.Count(s => s.Score >= 0.35m && s.Score < 0.65m),
                Auc = liveAuc,
                // latestPredictions is keyed by the students who have a real ML score.
                MlScored = enriched.Count(s => latestPredictions.ContainsKey(s.Id)),
            };

            var topARisque = enriched
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Moyenne)
                .Take(10)
                .Select(s => new TopRisqueRowDto
                {
                    Id          = s.Id,
                    NomComplet  = $"{s.Prenom} {s.Nom}".Trim(),
                    Matricule   = s.Matricule,
                    Filiere     = s.Filiere,
                    Niveau      = s.Niveau,
                    Moyenne     = Math.Round(s.Moyenne, 2),
                    Absences    = s.Absences,
                    ScoreRisque = Math.Round(s.Score, 3),
                })
                .ToList();

            // Runs view: group real predictions by day + filière. The schema has
            // no PredictionRun table, so we synthesize "run" rows on the fly.
            var rawRuns = await _context.PredictionsML
                .AsNoTracking()
                .Where(p => p.ScoreRisque.HasValue)
                .Join(_context.Etudiants.Where(e => e.DesinscritLe == null),
                    p => p.EtudiantId,
                    e => e.Id,
                    (p, e) => new { p.CreeLe, p.ScoreRisque, p.Niveau, FiliereId = e.FiliereId })
                .Join(_context.Filieres,
                    pe => pe.FiliereId,
                    f => f.Id,
                    (pe, f) => new { pe.CreeLe, pe.ScoreRisque, pe.Niveau, FiliereCode = f.Code })
                .ToListAsync();

            var runs = rawRuns
                .GroupBy(r => new { Date = r.CreeLe.Date, r.FiliereCode })
                .OrderByDescending(g => g.Key.Date)
                .Take(10)
                .Select((g, idx) => new RunDto
                {
                    Id          = $"PR-{g.Key.Date:yyMMdd}-{g.Key.FiliereCode}",
                    Date        = g.Key.Date.ToString("dd MMM yyyy", new CultureInfo("fr-FR")),
                    Filiere     = g.Key.FiliereCode,
                    Etudiants   = g.Count(),
                    RisqueEleve = g.Count(r => r.Niveau is "Eleve" or "Critique"),
                    Modele      = $"XGBoost {liveModelVersion}",
                    Auc         = liveAuc,
                    Statut      = "OK",
                })
                .ToList();

            return new PredictionsSummaryDto
            {
                Kpis = kpis,
                Scatter = scatter,
                TopARisque = topARisque,
                Runs = runs,
            };
        }

        // POST: api/predictions/batch — run predictions for a filtered cohort.
        // Calls the ML service synchronously per student (cohort sizes are
        // small in this PFA). Returns counts; per-student details land in the
        // PredictionsML table and trigger automatic alerts.
        [HttpPost("batch")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<BatchResultDto>> RunBatch([FromBody] BatchRequestDto request)
        {
            var query = _context.Etudiants
                .Include(e => e.Notes)
                .Include(e => e.Absences)
                .Where(e => e.DesinscritLe == null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.FiliereCode))
            {
                query = query.Where(e => e.Filiere != null && e.Filiere.Code == request.FiliereCode);
            }
            if (!string.IsNullOrWhiteSpace(request.Niveau))
            {
                query = query.Where(e => e.Niveau == request.Niveau);
            }

            var students = await query.ToListAsync();
            var result = new BatchResultDto { Total = students.Count };

            var targetAnnee = request.Annee ?? _configuration["CurrentAcademicYear"] ?? AcademicPeriod.CurrentAcademicYear();
            var targetSemestre = request.Semestre;
            var (windowStart, windowEnd) = AcademicPeriod.SemesterDateRange(targetAnnee, targetSemestre);

            var validStudents = new List<(Etudiant Student, decimal MoyenneGenerale, double TauxAbsence, int NbModules, double EcartTypeModules, int NbEchecsAnterieurs)>();

            foreach (var student in students)
            {
                var notes = student.Notes ?? new List<Note>();
                var absences = student.Absences ?? new List<Absence>();

                var filteredNotes = notes
                    .Where(n => n.Annee == targetAnnee && (targetSemestre == null || n.Semestre == targetSemestre))
                    .ToList();
                var notesAvecFinal = filteredNotes.Where(n => n.NoteFinal.HasValue).ToList();
                int nbModules = filteredNotes.Select(n => n.ModuleId).Distinct().Count();

                if (nbModules == 0 || notesAvecFinal.Count == 0)
                {
                    result.Skipped++;
                    continue;
                }

                var filteredAbsences = absences
                    .Where(a => a.DateAbsence >= windowStart && a.DateAbsence < windowEnd)
                    .ToList();

                decimal moyenneGenerale = notesAvecFinal.Average(n => n.NoteFinal!.Value);
                double scheduledHours = nbModules * AcademicPeriod.SessionsPerModule;
                int absenceHours = filteredAbsences.Where(a => !a.Justifiee).Sum(a => a.NombreHeures);
                double tauxAbsence = Math.Min(absenceHours / scheduledHours, 1.0);

                double ecartTypeModules = 0;
                if (notesAvecFinal.Count >= 2)
                {
                    var mean = (double)notesAvecFinal.Average(n => n.NoteFinal!.Value);
                    ecartTypeModules = Math.Sqrt(notesAvecFinal.Sum(n => ((double)n.NoteFinal!.Value - mean) * ((double)n.NoteFinal!.Value - mean)) / (notesAvecFinal.Count - 1));
                }

                var priorPeriods = notes
                    .Where(n => n.NoteFinal.HasValue)
                    .Select(n => new { n.Annee, n.Semestre })
                    .Distinct()
                    .Where(p => string.Compare(p.Annee, targetAnnee) < 0 || (string.Compare(p.Annee, targetAnnee) == 0 && targetSemestre != null && string.Compare(p.Semestre, targetSemestre) < 0))
                    .ToList();
                int nbEchecsAnterieurs = 0;
                foreach (var pp in priorPeriods)
                {
                    var periodNotes = notes.Where(n => n.Annee == pp.Annee && n.Semestre == pp.Semestre && n.NoteFinal.HasValue).ToList();
                    if (periodNotes.Count > 0 && periodNotes.Average(n => n.NoteFinal!.Value) < 10)
                        nbEchecsAnterieurs++;
                }

                validStudents.Add((student, moyenneGenerale, tauxAbsence, nbModules, ecartTypeModules, nbEchecsAnterieurs));
            }

            if (validStudents.Count > 0)
            {
                // ML rejects batches > 500. Chunk so a large cohort doesn't fail
                // wholesale, and reconcile per chunk: a failed chunk only marks
                // its own students MlUnavailable, leaving the rest predicted.
                const int MlBatchLimit = 500;
                var mlResults = new MlResponseItem?[validStudents.Count];
                var chunkFailed = new bool[validStudents.Count];

                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client = _httpClientFactory.CreateClient("MLService");

                for (int offset = 0; offset < validStudents.Count; offset += MlBatchLimit)
                {
                    var chunk = validStudents.Skip(offset).Take(MlBatchLimit).ToList();
                    var requestBody = chunk.Select(vs => new
                    {
                        moyenne_generale = vs.MoyenneGenerale,
                        taux_absence = vs.TauxAbsence,
                        nb_modules = vs.NbModules,
                        ecart_type_modules = vs.EcartTypeModules,
                        nb_echecs_anterieurs = vs.NbEchecsAnterieurs,
                    }).ToList();
                    string jsonBody = JsonSerializer.Serialize(requestBody);

                    try
                    {
                        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict/batch")
                        {
                            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
                        };
                        httpRequest.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"]);

                        var response = await client.SendAsync(httpRequest);
                        if (response.IsSuccessStatusCode)
                        {
                            var body = await response.Content.ReadAsStringAsync();
                            var items = JsonSerializer.Deserialize<List<MlResponseItem>>(body, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            for (int j = 0; j < chunk.Count; j++)
                                mlResults[offset + j] = (items != null && j < items.Count) ? items[j] : null;
                        }
                        else
                        {
                            for (int j = 0; j < chunk.Count; j++) chunkFailed[offset + j] = true;
                            _logger.LogWarning("ML service returned {Code} for batch prediction chunk at offset {Offset}", response.StatusCode, offset);
                        }
                    }
                    catch (Exception ex)
                    {
                        for (int j = 0; j < chunk.Count; j++) chunkFailed[offset + j] = true;
                        _logger.LogWarning(ex, "ML service unreachable for batch prediction chunk at offset {Offset}", offset);
                    }
                }

                for (int i = 0; i < validStudents.Count; i++)
                {
                    var (student, moyenneGenerale, tauxAbsence, nbModules, _ /*ecartTypeModules*/, _ /*nbEchecsAnterieurs*/) = validStudents[i];

                    decimal? scoreRisque = null;
                    string? niveauRisque = null;
                    string studentStatus;

                    if (chunkFailed[i])
                    {
                        studentStatus = "MlUnavailable";
                        result.Failed++;
                    }
                    else
                    {
                        var item = mlResults[i];
                        if (item != null && item.Probabilite >= 0.0m && item.Probabilite <= 1.0m &&
                            (item.NiveauRisque == "Faible" || item.NiveauRisque == "Moyen" || item.NiveauRisque == "Eleve" || item.NiveauRisque == "Élevé" || item.NiveauRisque == "Critique"))
                        {
                            scoreRisque = item.Probabilite;
                            niveauRisque = item.NiveauRisque == "Élevé" ? "Eleve" : item.NiveauRisque;
                            studentStatus = "Ok";
                            result.Predicted++;
                        }
                        else
                        {
                            studentStatus = "InvalidResponse";
                            result.Failed++;
                        }
                    }

                    var prediction = new PredictionML
                    {
                        EtudiantId  = student.Id,
                        TypeModele  = "RisqueEchec",
                        ScoreRisque = scoreRisque,
                        Niveau      = niveauRisque,
                        Status      = studentStatus,
                        Annee       = targetAnnee,
                        CreeLe      = DateTime.UtcNow,
                    };
                    _context.PredictionsML.Add(prediction);

                    if (studentStatus == "Ok" && (niveauRisque is "Eleve" or "Critique"))
                    {
                        result.RisqueEleve++;

                        // Dedupe + escalate instead of appending a fresh alert on
                        // every batch run (otherwise re-running fills Alertes with dupes).
                        await _alertes.UpsertRiskAlertAsync(
                            student.Id, niveauRisque,
                            $"Risque élevé détecté : {niveauRisque} ({scoreRisque:P0})");
                    }
                }

                await _context.SaveChangesAsync();
            }

            await _audit.LogAsync(
                action: "PredictionBatch",
                entityType: "PredictionML",
                message: $"Batch {request.FiliereCode ?? "TOUS"}/{request.Niveau ?? "—"} : "
                       + $"{result.Predicted}/{result.Total} prédits, {result.RisqueEleve} risque élevé",
                userId: GetUserId(),
                userName: GetUserName());

            return result;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private string GetUserName()
        {
            return User.FindFirst("NomComplet")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? "Système";
        }

        // GET: api/predictions?page=1&pageSize=20&etudiantId=5&status=Ok
        [HttpGet]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<PaginatedResult<PredictionML>>> GetPredictions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? etudiantId = null,
            [FromQuery] string? status = null)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.PredictionsML.AsNoTracking()
                .Where(p => p.Etudiant.DesinscritLe == null)
                .AsQueryable();
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
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<PredictionML>> GetPrediction(int id)
        {
            var prediction = await _context.PredictionsML
                .Include(p => p.Etudiant)
                .FirstOrDefaultAsync(p => p.Id == id && p.Etudiant.DesinscritLe == null);

            if (prediction == null) return NotFound(new { message = "Prédiction introuvable." });
            return prediction;
        }

        // GET: api/predictions/etudiant/5  (latest 200 for one student)
        [HttpGet("etudiant/{etudiantId}")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<IEnumerable<PredictionML>>> GetPredictionsByEtudiant(int etudiantId)
        {
            return await _context.PredictionsML
                .AsNoTracking()
                .Include(p => p.Etudiant)
                .Where(p => p.EtudiantId == etudiantId && p.Etudiant.DesinscritLe == null)
                .OrderByDescending(p => p.CreeLe)
                .Take(200)
                .ToListAsync();
        }

        // GET: api/predictions/explain/5
        [HttpGet("explain/{etudiantId:int}")]
        [Authorize(Roles = "Admin,Responsable,Enseignant")]
        public async Task<ActionResult<ShapExplainDto>> ExplainRisk(
            int etudiantId,
            CancellationToken ct,
            [FromQuery] string? annee = null,
            [FromQuery] string? semestre = null)
        {
            var etudiant = await _context.Etudiants
                .AsNoTracking()
                .Include(e => e.Notes)
                .Include(e => e.Absences)
                .FirstOrDefaultAsync(e => e.Id == etudiantId && e.DesinscritLe == null, ct);

            if (etudiant is null) return NotFound();

            var notes    = etudiant.Notes    ?? new List<Note>();
            var absences = etudiant.Absences ?? new List<Absence>();

            // Per-period features, same scope as PredictForEtudiant: SHAP must
            // explain the same inputs the prediction was computed from, otherwise
            // the breakdown describes a different student profile than the score.
            var targetAnnee = annee ?? _configuration["CurrentAcademicYear"] ?? AcademicPeriod.CurrentAcademicYear();
            var targetSemestre = semestre;
            var (windowStart, windowEnd) = AcademicPeriod.SemesterDateRange(targetAnnee, targetSemestre);

            var filteredNotes = notes
                .Where(n => n.Annee == targetAnnee && (targetSemestre == null || n.Semestre == targetSemestre))
                .ToList();
            var notesAvecFinal = filteredNotes.Where(n => n.NoteFinal.HasValue).ToList();
            int nbModules = filteredNotes.Select(n => n.ModuleId).Distinct().Count();

            if (nbModules == 0 || notesAvecFinal.Count == 0)
                return UnprocessableEntity("Données insuffisantes pour l'explication SHAP.");

            var filteredAbsences = absences
                .Where(a => a.DateAbsence >= windowStart && a.DateAbsence < windowEnd)
                .ToList();

            decimal moyenneGenerale = notesAvecFinal.Average(n => n.NoteFinal!.Value);
            double scheduledHours   = nbModules * AcademicPeriod.SessionsPerModule;
            int absenceHours        = filteredAbsences.Where(a => !a.Justifiee).Sum(a => a.NombreHeures);
            double tauxAbsence      = Math.Min(absenceHours / scheduledHours, 1.0);

            var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
            var client   = _httpClientFactory.CreateClient("MLService");

            double ecartTypeModulesExplain = 0;
            if (notesAvecFinal.Count >= 2)
            {
                var meanExplain = (double)notesAvecFinal.Average(n => n.NoteFinal!.Value);
                ecartTypeModulesExplain = Math.Sqrt(notesAvecFinal.Sum(n => ((double)n.NoteFinal!.Value - meanExplain) * ((double)n.NoteFinal!.Value - meanExplain)) / (notesAvecFinal.Count - 1));
            }

            // Prior-period failures only, matching PredictForEtudiant.
            var priorPeriodsExplain = notes
                .Where(n => n.NoteFinal.HasValue)
                .Select(n => new { n.Annee, n.Semestre })
                .Distinct()
                .Where(p => string.Compare(p.Annee, targetAnnee) < 0 || (string.Compare(p.Annee, targetAnnee) == 0 && targetSemestre != null && string.Compare(p.Semestre, targetSemestre) < 0))
                .ToList();
            int nbEchecsAnterieursExplain = 0;
            foreach (var dp in priorPeriodsExplain)
            {
                var periodNotesExplain = notes.Where(n => n.Annee == dp.Annee && n.Semestre == dp.Semestre && n.NoteFinal.HasValue).ToList();
                if (periodNotesExplain.Count > 0 && periodNotesExplain.Average(n => n.NoteFinal!.Value) < 10)
                    nbEchecsAnterieursExplain++;
            }

            var body = JsonSerializer.Serialize(new
            {
                moyenne_generale = (double)moyenneGenerale,
                taux_absence     = tauxAbsence,
                nb_modules       = nbModules,
                ecart_type_modules = ecartTypeModulesExplain,
                nb_echecs_anterieurs = nbEchecsAnterieursExplain,
            });

            var req = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict/explain")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"] ?? "");

            HttpResponseMessage resp;
            try { resp = await client.SendAsync(req, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML /predict/explain unreachable for etudiantId={Id}", etudiantId);
                return StatusCode(503, "ML service unavailable");
            }

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, "ML explain failed");

            var json   = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ShapExplainDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return Ok(result);
        }

        // GET: api/predictions/forecast/5 — proxies the ML service's /forecast
        // to predict the student's next-period average grade (0-20), as opposed
        // to /predict which returns a failure-risk probability.
        [HttpGet("forecast/{etudiantId:int}")]
        [Authorize(Roles = "Admin,Responsable,Enseignant")]
        public async Task<ActionResult<ForecastDto>> ForecastMoyenne(
            int etudiantId,
            CancellationToken ct,
            [FromQuery] string? annee = null,
            [FromQuery] string? semestre = null)
        {
            var etudiant = await _context.Etudiants
                .AsNoTracking()
                .Include(e => e.Notes)
                .Include(e => e.Absences)
                .FirstOrDefaultAsync(e => e.Id == etudiantId && e.DesinscritLe == null, ct);

            if (etudiant is null) return NotFound();

            var notes    = etudiant.Notes    ?? new List<Note>();
            var absences = etudiant.Absences ?? new List<Absence>();

            // Per-period features, same scope as PredictForEtudiant: the forecast
            // model was trained on per-period moyenne/absences predicting the next
            // period, so all-time aggregates would feed it out-of-distribution inputs.
            var targetAnnee = annee ?? _configuration["CurrentAcademicYear"] ?? AcademicPeriod.CurrentAcademicYear();
            var targetSemestre = semestre;
            var (windowStart, windowEnd) = AcademicPeriod.SemesterDateRange(targetAnnee, targetSemestre);

            var filteredNotes = notes
                .Where(n => n.Annee == targetAnnee && (targetSemestre == null || n.Semestre == targetSemestre))
                .ToList();
            var notesAvecFinal = filteredNotes.Where(n => n.NoteFinal.HasValue).ToList();
            int nbModules = filteredNotes.Select(n => n.ModuleId).Distinct().Count();

            if (nbModules == 0 || notesAvecFinal.Count == 0)
                return UnprocessableEntity("Données insuffisantes pour la prévision.");

            // ML side rejects nb_modules outside 1–30 with a 422; validate here so
            // the client gets a clear message instead of "ML forecast failed".
            if (nbModules > 30)
                return UnprocessableEntity("Nombre de modules invalide pour la prévision (doit être entre 1 et 30).");

            var filteredAbsences = absences
                .Where(a => a.DateAbsence >= windowStart && a.DateAbsence < windowEnd)
                .ToList();

            decimal moyenneActuelle = notesAvecFinal.Average(n => n.NoteFinal!.Value);
            double scheduledHours   = nbModules * AcademicPeriod.SessionsPerModule;
            int absenceHours        = filteredAbsences.Where(a => !a.Justifiee).Sum(a => a.NombreHeures);
            double tauxAbsence      = Math.Min(absenceHours / scheduledHours, 1.0);

            double ecartTypeModules = 0;
            if (notesAvecFinal.Count >= 2)
            {
                var mean = (double)notesAvecFinal.Average(n => n.NoteFinal!.Value);
                ecartTypeModules = Math.Sqrt(notesAvecFinal.Sum(n => ((double)n.NoteFinal!.Value - mean) * ((double)n.NoteFinal!.Value - mean)) / (notesAvecFinal.Count - 1));
            }

            // Prior-period failures only (strictly before the target period),
            // matching PredictForEtudiant — counting the current period would leak
            // the outcome the model is trying to anticipate.
            var priorPeriods = notes
                .Where(n => n.NoteFinal.HasValue)
                .Select(n => new { n.Annee, n.Semestre })
                .Distinct()
                .Where(p => string.Compare(p.Annee, targetAnnee) < 0 || (string.Compare(p.Annee, targetAnnee) == 0 && targetSemestre != null && string.Compare(p.Semestre, targetSemestre) < 0))
                .ToList();
            int nbEchecsAnterieurs = 0;
            foreach (var pp in priorPeriods)
            {
                var periodNotes = notes.Where(n => n.Annee == pp.Annee && n.Semestre == pp.Semestre && n.NoteFinal.HasValue).ToList();
                if (periodNotes.Count > 0 && periodNotes.Average(n => n.NoteFinal!.Value) < 10)
                    nbEchecsAnterieurs++;
            }

            var body = JsonSerializer.Serialize(new
            {
                moyenne_actuelle = (double)moyenneActuelle,
                taux_absence     = tauxAbsence,
                nb_modules       = nbModules,
                ecart_type_modules = ecartTypeModules,
                nb_echecs_anterieurs = nbEchecsAnterieurs,
            });

            var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
            var client   = _httpClientFactory.CreateClient("MLService");

            var req = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/forecast")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"] ?? "");

            HttpResponseMessage resp;
            try { resp = await client.SendAsync(req, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML /forecast unreachable for etudiantId={Id}", etudiantId);
                return StatusCode(503, "ML service unavailable");
            }

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, "ML forecast failed");

            var json   = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ForecastDto>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
            });

            return Ok(result);
        }

        [HttpPost("predict/{etudiantId}")]
        [Authorize(Roles = "Admin,Responsable")]
        public async Task<ActionResult<PredictionML>> PredictForEtudiant(
            int etudiantId,
            [FromQuery] string? annee = null,
            [FromQuery] string? semestre = null)
        {
            var etudiant = await _context.Etudiants
                .Include(e => e.Notes)
                .Include(e => e.Absences)
                .FirstOrDefaultAsync(e => e.Id == etudiantId && e.DesinscritLe == null);

            if (etudiant == null) return NotFound(new { message = "Étudiant introuvable." });

            var targetAnnee = annee ?? _configuration["CurrentAcademicYear"] ?? AcademicPeriod.CurrentAcademicYear();
            var targetSemestre = semestre;
            var (windowStart, windowEnd) = AcademicPeriod.SemesterDateRange(targetAnnee, targetSemestre);

            var notes = etudiant.Notes ?? new List<Note>();
            var absences = etudiant.Absences ?? new List<Absence>();

            var filteredNotes = notes
                .Where(n => n.Annee == targetAnnee && (targetSemestre == null || n.Semestre == targetSemestre))
                .ToList();
            var notesAvecFinal = filteredNotes.Where(n => n.NoteFinal.HasValue).ToList();
            int nbModules = filteredNotes.Select(n => n.ModuleId).Distinct().Count();

            if (nbModules == 0 || notesAvecFinal.Count == 0)
            {
                return UnprocessableEntity(new
                {
                    message = "Données insuffisantes : aucune note finale enregistrée pour la période sélectionnée.",
                    nbModules,
                    nbNotesFinal = notesAvecFinal.Count,
                });
            }

            if (nbModules < 1 || nbModules > 30)
            {
                return UnprocessableEntity(new
                {
                    message = "Nombre de modules invalide pour la prédiction (doit être entre 1 et 30).",
                    nbModules
                });
            }

            var filteredAbsences = absences
                .Where(a => a.DateAbsence >= windowStart && a.DateAbsence < windowEnd)
                .ToList();

            decimal moyenneGenerale = notesAvecFinal.Average(n => n.NoteFinal!.Value);
            double scheduledHours = nbModules * AcademicPeriod.SessionsPerModule;
            int absenceHours = filteredAbsences.Where(a => !a.Justifiee).Sum(a => a.NombreHeures);
            double tauxAbsence = Math.Min(absenceHours / scheduledHours, 1.0);

            double ecartTypeModules = 0;
            if (notesAvecFinal.Count >= 2)
            {
                var mean = (double)notesAvecFinal.Average(n => n.NoteFinal!.Value);
                ecartTypeModules = Math.Sqrt(notesAvecFinal.Sum(n => ((double)n.NoteFinal!.Value - mean) * ((double)n.NoteFinal!.Value - mean)) / (notesAvecFinal.Count - 1));
            }

            var priorPeriods = notes
                .Where(n => n.NoteFinal.HasValue)
                .Select(n => new { n.Annee, n.Semestre })
                .Distinct()
                .Where(p => string.Compare(p.Annee, targetAnnee) < 0 || (string.Compare(p.Annee, targetAnnee) == 0 && targetSemestre != null && string.Compare(p.Semestre, targetSemestre) < 0))
                .ToList();
            int nbEchecsAnterieurs = 0;
            foreach (var pp in priorPeriods)
            {
                var periodNotes = notes.Where(n => n.Annee == pp.Annee && n.Semestre == pp.Semestre && n.NoteFinal.HasValue).ToList();
                if (periodNotes.Count > 0 && periodNotes.Average(n => n.NoteFinal!.Value) < 10)
                    nbEchecsAnterieurs++;
            }

            var features = new
            {
                moyenne_generale = moyenneGenerale,
                taux_absence = tauxAbsence,
                nb_modules = nbModules,
                ecart_type_modules = ecartTypeModules,
                nb_echecs_anterieurs = nbEchecsAnterieurs,
            };

            decimal? scoreRisque = null;
            string? niveauRisque = null;
            string status = "Ok";
            string featuresJson = JsonSerializer.Serialize(features);

            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client = _httpClientFactory.CreateClient("MLService");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{mlApiUrl}/predict")
                {
                    Content = new StringContent(featuresJson, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"] ?? "");

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    bool isValid = false;
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                            doc.RootElement.TryGetProperty("probabilite", out var prob) &&
                            doc.RootElement.TryGetProperty("niveau_risque", out var niveau))
                        {
                            var probVal = prob.GetDecimal();
                            var niveauVal = niveau.GetString();

                            if (probVal >= 0.0m && probVal <= 1.0m &&
                                (niveauVal == "Faible" || niveauVal == "Moyen" || niveauVal == "Eleve" || niveauVal == "Élevé" || niveauVal == "Critique"))
                            {
                                scoreRisque = probVal;
                                niveauRisque = niveauVal;
                                if (niveauRisque == "Élevé")
                                {
                                    niveauRisque = "Eleve";
                                }
                                isValid = true;
                            }
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed to parse ML response body");
                    }

                    if (!isValid)
                    {
                        status = "InvalidResponse";
                        _logger.LogWarning("ML service returned malformed or out of bounds response: {Body}", body);
                    }
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

            if (status != "Ok")
            {
                var failedPrediction = new PredictionML
                {
                    EtudiantId = etudiantId,
                    TypeModele = "RisqueEchec",
                    ScoreRisque = null,
                    Niveau = null,
                    Status = status,
                    Annee = targetAnnee,
                    CreeLe = DateTime.UtcNow,
                };
                _context.PredictionsML.Add(failedPrediction);
                await _context.SaveChangesAsync();

                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Service ML indisponible ou réponse invalide. Aucune prédiction enregistrée.",
                    status,
                });
            }

            var prediction = new PredictionML
            {
                EtudiantId = etudiantId,
                TypeModele = "RisqueEchec",
                ScoreRisque = scoreRisque,
                Niveau = niveauRisque,
                Status = status,
                Annee = targetAnnee,
                CreeLe = DateTime.UtcNow,
            };

            _context.PredictionsML.Add(prediction);
            await _context.SaveChangesAsync();

            if (niveauRisque is "Eleve" or "Critique")
            {
                await _alertes.UpsertRiskAlertAsync(
                    etudiantId, niveauRisque,
                    $"Risque élevé détecté : {niveauRisque} ({scoreRisque:P0})");
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

        /// agrees with the dashboard card. Returns <c>(0.0, "—")</c> if the
        /// ML service is unreachable — the page still renders, just without
        /// the headline number.
        /// </summary>
        private async Task<(decimal Auc, string ModelVersion)> TryFetchMlSummaryAsync()
        {
            try
            {
                var mlApiUrl = _configuration["ML_API_URL"] ?? "http://ml-service:8000";
                var client   = _httpClientFactory.CreateClient("MLService");
                var req = new HttpRequestMessage(HttpMethod.Get, $"{mlApiUrl}/metrics");
                req.Headers.Add("X-Internal-Token", _configuration["ML_INTERNAL_TOKEN"]);
                using var resp = await client.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return (0m, "—");

                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var aucProp = doc.RootElement.TryGetProperty("auc", out var a) ? a.GetDecimal() : 0m;
                var ver     = doc.RootElement.TryGetProperty("model_version", out var v)
                              ? (v.GetString() ?? "—") : "—";
                return (Math.Round(aucProp, 2), ver);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch ML summary");
                return (0m, "—");
            }
        }

        private class MlResponseItem
        {
            [JsonPropertyName("probabilite")]
            public decimal Probabilite { get; set; }

            [JsonPropertyName("niveau_risque")]
            public string NiveauRisque { get; set; } = "Faible";
        }
    }
}
