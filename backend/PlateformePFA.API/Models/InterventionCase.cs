using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Models
{
    // An owned, auditable intervention on a student in difficulty.
    // Signals (Alertes/Predictions) are evidence; a case is the managed action.
    public class InterventionCase
    {
        public int Id { get; set; }

        public int EtudiantId { get; set; }

        // Denormalised from the student so the Responsable triage queue and
        // filière dashboards filter without joining through Etudiant every time.
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Motif { get; set; } = string.Empty; // opening reason

        [Required]
        [MaxLength(20)]
        public string Priorite { get; set; } = "Medium"; // Critical | High | Medium | Low

        [Required]
        [MaxLength(20)]
        public string Etat { get; set; } = CaseWorkflowState.Open; // see CaseWorkflow

        // One accountable owner. Required to leave Open.
        public int? OwnerId { get; set; }

        public DateTime? DueDate { get; set; }

        // Escalation is derived, not stored: a Critical case past its due date
        // IS escalated. No background worker, no extra state to keep in sync.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool EnRetard =>
            DueDate.HasValue && DueDate.Value < DateTime.UtcNow
            && Etat != CaseWorkflowState.Resolved && Etat != CaseWorkflowState.Closed;

        // Set on Resolved; both required by the workflow.
        [MaxLength(40)]
        public string? Outcome { get; set; } // Improved | Stable | Deteriorated | ...
        [MaxLength(1000)]
        public string? ResolutionSummary { get; set; }
        public DateTime? FollowUpDate { get; set; }

        // Single scheduled outreach meeting + its recorded outcome. One case
        // drives one meeting at a time; rescheduling overwrites these fields.
        public DateTime? MeetingScheduledFor { get; set; }
        [MaxLength(200)] public string? MeetingLocation { get; set; }
        [MaxLength(20)] public string? MeetingAttendance { get; set; } // Held | Absent | Cancelled
        public DateTime? MeetingHeldAt { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public int? CreeParId { get; set; }
        public DateTime? ClotureLe { get; set; }

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
        [JsonIgnore] public Utilisateur? Owner { get; set; }
        [JsonIgnore] public ICollection<CaseEvent>? Events { get; set; }
    }
}
