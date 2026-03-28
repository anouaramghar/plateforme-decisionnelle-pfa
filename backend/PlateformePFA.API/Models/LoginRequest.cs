namespace PlateformePFA.API.Models
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string MotDePasse { get; set; } = string.Empty;
    }
}