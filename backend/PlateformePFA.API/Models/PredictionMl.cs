using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    [Table("Predictions")] // SQL table = "Predictions", EF would default to "PredictionMLs"
    public class PredictionML
    {
        public int      Id          { get; set; }
        public int      EtudiantId  { get; set; }
        public string   TypeModele  { get; set; } = string.Empty; // RisqueEchec | Regression | Clustering
        public decimal? ScoreRisque { get; set; }  // probabilité 0.0–1.0
        public int?     Cluster     { get; set; }
        public decimal? NotePredite { get; set; }
        public decimal? Confiance   { get; set; }
        public string   Annee       { get; set; } = string.Empty;
        public DateTime CreeLe      { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
    }
}