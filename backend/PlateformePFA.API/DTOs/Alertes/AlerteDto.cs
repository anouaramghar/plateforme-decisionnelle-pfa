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
}
