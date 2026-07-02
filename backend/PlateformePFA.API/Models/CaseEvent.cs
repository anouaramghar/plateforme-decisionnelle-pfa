using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    // The single append-only event stream of a case. Everything that happens —
    // state changes, notes, emails, meetings — is one row here; "the timeline"
    // is a query over this table, not a separate table kept in sync by hand.
    // Type='Note' rows carry staff commentary (IsPrivate hides them from
    // teachers); every other Type is a system/action record.
    public class CaseEvent
    {
        public int Id { get; set; }
        public int CaseId { get; set; }

        [Required]
        [MaxLength(40)]
        public string Type { get; set; } = string.Empty; // Created | StateChanged | Reopened | SignalLinked | Note | EmailSent | ...

        [MaxLength(2000)]
        public string? Contenu { get; set; } // note text, or the event description

        // Only meaningful for Type='Note': hides the note from non-privileged roles.
        public bool IsPrivate { get; set; }

        public int? UtilisateurId { get; set; }
        [MaxLength(200)]
        public string UtilisateurNom { get; set; } = "Système";

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public InterventionCase Case { get; set; } = null!;
    }
}
