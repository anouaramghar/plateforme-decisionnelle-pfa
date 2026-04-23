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
        [MinLength(6)]
        public string MotDePasse { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;
    }
}
