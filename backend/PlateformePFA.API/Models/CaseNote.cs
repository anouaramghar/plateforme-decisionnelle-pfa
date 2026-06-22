using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    // An internal note on a case. IsPrivate notes are Admin/Responsable-only;
    // enforcement matters once teachers get contribution access (later slice) —
    // the column exists now so the privacy model is in place from the start.
    public class CaseNote
    {
        public int Id { get; set; }
        public int CaseId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Contenu { get; set; } = string.Empty;

        public bool IsPrivate { get; set; }

        public int? AuteurId { get; set; }
        [MaxLength(200)]
        public string AuteurNom { get; set; } = "Système";

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public InterventionCase Case { get; set; } = null!;
    }
}
