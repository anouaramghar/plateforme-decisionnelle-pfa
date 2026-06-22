using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Alerte
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }

        // Nullable: AbsenceExcessive aggregates every module, NoteFaible is per-module.
        public int? ModuleId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty; // RisqueEchec | AbsenceExcessive | NoteFaible | Abandon

        [Required]
        [MaxLength(20)]
        public string Niveau { get; set; } = string.Empty; // Faible | Moyen | Eleve | Critique

        [MaxLength(500)]
        public string? Message { get; set; }

        public bool Resolue { get; set; }
        public DateTime CreeLe { get; set; } = DateTime.UtcNow;
        public DateTime? ResolueeLe { get; set; }

        // Audit: which Utilisateur clicked "Résoudre".
        public int? ResolueeParId { get; set; }

        // The signal/triage link: set when this alert is converted to or linked
        // with an intervention case. Null = still an un-triaged evidence signal.
        public int? CaseId { get; set; }

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
        [JsonIgnore] public Module? Module { get; set; }
        [JsonIgnore] public Utilisateur? ResolueePar { get; set; }
    }
}