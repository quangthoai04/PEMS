using Microsoft.EntityFrameworkCore;
using PEMS.Application.Authentication.Models;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Evaluates RBAC permissions from the <c>role_permissions</c> table.
/// </summary>
public sealed class PermissionChecker : IPermissionChecker
{
    private readonly IApplicationDbContext _db;

    public PermissionChecker(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserPermissionDto>> GetPermissionsForRoleAsync(
        string roleId, string subRole, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(subRole))
            return Array.Empty<UserPermissionDto>();

        return await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId && rp.SubRole == subRole)
            .Join(_db.Permissions,
                rp => rp.PermissionId,
                p => p.PermissionId,
                (rp, p) => new UserPermissionDto
                {
                    PermissionCode = p.PermissionCode,
                    PermissionLevel = rp.PermissionLevel,
                    PermissionGroup = p.PermissionGroup
                })
            .OrderBy(p => p.PermissionGroup)
            .ThenBy(p => p.PermissionCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetPermissionLevelAsync(
        string roleId, string subRole, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(roleId) || string.IsNullOrEmpty(subRole) || string.IsNullOrEmpty(permissionCode))
            return null;

        return await _db.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId && rp.SubRole == subRole)
            .Join(_db.Permissions.Where(p => p.PermissionCode == permissionCode),
                rp => rp.PermissionId,
                p => p.PermissionId,
                (rp, p) => rp.PermissionLevel)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasPermissionAsync(
        string roleId, string subRole, string permissionCode, string minimumLevel, CancellationToken cancellationToken = default)
    {
        var level = await GetPermissionLevelAsync(roleId, subRole, permissionCode, cancellationToken);
        return level is not null && PermissionLevels.Satisfies(level, minimumLevel);
    }
}
