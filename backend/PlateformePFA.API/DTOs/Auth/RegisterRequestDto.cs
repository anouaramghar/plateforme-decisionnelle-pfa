using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Auth
{
    public class RegisterRequestDto
    {
        [Required]
        public string Nom { get; set; } = string.Empty;

        [Required]
        public string Prenom { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(12, ErrorMessage = "Le mot de passe doit contenir au moins 12 caractères.")]
        public string MotDePasse { get; set; } = string.Empty;

        [Required]
        [AllowedValues("Admin", "Enseignant", "Responsable",
            ErrorMessage = "Rôle invalide. Valeurs acceptées : Admin, Enseignant, Responsable.")]
        public string Role { get; set; } = string.Empty;

        // Only relevant when Role == "Enseignant"
        public int? ModuleId { get; set; }
    }
}
