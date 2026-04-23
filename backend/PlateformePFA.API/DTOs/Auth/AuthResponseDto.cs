namespace PlateformePFA.API.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Token        { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public string Role         { get; set; } = string.Empty;
        public string Email        { get; set; } = string.Empty;
        public string NomComplet   { get; set; } = string.Empty;
    }
}
