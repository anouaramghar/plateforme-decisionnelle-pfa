using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Alerte
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }

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

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
    }
}