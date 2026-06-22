using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Interventions
{
    public class CreateCaseDto
    {
        [Required] public int EtudiantId { get; set; }

        [Required, MaxLength(200)]
        public string Motif { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Priorite { get; set; } = "Medium";

        public int? OwnerId { get; set; }
        public DateTime? DueDate { get; set; }

        // Optional: open the case straight from an alert (the "signal").
        public int? AlerteId { get; set; }
    }

    public class TransitionCaseDto
    {
        [Required, MaxLength(20)]
        public string Etat { get; set; } = string.Empty; // target state

        // Set when resolving.
        [MaxLength(40)] public string? Outcome { get; set; }
        [MaxLength(1000)] public string? ResolutionSummary { get; set; }
        public DateTime? FollowUpDate { get; set; }

        // Required when reopening; recorded on the timeline otherwise.
        [MaxLength(500)] public string? Raison { get; set; }

        public int? OwnerId { get; set; } // optional assignment alongside the transition
    }
}
