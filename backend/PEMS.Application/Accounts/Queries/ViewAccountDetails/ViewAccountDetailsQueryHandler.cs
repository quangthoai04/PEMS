using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.Accounts.Queries.ViewAccountDetails;

/// <summary>
/// UC-98 handler. Loads a single account (read-only, no sensitive columns) and enforces
/// the caller's visibility scope before returning it. The endpoint is already RBAC-gated
/// on UC-98.VIEW_ACCOUNT_DETAILS; this adds the row-level scope check.
/// </summary>
public sealed class ViewAccountDetailsQueryHandler : IRequestHandler<ViewAccountDetailsQuery, ViewAccountDetailsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewAccountDetailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ViewAccountDetailsDto> Handle(ViewAccountDetailsQuery request, CancellationToken cancellationToken)
    {
        var row = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == request.UserId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Phone,
                u.Gender,
                RoleCode = u.Role.RoleCode,
                RoleName = u.Role.Name,
                u.SubRole,
                u.PrimaryCampusId,
                CampusName = u.PrimaryCampus != null ? u.PrimaryCampus.Name : null,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                u.StudentCode,
                u.Status,
                u.CreatedVia,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        EnforceScope(row.RoleCode, row.SubRole, row.PrimaryCampusId);

        var isStaffLeader = row.RoleCode == RoleCodes.Staff && row.SubRole == UserSubRoles.Leader;

        // ── HO basic-info edit permission (HO_BASIC_INFO spec §11). Only an HO caller may edit
        //    another HO / Staff Leader's full name + email (never self, never a LOCKED account). ──
        string? editBasicInfoDisabledReason = null;
        var canEditBasicInfo = false;
        if (_currentUser.RoleCode == RoleCodes.Ho)
        {
            var isSelf = _currentUser.UserId.HasValue && row.UserId == _currentUser.UserId.Value;
            var targetInScope = row.RoleCode == RoleCodes.Ho || isStaffLeader;
            if (isSelf)
                editBasicInfoDisabledReason = "SELF_ACCOUNT";
            else if (!targetInScope)
                editBasicInfoDisabledReason = "TARGET_ROLE_NOT_MANAGEABLE";
            else if (row.Status == UserStatuses.Locked)
                editBasicInfoDisabledReason = "ACCOUNT_LOCKED";
            canEditBasicInfo = editBasicInfoDisabledReason is null;
        }

        return new ViewAccountDetailsDto
        {
            UserId = row.UserId,
            FullName = row.FullName,
            Email = row.Email,
            Phone = row.Phone,
            Gender = row.Gender,
            RoleCode = row.RoleCode,
            RoleName = row.RoleName,
            SubRole = row.SubRole,
            DisplayRole = isStaffLeader ? "Staff" : row.RoleName,
            DisplayPosition = isStaffLeader ? "Trưởng phòng" : null,
            CampusId = row.PrimaryCampusId,
            CampusName = row.CampusName,
            DepartmentId = row.DepartmentId,
            DepartmentName = row.DepartmentName,
            StudentCode = row.StudentCode,
            Status = row.Status,
            CreatedVia = row.CreatedVia,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            LastLoginAt = row.LastLoginAt,
            CanEditBasicInfo = canEditBasicInfo,
            EditBasicInfoDisabledReason = editBasicInfoDisabledReason,
        };
    }

    /// <summary>
    /// ADMIN may view any account. HO may view only HO and Staff-Leader accounts.
    /// Any other (campus-scoped) caller is limited to their own campus.
    /// A forbidden target is reported as Not Found so existence is not leaked.
    /// </summary>
    private void EnforceScope(string targetRoleCode, string? targetSubRole, ulong? targetCampusId)
    {
        var callerRole = _currentUser.RoleCode;

        if (callerRole == RoleCodes.Admin)
            return;

        if (callerRole == RoleCodes.Ho)
        {
            var inScope = targetRoleCode == RoleCodes.Ho
                || (targetRoleCode == RoleCodes.Staff && targetSubRole == UserSubRoles.Leader);
            if (!inScope)
                throw new NotFoundException("Account", _currentUser.UserId ?? 0);
            return;
        }

        // Campus-scoped caller (e.g. Staff Leader).
        if (_currentUser.PrimaryCampusId is null || targetCampusId != _currentUser.PrimaryCampusId)
            throw new NotFoundException("Account", _currentUser.UserId ?? 0);
    }
}
