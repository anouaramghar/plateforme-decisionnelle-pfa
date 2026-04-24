using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Etudiants
{
    public class EtudiantDto
    {
        public int    Id        { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom       { get; set; } = string.Empty;
        public string Prenom    { get; set; } = string.Empty;
        public string? Email    { get; set; }
        public int    FiliereId { get; set; }
        public string Filiere   { get; set; } = string.Empty;
        public string Niveau    { get; set; } = string.Empty;
        public string Annee     { get; set; } = string.Empty;
    }

    public class CreateEtudiantDto
    {
        [Required]
        [MaxLength(20)]
        public string Matricule { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty; // L1 L2 L3 M1 M2

        [Required]
        [MaxLength(20)]
        public string Annee { get; set; } = string.Empty; // ex: 2025/2026
    }

    public class UpdateEtudiantDto
    {
        [Required]
        [MaxLength(20)]
        public string Matricule { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Annee { get; set; } = string.Empty;
    }
}
