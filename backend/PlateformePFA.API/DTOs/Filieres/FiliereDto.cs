using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Filieres
{
    public class CreateFiliereDto
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Intitule { get; set; } = string.Empty;

        public int? ResponsableId { get; set; }
    }

    public class UpdateFiliereDto
    {
        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Intitule { get; set; } = string.Empty;

        public int? ResponsableId { get; set; }
    }
}
