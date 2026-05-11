using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// Append-only event log row written by IAuditService whenever a
    /// state-changing controller action succeeds. Powers the dashboard's
    /// "Activité récente" feed and acts as the platform's audit trail.
    /// </summary>
    public class AuditEntry
    {
        public int Id { get; set; }

        public int? UtilisateurId { get; set; }

        [Required] [MaxLength(200)]
        public string UtilisateurNom { get; set; } = "Système";

        // PredictionBatch | NotePubliee | DwSync | RapportGenere | LoginOk
        // | AlerteResolue | etc. Strings keep the schema future-proof.
        [Required] [MaxLength(40)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(40)]
        public string EntityType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Utilisateur? Utilisateur { get; set; }
    }
}
