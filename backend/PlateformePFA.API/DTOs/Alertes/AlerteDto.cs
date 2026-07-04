using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Alertes
{
    public class CreateAlerteDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "EtudiantId must be a positive integer.")]
        public int EtudiantId { get; set; }

        /// <summary>RisqueEchec | AbsenceExcessive | NoteFaible | Abandon</summary>
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        /// <summary>Faible | Moyen | Eleve | Critique</summary>
        [Required]
        [MaxLength(20)]
        public string Niveau { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Student summary embedded in alert list rows. The Etudiant nav property
    /// on the entity is [JsonIgnore]'d (cycle guard), so the list endpoint
    /// projects this DTO instead of silently returning null students.
    /// </summary>
    public class AlerteEtudiantDto
    {
        public int Id { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string NomComplet { get; set; } = string.Empty;
        /// <summary>Filière code (e.g. GI), not the nav object.</summary>
        public string Filiere { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty;
    }

    public class AlerteListDto
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }
        public int? ModuleId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Niveau { get; set; } = string.Empty;
        public string? Message { get; set; }
        public bool Resolue { get; set; }
        public DateTime CreeLe { get; set; }
        public AlerteEtudiantDto? Etudiant { get; set; }
    }

    /// <summary>
    /// Global alert counts — the list endpoint is paginated (capped at 100),
    /// so UI stat cards must not derive totals from the fetched window.
    /// </summary>
    public class AlerteStatsDto
    {
        public int Active { get; set; }
        public int Resolue { get; set; }
        public int Total { get; set; }
        /// <summary>Unresolved count per alert type (RisqueEchec, NoteFaible, …).</summary>
        public Dictionary<string, int> ParType { get; set; } = new();
    }
}
