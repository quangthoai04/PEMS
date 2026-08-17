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
                u.Nationality,
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
                // Security context for the ADMIN read-only review (spec §8.4). Provider TYPES only —
                // the subject ids, tokens and every other secret on UserAuthProvider stay in the DB.
                Providers = u.AuthProviders.Select(p => p.ProviderType).ToList(),
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

        // ── Pending-account actions (resend / correct the email). Both are the same permission, and
        //    both are computed from the DATABASE row rather than left to the client to infer from a
        //    role code and a campus id. ──
        var canManagePending = row.Status == UserStatuses.PendingEmailConfirmation
            && CanManagePendingAccount(row.UserId, row.RoleCode, row.SubRole, row.PrimaryCampusId);

        return new ViewAccountDetailsDto
        {
            UserId = row.UserId,
            FullName = row.FullName,
            Email = row.Email,
            Phone = row.Phone,
            Gender = row.Gender,
            Nationality = row.Nationality,
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
            Providers = row.Providers,
            CanEditBasicInfo = canEditBasicInfo,
            EditBasicInfoDisabledReason = editBasicInfoDisabledReason,
            CanResendEmailConfirmation = canManagePending,
            CanEditPendingEmail = canManagePending,
        };
    }

    /// <summary>
    /// Whether the caller may act on a PENDING_EMAIL_CONFIRMATION account: HO within its own scope
    /// (another HO or a Staff Leader), or the Staff Leader of the account's OWN campus over the three
    /// shapes a leader manages. Never one's own account — nobody re-issues their own activation link.
    ///
    /// <para>
    /// This mirrors what <c>PendingAccountAuthorization</c> plus the Staff Leader target rules enforce
    /// at the mutations. Being a display hint does not make it optional to get right: a button offered
    /// and then refused with a 403 is a worse answer than no button, and one withheld from somebody
    /// entitled to it hides the only way forward for that account.
    /// </para>
    /// </summary>
    private bool CanManagePendingAccount(
        ulong targetUserId, string targetRoleCode, string? targetSubRole, ulong? targetCampusId)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null) return false;
        if (_currentUser.UserId.Value == targetUserId) return false;

        if (_currentUser.RoleCode == RoleCodes.Ho)
            return targetRoleCode == RoleCodes.Ho
                || (targetRoleCode == RoleCodes.Staff && targetSubRole == UserSubRoles.Leader);

        var isCampusLeader = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader
            && _currentUser.PrimaryCampusId is not null
            && _currentUser.PrimaryCampusId == targetCampusId;
        if (!isCampusLeader) return false;

        // The same three shapes the mutations allow — never an HO, never another Staff Leader, never a
        // DEPARTMENT/STAFF — read from the one place that defines them.
        return PendingAccountAuthorization.IsStaffLeaderManageableShape(targetRoleCode, targetSubRole);
    }

    /// <summary>
    /// ADMIN may view any account. HO may view only HO and Staff-Leader accounts. Staff Leader is
    /// limited to their own campus. Everyone else — Department Lead, Department staff, IC Staff,
    /// Student, Visitor — has no account-management surface at all here (SEC-02): the "campus-scoped
    /// caller" branch used to fall through to any authenticated same-campus user, which handed full
    /// PII (email, phone, StudentCode, security-provider types) to anyone sharing a campus with the
    /// target. A forbidden target is reported as Not Found so existence is not leaked.
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

        var isStaffLeader = callerRole == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader;
        if (!isStaffLeader || _currentUser.PrimaryCampusId is null || targetCampusId != _currentUser.PrimaryCampusId)
            throw new NotFoundException("Account", _currentUser.UserId ?? 0);
    }
}
