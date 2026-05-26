using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// A pending alert prepared by the Copilot via draft_alert. Never auto-sent;
    /// promoted to a real Alerte only when the user clicks Confirmer in the chat
    /// panel (POST /api/copilot/confirm). Expires 5 min after creation.
    /// </summary>
    public class AlertDraft
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        // 'low' | 'medium' | 'high'
        [Required] [MaxLength(10)]
        public string Severity { get; set; } = "medium";

        [Required] [MaxLength(1000)]
        public string MessageFr { get; set; } = string.Empty;

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);

        // 'pending_user_confirm' | 'sent' | 'expired' | 'cancelled'
        [Required] [MaxLength(30)]
        public string Status { get; set; } = "pending_user_confirm";

        public DateTime? SentAt { get; set; }

        [JsonIgnore] [ForeignKey(nameof(StudentId))]
        public Etudiant? Student { get; set; }

        [JsonIgnore] [ForeignKey(nameof(CreatedBy))]
        public Utilisateur? Creator { get; set; }
    }
}
