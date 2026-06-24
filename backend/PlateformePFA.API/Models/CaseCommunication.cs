using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    // Lifecycle of an outbound case email. Draft is outreach-only (editable
    // before send); legacy generic communications start at Queued.
    public static class CommunicationStatus
    {
        public const string Draft = "Draft";
        public const string Queued = "Queued";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
    }

    // An outbound email tied to a case. The rendered subject/body are stored as
    // sent, so later template edits never rewrite history. Retry re-sends the
    // stored text — it does not re-render.
    public class CaseCommunication
    {
        public int Id { get; set; }
        public int CaseId { get; set; }

        [Required, MaxLength(200)]
        public string Destinataire { get; set; } = string.Empty;

        [MaxLength(60)]
        public string TemplateId { get; set; } = string.Empty;

        [Required, MaxLength(300)]
        public string Sujet { get; set; } = string.Empty;
        [Required, MaxLength(4000)]
        public string Corps { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Queued"; // Queued | Sent | Failed

        [MaxLength(500)]
        public string? Erreur { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public DateTime? EnvoyeLe { get; set; }
        public int? CreeParId { get; set; }

        [JsonIgnore] public InterventionCase Case { get; set; } = null!;
    }
}
