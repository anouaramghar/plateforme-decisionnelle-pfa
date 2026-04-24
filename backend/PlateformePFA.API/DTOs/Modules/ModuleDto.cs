using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Modules
{
    public class CreateModuleDto
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty;

        [Range(0.5, 10, ErrorMessage = "Coefficient must be between 0.5 and 10.")]
        public decimal Coefficient { get; set; } = 1;

        /// <summary>S1 | S2</summary>
        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty;
    }

    public class UpdateModuleDto
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty;

        [Range(0.5, 10, ErrorMessage = "Coefficient must be between 0.5 and 10.")]
        public decimal Coefficient { get; set; } = 1;

        /// <summary>S1 | S2</summary>
        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty;
    }
}
