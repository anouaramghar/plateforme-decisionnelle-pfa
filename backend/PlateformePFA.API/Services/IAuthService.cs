using PlateformePFA.API.DTOs.Auth;

namespace PlateformePFA.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);
        Task<AuthResponseDto?> RefreshAsync(string refreshToken);
    }
}
