using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// One conversation between a user and the ENIAD Copilot. Messages live in
    /// AgentSessionMessage. Auto-archived after 24h of inactivity.
    /// </summary>
    public class AgentSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int UserId { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        public bool IsArchived { get; set; } = false;

        [JsonIgnore] public Utilisateur? User { get; set; }

        [JsonIgnore] public ICollection<AgentSessionMessage> Messages { get; set; }
            = new List<AgentSessionMessage>();
    }
}
