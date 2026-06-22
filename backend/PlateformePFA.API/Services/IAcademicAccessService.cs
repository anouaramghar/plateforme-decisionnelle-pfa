using System.Security.Claims;

namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Resolved read scope for academic records. Distinguishes an unrestricted
    /// role (Admin/Responsable) from a teacher restricted to a single module —
    /// a distinction <see cref="IAcademicAccessService.GetAssignedModuleIdAsync"/>
    /// alone loses, because it returns null for BOTH an unrestricted role and a
    /// teacher with no assignment.
    /// </summary>
    public readonly record struct ModuleScope(bool Unrestricted, int? ModuleId)
    {
        public static ModuleScope All => new(true, null);

        // EF filter value: a teacher with no assignment (ModuleId null) maps to
        // -1, which matches no real module id, so they see nothing.
        public int FilterModuleId => ModuleId ?? -1;
    }

    public interface IAcademicAccessService
    {
        Task<int?> GetAssignedModuleIdAsync(ClaimsPrincipal user, CancellationToken ct = default);
        Task<bool> CanAccessModuleAsync(ClaimsPrincipal user, int moduleId, CancellationToken ct = default);
        Task<ModuleScope> GetModuleScopeAsync(ClaimsPrincipal user, CancellationToken ct = default);
    }
}
