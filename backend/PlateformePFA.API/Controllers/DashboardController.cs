using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Dashboard;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Stable colour map for the five filières — matches the frontend palette.
        private static readonly Dictionary<string, string> FiliereColors = new()
        {
            ["TCP"]  = "#94a3b8",
            ["GI"]   = "#f97316",
            ["IA"]   = "#ec4899",
            ["ROC"]  = "#a855f7",
            ["IRSI"] = "#0ea5e9",
        };

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/dashboard/activity?limit=8
        // Returns recent rows from AuditEntries (capped at 100). Powers the
        // dashboard's right-rail "Activité récente" feed.
        [HttpGet("activity")]
        public async Task<ActionResult<List<ActivityDto>>> GetActivity([FromQuery] int limit = 12)
        {
            limit = Math.Clamp(limit, 1, 100);
            var rows = await _context.AuditEntries
                .AsNoTracking()
                .OrderByDescending(a => a.CreeLe)
                .Take(limit)
                .Select(a => new ActivityDto
                {
                    Id             = a.Id,
                    Action         = a.Action,
                    UtilisateurNom = a.UtilisateurNom,
                    Message        = a.Message ?? string.Empty,
                    CreeLe         = a.CreeLe,
                })
                .ToListAsync();
            return rows;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
        {
            // 1) Per-student aggregates: average final note + total absence hours.
            //    Pulled in a single round-trip; everything below is in-memory.
            var students = await _context.Etudiants
                .AsNoTracking()
                .Select(e => new
                {
                    e.Id,
                    e.Nom,
                    e.Prenom,
                    e.Matricule,
                    e.Niveau,
                    FiliereCode = e.Filiere != null ? e.Filiere.Code : string.Empty,
                    Moyenne = (decimal?)e.Notes!
                        .Where(n => n.NoteFinal.HasValue)
                        .Average(n => n.NoteFinal),
                    Absences = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                })
                .ToListAsync();

            // 2) Latest ML prediction per student (if any). Falls back to a
            //    deterministic heuristic when no real prediction exists yet, so
            //    the dashboard is never blank on a fresh seed.
            var latestPredictions = await _context.PredictionsML
                .AsNoTracking()
                .Where(p => p.ScoreRisque.HasValue)
                .GroupBy(p => p.EtudiantId)
                .Select(g => new
                {
                    EtudiantId = g.Key,
                    Score = g.OrderByDescending(p => p.CreeLe).First().ScoreRisque,
                })
                .ToDictionaryAsync(x => x.EtudiantId, x => (decimal)x.Score!.Value);

            // 3) Active alerts count.
            var alertesActives = await _context.Alertes.CountAsync(a => !a.Resolue);

            // 4) KPIs.
            var nbEtudiants = students.Count;
            var withMoy = students.Where(s => s.Moyenne.HasValue).ToList();
            var moyGlobale = withMoy.Count > 0
                ? Math.Round(withMoy.Average(s => s.Moyenne!.Value), 2)
                : 0m;
            var tauxReussite = withMoy.Count > 0
                ? Math.Round((decimal)withMoy.Count(s => s.Moyenne!.Value >= 10m) / withMoy.Count * 100m, 1)
                : 0m;

            // 5) Notes by filière (avg of student averages — fair across cohort sizes).
            var notesByFiliere = students
                .Where(s => s.Moyenne.HasValue && !string.IsNullOrEmpty(s.FiliereCode))
                .GroupBy(s => s.FiliereCode)
                .Select(g => new NotesByFiliereDto
                {
                    Filiere = g.Key,
                    Moyenne = Math.Round(g.Average(s => s.Moyenne!.Value), 2),
                    Color = FiliereColors.TryGetValue(g.Key, out var c) ? c : "#94a3b8",
                })
                .OrderBy(x => x.Filiere)
                .ToList();

            // 6) Distribution histogram /20.
            var buckets = new (string Label, decimal Min, decimal Max)[]
            {
                ("<8",    0m,  8m),
                ("8-10",  8m,  10m),
                ("10-12", 10m, 12m),
                ("12-14", 12m, 14m),
                ("14-16", 14m, 16m),
                ("16+",   16m, 21m),
            };
            var moyenneDistribution = buckets.Select(b => new DistributionBucketDto
            {
                Label = b.Label,
                N = withMoy.Count(s => s.Moyenne!.Value >= b.Min && s.Moyenne!.Value < b.Max),
            }).ToList();

            // 7) Risk score per student: ML prediction if present, otherwise a
            //    heuristic based on moyenne + absences. Never returns NaN.
            decimal RiskScore(decimal? moy, int absH, int etuId)
            {
                if (latestPredictions.TryGetValue(etuId, out var ml)) return ml;
                if (!moy.HasValue) return 0.4m;
                var fromMoy = moy.Value < 10m ? 0.55m : (moy.Value < 12m ? 0.32m : 0.12m);
                var fromAbs = absH > 30 ? 0.22m : (absH > 18 ? 0.12m : 0m);
                return Math.Min(0.98m, fromMoy + fromAbs);
            }

            var withRisk = students
                .Select(s => new
                {
                    s.Id,
                    s.Nom,
                    s.Prenom,
                    s.Matricule,
                    s.Niveau,
                    s.FiliereCode,
                    Moyenne = s.Moyenne ?? 0m,
                    s.Absences,
                    Score = RiskScore(s.Moyenne, s.Absences, s.Id),
                })
                .ToList();

            // 8) Risk breakdown.
            var riskBreakdown = new List<RiskBucketDto>
            {
                new() { Label = "Faible", N = withRisk.Count(s => s.Score < 0.35m),                       Color = "#16a34a" },
                new() { Label = "Modéré", N = withRisk.Count(s => s.Score >= 0.35m && s.Score < 0.65m),   Color = "#f59e0b" },
                new() { Label = "Élevé",  N = withRisk.Count(s => s.Score >= 0.65m),                     Color = "#dc2626" },
            };

            // 9) Absence trend — last 14 ISO weeks. Bucket on the host side
            //    because SQL Server's DATEPART(week, ...) varies by language
            //    settings and this is only ~400 rows seeded.
            var since = DateTime.UtcNow.Date.AddDays(-14 * 7);
            var rawAbsences = await _context.Absences
                .AsNoTracking()
                .Where(a => a.DateAbsence >= since)
                .Select(a => new { a.DateAbsence, a.NombreHeures })
                .ToListAsync();

            var trend = new List<AbsenceTrendDto>();
            for (int w = 13; w >= 0; w--)
            {
                var weekStart = DateTime.UtcNow.Date.AddDays(-w * 7 - 6);
                var weekEnd   = weekStart.AddDays(7);
                var heures = rawAbsences
                    .Where(a => a.DateAbsence >= weekStart && a.DateAbsence < weekEnd)
                    .Sum(a => a.NombreHeures);
                trend.Add(new AbsenceTrendDto
                {
                    Semaine = $"S{14 - w}",
                    Heures = heures,
                });
            }

            // 10) Top à risque — 6 students with the highest score, breaking ties
            //     on lowest moyenne so chronic low performers surface first.
            var topARisque = withRisk
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Moyenne)
                .Take(6)
                .Select(s => new TopRisqueDto
                {
                    Id = s.Id,
                    NomComplet = $"{s.Prenom} {s.Nom}".Trim(),
                    Matricule = s.Matricule,
                    Filiere = s.FiliereCode,
                    Niveau = s.Niveau,
                    Moyenne = Math.Round(s.Moyenne, 2),
                    Absences = s.Absences,
                    ScoreRisque = Math.Round(s.Score, 3),
                })
                .ToList();

            // ── Deltas (Phase 1 task 1.5) ────────────────────────────────────
            // Compare current-period averages against the previous semester so
            // the dashboard's KPI tiles show real movement instead of literals.
            var (currentSem, prevAnnee, prevSem) = ResolvePreviousPeriod();

            var prevStudents = await _context.Etudiants
                .AsNoTracking()
                .Select(e => new
                {
                    Moyenne = (decimal?)e.Notes!
                        .Where(n => n.NoteFinal.HasValue && n.Annee == prevAnnee && n.Semestre == prevSem)
                        .Average(n => n.NoteFinal),
                })
                .ToListAsync();

            var prevWithMoy = prevStudents.Where(s => s.Moyenne.HasValue).ToList();
            var prevMoy = prevWithMoy.Count > 0
                ? Math.Round(prevWithMoy.Average(s => s.Moyenne!.Value), 2)
                : 0m;
            var prevReussite = prevWithMoy.Count > 0
                ? Math.Round((decimal)prevWithMoy.Count(s => s.Moyenne!.Value >= 10m) / prevWithMoy.Count * 100m, 1)
                : 0m;

            // Active alerts 7 days ago: created before the cutoff and not yet resolved
            // by then. Captures the "trend over the last week" the UI needs.
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var prevActiveAlerts = await _context.Alertes
                .CountAsync(a => a.CreeLe < weekAgo && (a.ResolueeLe == null || a.ResolueeLe >= weekAgo));

            // Student count trend: % growth in CreeLe last 30 days vs the prior 30 days.
            var since30 = DateTime.UtcNow.AddDays(-30);
            var since60 = DateTime.UtcNow.AddDays(-60);
            var newLast30 = await _context.Etudiants.CountAsync(e => e.CreeLe >= since30);
            var newPrev30 = await _context.Etudiants.CountAsync(e => e.CreeLe < since30 && e.CreeLe >= since60);
            var nbDelta = newPrev30 == 0 ? 0m : Math.Round((decimal)(newLast30 - newPrev30) / newPrev30 * 100m, 1);

            // ── Sparkline + side stats (Phase 1 task 1.6) ────────────────────
            // Cumulative student count at the end of each of the last 14 weeks,
            // oldest first so the bars render left→right naturally on the UI.
            // One DB roundtrip (vs 14 CountAsync calls) — sort once, then
            // binary-search each weekEnd cutoff in memory.
            var creeLeDates = await _context.Etudiants
                .Select(e => e.CreeLe)
                .ToListAsync();
            creeLeDates.Sort();
            var sparkPoints = new List<int>();
            for (int w = 13; w >= 0; w--)
            {
                var weekEnd = DateTime.UtcNow.Date.AddDays(-w * 7);
                int lo = 0, hi = creeLeDates.Count;
                while (lo < hi)
                {
                    var mid = (lo + hi) >> 1;
                    if (creeLeDates[mid] < weekEnd) lo = mid + 1;
                    else hi = mid;
                }
                sparkPoints.Add(lo);
            }
            var nouveaux = await _context.Etudiants.CountAsync(e => e.CreeLe >= weekAgo);
            // No soft-delete column on Etudiant yet — retraits stays at 0 until
            // a DesinscritLe field is added. Kept in the DTO so the UI binds.
            var retraits = 0;

            return new DashboardSummaryDto
            {
                Kpis = new KpisDto
                {
                    NbEtudiants = nbEtudiants,
                    MoyGlobale = moyGlobale,
                    TauxReussite = tauxReussite,
                    AlertesActives = alertesActives,
                    NbEtudiantsDelta = nbDelta,
                    MoyGlobaleDelta = Math.Round(moyGlobale - prevMoy, 2),
                    TauxReussiteDelta = Math.Round(tauxReussite - prevReussite, 1),
                    AlertesActivesDelta = alertesActives - prevActiveAlerts,
                    CompareLabel = $"vs {prevSem} {prevAnnee}",
                },
                NotesByFiliere = notesByFiliere,
                MoyenneDistribution = moyenneDistribution,
                RiskBreakdown = riskBreakdown,
                AbsenceTrend = trend,
                TopARisque = topARisque,
                EtudiantsParSemaine = sparkPoints,
                NouveauxCetteSemaine = nouveaux,
                RetraitsCetteSemaine = retraits,
            };
        }

        /// <summary>
        /// Resolves the previous comparable academic period.
        /// School year flips Sep 1; S1 ≈ Sep–Jan, S2 ≈ Feb–Jul. When we're in
        /// S2 the comparison is the same year's S1; when we're in S1 it's the
        /// previous year's S2 (because moving back one slot crosses years).
        /// </summary>
        private static (string CurrentSem, string PrevAnnee, string PrevSem) ResolvePreviousPeriod()
        {
            var now = DateTime.UtcNow;
            var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
            var currentSem = now.Month >= 2 && now.Month <= 8 ? "S2" : "S1";
            if (currentSem == "S2")
                return (currentSem, $"{startYear}/{startYear + 1}", "S1");
            return (currentSem, $"{startYear - 1}/{startYear}", "S2");
        }
    }
}
