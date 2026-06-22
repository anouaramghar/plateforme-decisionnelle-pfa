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

        public async Task<ModuleScope> GetModuleScopeAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            // No principal at all => restricted to nothing (deny all reads).
            if (user == null) return new ModuleScope(false, null);

            if (user.IsInRole("Admin") || user.IsInRole("Responsable"))
            {
                return ModuleScope.All;
            }

            // Any non-privileged role (Enseignant) is restricted to its single
            // assignment. A null assignment means "see nothing", never "see all".
            var assignedModuleId = await GetAssignedModuleIdAsync(user, ct);
            return new ModuleScope(false, assignedModuleId);
        }

        public async Task<CohortScope> GetCohortScopeAsync(ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (user == null) return new CohortScope(false, null, null);

            if (user.IsInRole("Admin") || user.IsInRole("Responsable"))
            {
                return CohortScope.All;
            }

            // Enseignant: derive the cohort (filière + niveau) from the assigned
            // module. No module / unknown module ⇒ no cohort ⇒ sees nothing.
            var assignedModuleId = await GetAssignedModuleIdAsync(user, ct);
            if (!assignedModuleId.HasValue) return new CohortScope(false, null, null);

            var module = await _db.Modules
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == assignedModuleId.Value, ct);
            if (module == null) return new CohortScope(false, null, null);

            return new CohortScope(false, module.FiliereId, module.Niveau);
        }
    }
}
