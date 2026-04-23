namespace PlateformePFA.API.DTOs.Auth
{
    public class RegisterRequestDto
    {
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MotDePasse { get; set; } = string.Empty; // In reality this is the plain password from frontend
        public string Role { get; set; } = string.Empty;
    }
}
