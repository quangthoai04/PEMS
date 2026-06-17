using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Loads and evaluates role-based permissions (RBAC). Permission data lives in
/// the <c>role_permissions</c> table and is keyed by role.
/// </summary>
public interface IPermissionChecker
{
    /// <summary>Returns all permissions granted to the given role.</summary>
    Task<IReadOnlyList<UserPermissionDto>> GetPermissionsForRoleAsync(string roleId, string subRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the role has <paramref name="permissionCode"/> at a level
    /// greater than or equal to <paramref name="minimumLevel"/> (F &gt; E &gt; O &gt; R).
    /// </summary>
    Task<bool> HasPermissionAsync(string roleId, string subRole, string permissionCode, string minimumLevel, CancellationToken cancellationToken = default);

    /// <summary>Returns the granted level for a permission, or null when not granted.</summary>
    Task<string?> GetPermissionLevelAsync(string roleId, string subRole, string permissionCode, CancellationToken cancellationToken = default);
}
