using System.Security.Claims;

namespace PlateformePFA.API.Services
{
    public interface IAcademicAccessService
    {
        Task<int?> GetAssignedModuleIdAsync(ClaimsPrincipal user, CancellationToken ct = default);
        Task<bool> CanAccessModuleAsync(ClaimsPrincipal user, int moduleId, CancellationToken ct = default);
    }
}
