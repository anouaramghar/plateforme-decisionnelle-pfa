using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;

namespace PlateformePFA.API.Services;

public class RiskScorer
{
    private readonly AppDbContext _context;

    public RiskScorer(AppDbContext context)
    {
        _context = context;
    }

    // Step-function risk-factor weights. Exposed individually so the Copilot
    // "explain" breakdown reuses the exact same values instead of duplicating
    // the magic numbers (which would silently drift if either side changed).
    public static decimal MoyenneWeight(decimal moyenne) =>
        moyenne == 0m ? 0.4m
        : moyenne < 10m ? 0.55m
        : moyenne < 12m ? 0.32m
        : 0.12m;

    public static decimal AbsenceWeight(int absencesH) =>
        absencesH > 30 ? 0.22m
        : absencesH > 18 ? 0.12m
        : 0m;

    public static decimal HeuristicScore(decimal moyenne, int absencesH) =>
        Math.Min(0.98m, MoyenneWeight(moyenne) + AbsenceWeight(absencesH));

    public decimal Score(decimal? moyenne, int absencesH, int etudiantId,
        IReadOnlyDictionary<int, decimal> latestPredictions)
    {
        if (latestPredictions.TryGetValue(etudiantId, out var ml)) return ml;
        if (!moyenne.HasValue) return 0.4m;
        return HeuristicScore(moyenne.Value, absencesH);
    }

    public async Task<Dictionary<int, decimal>> GetLatestPredictionsAsync()
    {
        return await _context.PredictionsML
            .AsNoTracking()
            .Where(p => p.Status == "Ok" && p.ScoreRisque.HasValue)
            .GroupBy(p => p.EtudiantId)
            .Select(g => new
            {
                EtudiantId = g.Key,
                Score = g.OrderByDescending(p => p.CreeLe).First().ScoreRisque,
            })
            .ToDictionaryAsync(x => x.EtudiantId, x => (decimal)x.Score!);
    }

    public static string Bucket(decimal score) =>
        score >= 0.65m ? "eleve" : score >= 0.35m ? "modere" : "faible";

    // ── Triage ranking ────────────────────────────────────────────────────────
    // The alert engine's raw severity string → a case priority.
    public static string NiveauToPriorite(string niveau) => niveau switch
    {
        "Critique" => "Critical", "Eleve" => "High", "Moyen" => "Medium", _ => "Low",
    };

    // The predicted risk score → a case priority.
    public static string RiskToPriorite(decimal score) =>
        score >= 0.80m ? "Critical" : score >= 0.60m ? "High" : score >= 0.35m ? "Medium" : "Low";

    private static int PrioriteRank(string p) => p switch
    {
        "Critical" => 3, "High" => 2, "Medium" => 1, _ => 0,
    };

    /// <summary>
    /// Triage priority = the more severe of (the model's risk score) and
    /// (the worst raw signal). This is what connects intervention to prediction:
    /// a high ML risk escalates a case even if the raw alerts look mild, and a
    /// raw "Critique" signal is never down-ranked by an optimistic score.
    /// </summary>
    public static string SuggestedPriorite(decimal score, string maxSignalNiveau)
    {
        var fromRisk   = RiskToPriorite(score);
        var fromSignal = NiveauToPriorite(maxSignalNiveau);
        return PrioriteRank(fromRisk) >= PrioriteRank(fromSignal) ? fromRisk : fromSignal;
    }

    public static string StatutLabel(decimal moyenne) =>
        moyenne == 0m ? "Suivi"
        : moyenne < 10m ? "En difficulté"
        : moyenne < 12m ? "Suivi"
        : "Régulier";
}
