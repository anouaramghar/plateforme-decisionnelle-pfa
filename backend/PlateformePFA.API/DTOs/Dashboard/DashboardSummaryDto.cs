namespace PlateformePFA.API.DTOs.Dashboard
{
    public class DashboardSummaryDto
    {
        public KpisDto Kpis { get; set; } = new();
        public List<NotesByFiliereDto> NotesByFiliere { get; set; } = new();
        public List<DistributionBucketDto> MoyenneDistribution { get; set; } = new();
        public List<RiskBucketDto> RiskBreakdown { get; set; } = new();
        public List<AbsenceTrendDto> AbsenceTrend { get; set; } = new();
        public List<TopRisqueDto> TopARisque { get; set; } = new();

        // Hero card sparkline + side stats — last 14 weeks of total student
        // count (oldest first), plus this-week deltas.
        public List<int> EtudiantsParSemaine { get; set; } = new();
        public int NouveauxCetteSemaine { get; set; }
        public int RetraitsCetteSemaine { get; set; }
    }

    public class KpisDto
    {
        public int NbEtudiants { get; set; }
        public decimal MoyGlobale { get; set; }
        public decimal TauxReussite { get; set; }
        public int AlertesActives { get; set; }

        // Comparisons vs the previous comparable period (semester / 7-day window).
        // NbEtudiantsDelta is a percentage growth; the rest are absolute deltas.
        public decimal NbEtudiantsDelta { get; set; }
        public decimal MoyGlobaleDelta { get; set; }
        public decimal TauxReussiteDelta { get; set; }
        public int AlertesActivesDelta { get; set; }
        public string CompareLabel { get; set; } = "vs période précédente";
    }

    public class NotesByFiliereDto
    {
        public string Filiere { get; set; } = string.Empty;
        public decimal Moyenne { get; set; }
        public string Color { get; set; } = "#94a3b8";
    }

    public class DistributionBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public int N { get; set; }
    }

    public class RiskBucketDto
    {
        public string Label { get; set; } = string.Empty;
        public int N { get; set; }
        public string Color { get; set; } = "#94a3b8";
    }

    public class AbsenceTrendDto
    {
        public string Semaine { get; set; } = string.Empty;
        public decimal Heures { get; set; }
    }

    /// <summary>One row of the dashboard's "Activité récente" feed,
    /// sourced from AuditEntries.</summary>
    public class ActivityDto
    {
        public int    Id             { get; set; }
        public string Action         { get; set; } = string.Empty;
        public string UtilisateurNom { get; set; } = "Système";
        public string Message        { get; set; } = string.Empty;
        public DateTime CreeLe       { get; set; }
    }

    public class TopRisqueDto
    {
        public int Id { get; set; }
        public string NomComplet { get; set; } = string.Empty;
        public string Matricule { get; set; } = string.Empty;
        public string Filiere { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty;
        public decimal Moyenne { get; set; }
        public int Absences { get; set; }
        public decimal ScoreRisque { get; set; }
    }
}
