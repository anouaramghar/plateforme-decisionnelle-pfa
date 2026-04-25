using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    [Table("Predictions")] // SQL table = "Predictions", EF would default to "PredictionMLs"
    public class PredictionML
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }

        [Required]
        [MaxLength(50)]
        public string TypeModele { get; set; } = string.Empty; // RisqueEchec | Regression | Clustering

        [Column(TypeName = "decimal(5,4)")]
        public decimal? ScoreRisque { get; set; }  // probabilité 0.0–1.0

        [MaxLength(20)]
        public string? Niveau { get; set; } // Faible | Moyen | Eleve | Critique

        public int? Cluster { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NotePredite { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal? Confiance { get; set; }

        [Required]
        [MaxLength(9)]
        public string Annee { get; set; } = string.Empty;

        // Provenance: distinguishes a real "low score" prediction from a stub
        // written when the ML service was unreachable or input was insufficient.
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Ok"; // Ok | MlUnavailable | InsufficientData

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
    }
}