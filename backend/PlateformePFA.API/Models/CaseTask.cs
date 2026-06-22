using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    // An actionable item on a case: assignee + deadline + completion evidence.
    public class CaseTask
    {
        public int Id { get; set; }
        public int CaseId { get; set; }

        [Required]
        [MaxLength(300)]
        public string Titre { get; set; } = string.Empty;

        public int? AssigneeId { get; set; } // Utilisateur responsible for the task
        public DateTime? DueDate { get; set; }

        public bool Done { get; set; }
        public DateTime? DoneLe { get; set; }

        [MaxLength(500)]
        public string? CompletionEvidence { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public int? CreeParId { get; set; }

        [JsonIgnore] public InterventionCase Case { get; set; } = null!;
    }
}
