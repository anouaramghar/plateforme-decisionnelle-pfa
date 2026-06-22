using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    // Append-only audit trail for a case. Rows are never updated or deleted —
    // every meaningful change (creation, state change, assignment) writes one.
    public class CaseTimelineEvent
    {
        public int Id { get; set; }
        public int CaseId { get; set; }

        [Required]
        [MaxLength(40)]
        public string Action { get; set; } = string.Empty; // Created | StateChanged | Assigned | Reopened | SignalLinked

        [MaxLength(500)]
        public string? Description { get; set; }

        public int? UtilisateurId { get; set; }
        [MaxLength(200)]
        public string UtilisateurNom { get; set; } = "Système";

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public InterventionCase Case { get; set; } = null!;
    }
}
