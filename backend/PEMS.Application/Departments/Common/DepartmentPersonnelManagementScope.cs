using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Departments;

namespace PEMS.Application.Departments.Common;

/// <summary>
/// SEC-05..09 remediation. The legacy <c>/api/Departments</c> personnel actions (search/view
/// personnel, update personnel, remove personnel, reassign lead) trusted a client-supplied
/// <c>departmentId</c> with no scope check at all — any authenticated user could read or mutate any
/// department's personnel. This is the single scope gate behind all five.
///
/// <para>
/// Actor set verified against <c>IRoleAccessPolicy.CanAccessDepartmentManagement</c>,
/// <c>DepartmentDetailDashboard.tsx</c>'s <c>canEditMember</c> and <c>dashboardRouteAccess.ts</c> —
/// not assumed from the frontend caller alone: HO (global), Staff Leader (own campus), Department
/// Lead (own department — and, mirroring the pattern
/// <c>DepartmentLeaderPersonnelScopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync</c>
/// already established for the canonical self-service flow, re-verified as the department's actual
/// current <c>HeadUserId</c>, never trusted from the JWT's <c>department_id</c> claim alone).
/// </para>
/// </summary>
internal static class DepartmentPersonnelManagementScope
{
    public static async Task<Department> EnsureDepartmentInScopeAsync(
        IApplicationDbContext db, ICurrentUserService currentUser, ulong departmentId, CancellationToken cancellationToken)
    {
        var department = await db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken)
            ?? throw new NotFoundException("Department", departmentId);

        string effectiveRole;
        try
        {
            effectiveRole = EffectiveRole.Resolve(currentUser.RoleCode ?? string.Empty, currentUser.SubRole);
        }
        catch (InvalidOperationException)
        {
            throw Forbidden();
        }

        switch (effectiveRole)
        {
            case EffectiveRole.Ho:
                return department;

            case EffectiveRole.StaffLeader:
                if (currentUser.PrimaryCampusId is null || department.CampusId != currentUser.PrimaryCampusId)
                    throw Forbidden();
                return department;

            case EffectiveRole.DepartmentLead:
                if (currentUser.UserId is not { } actorId
                    || department.DepartmentId != currentUser.DepartmentId
                    || department.HeadUserId != actorId)
                    throw Forbidden();
                return department;

            default:
                throw Forbidden();
        }
    }

    /// <summary>
    /// SEC-09: <c>reassigndepartmentlead</c> is deliberately narrower than the four read/write
    /// personnel actions above — Department Lead is excluded. Self-service leadership transfer has
    /// its own canonical, fully-correct flow at <c>/api/department-leader/transfer-leadership</c>
    /// (<c>TransferDepartmentLeadershipCommandHandler</c>); admitting Department Lead here too would
    /// give the same actor two different code paths to the same mutation, one of them this legacy
    /// screen's thinner logic.
    /// </summary>
    public static async Task<Department> EnsureDepartmentInScopeForReassignmentAsync(
        IApplicationDbContext db, ICurrentUserService currentUser, ulong departmentId, CancellationToken cancellationToken)
    {
        var department = await db.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == departmentId, cancellationToken)
            ?? throw new NotFoundException("Department", departmentId);

        string effectiveRole;
        try
        {
            effectiveRole = EffectiveRole.Resolve(currentUser.RoleCode ?? string.Empty, currentUser.SubRole);
        }
        catch (InvalidOperationException)
        {
            throw Forbidden();
        }

        if (effectiveRole == EffectiveRole.Ho)
            return department;

        if (effectiveRole == EffectiveRole.StaffLeader
            && currentUser.PrimaryCampusId is not null
            && department.CampusId == currentUser.PrimaryCampusId)
            return department;

        throw Forbidden();
    }

    private static AuthBusinessException Forbidden() => new(
        DepartmentErrorCodes.DepartmentManagementForbidden,
        "Bạn không có quyền quản lý phòng ban.", 403);
}
