using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Absences
{
    public class CreateAbsenceDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "EtudiantId must be a positive integer.")]
        public int EtudiantId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "ModuleId must be a positive integer.")]
        public int ModuleId { get; set; }

        [Range(1, 100, ErrorMessage = "NombreHeures must be between 1 and 100.")]
        public int NombreHeures { get; set; } = 1;

        public bool Justifiee { get; set; }

        [Required]
        public DateTime DateAbsence { get; set; }
    }

    public class UpdateAbsenceDto
    {
        [Range(1, 100, ErrorMessage = "NombreHeures must be between 1 and 100.")]
        public int NombreHeures { get; set; } = 1;

        public bool Justifiee { get; set; }

        [Required]
        public DateTime DateAbsence { get; set; }

        public string RowVersion { get; set; } = string.Empty;
    }
}
