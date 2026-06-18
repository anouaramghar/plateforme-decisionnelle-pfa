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

    public static decimal HeuristicScore(decimal moyenne, int absencesH)
    {
        var fromMoy = moyenne == 0m ? 0.4m
            : moyenne < 10m ? 0.55m
            : moyenne < 12m ? 0.32m
            : 0.12m;
        var fromAbs = absencesH > 30 ? 0.22m
            : absencesH > 18 ? 0.12m
            : 0m;
        return Math.Min(0.98m, fromMoy + fromAbs);
    }

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
            .Where(p => p.ScoreRisque.HasValue)
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

    public static string StatutLabel(decimal moyenne) =>
        moyenne == 0m ? "Suivi"
        : moyenne < 10m ? "En difficulté"
        : moyenne < 12m ? "Suivi"
        : "Régulier";
}
