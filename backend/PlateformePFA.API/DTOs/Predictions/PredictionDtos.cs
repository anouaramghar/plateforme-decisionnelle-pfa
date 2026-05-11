namespace PlateformePFA.API.DTOs.Predictions
{
    public class PredictionsSummaryDto
    {
        public PredictionKpisDto Kpis { get; set; } = new();
        public List<ScatterPointDto> Scatter { get; set; } = new();
        public List<TopRisqueRowDto> TopARisque { get; set; } = new();
        public List<RunDto> Runs { get; set; } = new();
    }

    public class PredictionKpisDto
    {
        public int Evalues { get; set; }
        public int RisqueEleve { get; set; }
        public int RisqueModere { get; set; }
        public decimal Auc { get; set; }
    }

    public class ScatterPointDto
    {
        public decimal X { get; set; }   // absences
        public decimal Y { get; set; }   // moyenne
        public string  R { get; set; } = "faible";
    }

    public class TopRisqueRowDto
    {
        public int    Id          { get; set; }
        public string NomComplet  { get; set; } = string.Empty;
        public string Matricule   { get; set; } = string.Empty;
        public string Filiere     { get; set; } = string.Empty;
        public string Niveau      { get; set; } = string.Empty;
        public decimal Moyenne    { get; set; }
        public int    Absences    { get; set; }
        public decimal ScoreRisque { get; set; }
    }

    public class RunDto
    {
        public string Id          { get; set; } = string.Empty;
        public string Date        { get; set; } = string.Empty;   // "25 avr. 2026, 14:32"
        public string Filiere     { get; set; } = "TOUS";
        public int    Etudiants   { get; set; }
        public int    RisqueEleve { get; set; }
        public string Modele      { get; set; } = "XGBoost v1.4";
        public decimal Auc        { get; set; }
        public string Statut      { get; set; } = "OK";
    }

    public class BatchRequestDto
    {
        public string? FiliereCode { get; set; }
        public string? Niveau      { get; set; }
    }

    public class BatchResultDto
    {
        public int Total      { get; set; }
        public int Predicted  { get; set; }
        public int Failed     { get; set; }
        public int Skipped    { get; set; }
        public int RisqueEleve { get; set; }
    }
}
