using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// A persisted, generated report. The actual file lives in the Contenu BLOB
    /// — keeping it in the same DB makes the platform self-contained for a PFA
    /// demo (no extra volume, no MinIO).
    /// </summary>
    public class Rapport
    {
        public int Id { get; set; }

        [Required] [MaxLength(40)]
        public string TemplateId { get; set; } = string.Empty; // perf-globale | risque | notes | absences | alertes | bilan-ml

        [Required] [MaxLength(200)]
        public string Titre { get; set; } = string.Empty;

        [Required] [MaxLength(10)]
        public string Format { get; set; } = "PDF"; // PDF | XLSX | CSV

        [MaxLength(20)]
        public string FiliereCode { get; set; } = "TOUS";

        [MaxLength(40)]
        public string Periode { get; set; } = string.Empty;

        public int? AuteurId { get; set; }

        [MaxLength(200)]
        public string AuteurNom { get; set; } = string.Empty;

        public int Taille { get; set; } // bytes

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        // Stored as VARBINARY(MAX). Excluded from JSON output — fetched via
        // /api/rapports/{id}/download instead.
        [JsonIgnore]
        [Column(TypeName = "varbinary(max)")]
        public byte[] Contenu { get; set; } = Array.Empty<byte>();

        [JsonIgnore]
        public Utilisateur? Auteur { get; set; }
    }
}
