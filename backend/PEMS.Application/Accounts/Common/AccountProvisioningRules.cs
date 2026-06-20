using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Common;

/// <summary>
/// Shared, DB-trigger-mirroring validation for account role/campus/department shape.
/// The MySQL <c>trg_users_validate_bi/bu</c> triggers enforce these rules at the data
/// layer; we replicate them here so the API returns friendly errors instead of raw
/// trigger failures, and so Staff-Leader campus scoping is applied consistently.
/// </summary>
internal static class AccountProvisioningRules
{
    /// <summary>Normalized, validated account shape ready to assign to a <c>User</c>.</summary>
    public sealed record ResolvedShape(
        ulong RoleId,
        string RoleCode,
        string? SubRole,
        ulong? DepartmentId,
        ulong? PrimaryCampusId);

    /// <summary>ADMIN / HO may operate across campuses; everyone else is scoped to their own campus.</summary>
    public static bool IsPrivileged(string? roleCode)
        => roleCode == RoleCodes.Admin || roleCode == RoleCodes.Ho;

    public static async Task<ResolvedShape> ResolveAsync(
        IApplicationDbContext db,
        string roleCode,
        string? subRole,
        ulong? primaryCampusId,
        ulong? departmentId,
        bool actorIsPrivileged,
        ulong? actorCampusId,
        CancellationToken cancellationToken)
    {
        roleCode = (roleCode ?? string.Empty).Trim().ToUpperInvariant();

        var role = await db.Roles.FirstOrDefaultAsync(
            r => r.RoleCode == roleCode && r.Status == EntityStatuses.Active && r.DeletedAt == null,
            cancellationToken)
            ?? throw new ValidationException($"Role '{roleCode}' is not valid or not active.");

        // Staff-Leader (non-privileged) scoping: cannot assign accounts to another campus.
        if (!actorIsPrivileged && roleCode != RoleCodes.Visitor)
        {
            if (actorCampusId is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage internal accounts.");

            if (primaryCampusId is not null && primaryCampusId != actorCampusId)
                throw new ForbiddenException("You can only manage accounts within your own campus.");

            primaryCampusId = actorCampusId;
        }

        switch (roleCode)
        {
            case RoleCodes.Visitor:
                // VISITOR: no sub-role, department, or campus.
                return new ResolvedShape(role.RoleId, roleCode, null, null, null);

            case RoleCodes.Staff:
            case RoleCodes.Department:
            {
                if (subRole != SubRoles.Leader && subRole != SubRoles.Staff)
                    throw new ValidationException("Sub-role (Leader or Staff) is required for STAFF/DEPARTMENT roles.");
                if (departmentId is null)
                    throw new ValidationException("Department is required for STAFF/DEPARTMENT roles.");

                var dept = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken)
                    ?? throw new ValidationException("Selected department was not found.");
                if (dept.Status != EntityStatuses.Active)
                    throw new ValidationException("Selected department is not active.");

                var expectedType = roleCode == RoleCodes.Staff ? "IC" : "GENERAL";
                if (!string.Equals(dept.DepartmentType, expectedType, StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException(roleCode == RoleCodes.Staff
                        ? "STAFF must belong to an IC department."
                        : "DEPARTMENT must belong to a GENERAL department.");

                if (primaryCampusId is not null && primaryCampusId != dept.CampusId)
                    throw new ValidationException("Selected campus does not match the department's campus.");

                if (!actorIsPrivileged && dept.CampusId != actorCampusId)
                    throw new ForbiddenException("You can only manage departments within your own campus.");

                // Campus is derived from the department (matches the DB trigger behaviour).
                return new ResolvedShape(role.RoleId, roleCode, subRole, dept.DepartmentId, dept.CampusId);
            }

            default: // ADMIN, HO, STUDENT — require a campus, no sub-role / department.
            {
                if (!string.IsNullOrWhiteSpace(subRole))
                    throw new ValidationException($"{roleCode} must not have a sub-role.");
                if (departmentId is not null)
                    throw new ValidationException($"{roleCode} must not have a department.");
                if (primaryCampusId is null)
                    throw new ValidationException($"{roleCode} requires a campus.");

                var campus = await db.Campuses.FirstOrDefaultAsync(c => c.CampusId == primaryCampusId, cancellationToken)
                    ?? throw new ValidationException("Selected campus was not found.");
                if (campus.Status != EntityStatuses.Active)
                    throw new ValidationException("Selected campus is not active.");

                return new ResolvedShape(role.RoleId, roleCode, null, null, primaryCampusId);
            }
        }
    }
}
