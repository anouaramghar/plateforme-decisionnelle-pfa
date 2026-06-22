using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;

namespace PlateformePFA.API.Services
{
    public class AcademicAccessService : IAcademicAccessService
    {
        private readonly AppDbContext _db;

        public AcademicAccessService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int?> GetAssignedModuleIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (user == null) return null;

            // Admin and Responsable are not restricted to a specific module
            if (user.IsInRole("Admin") || user.IsInRole("Responsable"))
            {
                return null;
            }

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            var dbUser = await _db.Utilisateurs
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            return dbUser?.ModuleId;
        }

        public async Task<bool> CanAccessModuleAsync(ClaimsPrincipal user, int moduleId, CancellationToken ct = default)
        {
            if (user == null) return false;

            // Admin and Responsable have access to all modules
            if (user.IsInRole("Admin") || user.IsInRole("Responsable"))
            {
                return true;
            }

            var assignedModuleId = await GetAssignedModuleIdAsync(user, ct);
            return assignedModuleId.HasValue && assignedModuleId.Value == moduleId;
        }
    }
}
