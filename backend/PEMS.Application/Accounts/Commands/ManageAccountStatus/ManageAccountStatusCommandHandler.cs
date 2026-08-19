using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Accounts.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Accounts.Commands.ManageAccountStatus;

/// <summary>
/// UC-97 handler. Updates <c>users.status</c> and, when the account leaves the ACTIVE
/// state, revokes every active session via <see cref="ISessionService"/> so existing
/// access/refresh tokens stop working immediately. This complements
/// SessionValidationMiddleware (which already blocks non-ACTIVE users per request) by
/// making the DB reflect the security state. Mirrors the scope/audit pattern used by
/// <c>UpdateAccountRoleCommandHandler</c>.
///
/// <para>
/// One endpoint, two meanings — split by caller role (ADMIN_ACCOUNT_MANAGEMENT spec §16):
/// </para>
/// <list type="bullet">
/// <item><b>ADMIN — security control.</b> ACTIVE → LOCKED and LOCKED → ACTIVE only, reason mandatory,
/// never their own account. LOCKED is a security state (suspicious login, suspected compromise,
/// investigation), so it is deliberately NOT reachable from INACTIVE or PENDING: an account that
/// already cannot sign in needs no lock on top, and that single rule is what removes any need for a
/// <c>status_before_lock</c> column — unlock always means "back to ACTIVE".</item>
/// <item><b>HO / Staff Leader — business status.</b> ACTIVE ↔ INACTIVE within their own scope. They
/// can neither set LOCKED nor move an account out of it: a security lock is lifted by ADMIN after the
/// investigation, not by the personnel manager who happens to see a locked row.</item>
/// <item><b>Everyone else</b> is refused here, not merely denied the screen.</item>
/// </list>
/// </summary>
public sealed class ManageAccountStatusCommandHandler : IRequestHandler<ManageAccountStatusCommand, ManageAccountStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISessionService _sessionService;
    private readonly ISecurityAuditService _audit;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ManageAccountStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        ISecurityAuditService audit,
        IDateTimeService clock,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _audit = audit;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<ManageAccountStatusResponse> Handle(ManageAccountStatusCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        var isAdminCaller = _currentUser.RoleCode == RoleCodes.Admin;
        var isHoCaller = _currentUser.RoleCode == RoleCodes.Ho;
        var isStaffLeaderCaller = _currentUser.RoleCode == RoleCodes.Staff
            && _currentUser.SubRole == UserSubRoles.Leader;

        // Fail closed (spec §16.4): only these three callers manage account status. Any other role —
        // a DEPARTMENT leader, an IC staff member, a student holding a valid token — is refused here
        // rather than relying on /dashboard/accounts not being on their menu.
        if (!isAdminCaller && !isHoCaller && !isStaffLeaderCaller)
            throw new AuthBusinessException(
                AccountErrorCodes.AccountStatusChangeForbidden,
                "Bạn không có quyền thay đổi trạng thái tài khoản.", 403);

        var newStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // An actor cannot change the status of their own account (prevents self-lockout). For ADMIN
        // this is the security self-guard of spec §20 and carries its own code — the frontend already
        // hides the action on the caller's own row, and this is the layer that makes that real.
        if (actorId is not null && user.UserId == actorId.Value)
            throw isAdminCaller
                ? new AuthBusinessException(
                    AccountErrorCodes.AccountSecuritySelfActionForbidden,
                    "Bạn không thể khóa hoặc mở khóa bảo mật cho chính tài khoản của mình.", 403)
                : new ForbiddenException("You cannot change the status of your own account.");

        // ── ADMIN — security control only. Evaluated BEFORE the pending/business rules below so the
        //    refusal names the security boundary that was crossed instead of pointing ADMIN at a
        //    pending-email flow it is not allowed to run either. ──
        var securityAction = isAdminCaller
            ? ResolveAdminSecurityAction(user.Status, newStatus, request.Reason)
            : SecurityAction.None;

        // P0 #1: a PENDING_EMAIL_CONFIRMATION account is activated ONLY by confirming its email — never via
        // this status toggle (that would bypass the email-ownership proof). Manage it through resend / edit
        // email / cancel instead.
        if (!isAdminCaller && user.Status == UserStatuses.PendingEmailConfirmation)
            throw new BusinessRuleException(
                "Tài khoản đang chờ xác nhận email. Chỉ có thể kích hoạt bằng cách xác nhận email; vui lòng dùng chức năng gửi lại/huỷ xác nhận.",
                "ACCOUNT_PENDING_EMAIL_CONFIRMATION");

        // ── HO scope (HO_BASIC_INFO spec §14): an HO may enable/disable another HO (any campus)
        //    or a Staff Leader, only between ACTIVE and INACTIVE, and never a LOCKED account.
        //    The self-account guard above already blocks changing one's own status. ──
        if (isHoCaller)
        {
            var targetInScope = user.Role.RoleCode == RoleCodes.Ho
                || (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Leader);
            if (!targetInScope)
                throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của HO.");

            if (newStatus == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "HO chỉ được phép kích hoạt hoặc vô hiệu hóa tài khoản. Khóa bảo mật do ADMIN thực hiện.",
                    AccountErrorCodes.SecurityStatusRequiresAdmin);

            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và chỉ ADMIN mới mở khóa được.",
                    AccountErrorCodes.SecurityStatusRequiresAdmin);
        }

        // Staff Leaders (non-privileged) may only manage users within their own campus.
        if (!privileged)
        {
            if (actorCampus is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage accounts.");
            if (user.PrimaryCampusId is null || user.PrimaryCampusId != actorCampus)
                throw new ForbiddenException("You can only manage accounts within your own campus.");
        }

        // ── UC-97-SL Staff Leader scope: may only enable/disable STAFF/STAFF,
        //    DEPARTMENT/LEADER and STUDENT, only between ACTIVE and INACTIVE, never a
        //    LOCKED (security) account. ──
        if (isStaffLeaderCaller)
        {
            var targetInScope =
                (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Staff)
                || (user.Role.RoleCode == RoleCodes.Department && user.SubRole == UserSubRoles.Leader)
                || user.Role.RoleCode == RoleCodes.Student;
            if (!targetInScope)
                throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của bạn.");

            if (newStatus == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Chỉ cho phép kích hoạt hoặc vô hiệu hóa tài khoản. Khóa bảo mật do ADMIN thực hiện.",
                    AccountErrorCodes.SecurityStatusRequiresAdmin);

            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và chỉ ADMIN mới mở khóa được.",
                    AccountErrorCodes.SecurityStatusRequiresAdmin);
        }

        var previousStatus = user.Status;

        // Idempotent no-op: status already at the requested value.
        if (string.Equals(previousStatus, newStatus, StringComparison.Ordinal))
        {
            return new ManageAccountStatusResponse
            {
                UserId = user.UserId,
                Status = newStatus,
                RevokedSessions = 0,
                Message = "Account status is already set to the requested value.",
            };
        }

        var now = _clock.VietnamNow;
        user.Status = newStatus;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;

        // Re-activating an account clears any leftover temporary lockout so the user can sign in again.
        if (newStatus == UserStatuses.Active)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
        }

        // A security lock/unlock is recorded under its own action so the audit trail separates
        // "personnel disabled this account" from "security locked this account" (spec §21). Same
        // AuditLog/AuditLogChange rows either way — no new table, no separate logging path.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = user.PrimaryCampusId ?? actorCampus,
            Action = securityAction switch
            {
                SecurityAction.Lock => "SECURITY_LOCK_ACCOUNT",
                SecurityAction.Unlock => "SECURITY_UNLOCK_ACCOUNT",
                _ => "MANAGE_ACCOUNT_STATUS",
            },
            EntityType = "User",
            EntityId = user.UserId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "Status",
                    OldValueText = previousStatus,
                    NewValueText = JsonSerializer.Serialize(new { status = newStatus, reason = request.Reason })
                }
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // The holder is told what happened to their account in the terms that actually apply: a
        // security lock is not "vô hiệu hóa", and an unlock is an invitation to sign in again. No new
        // template and no email — this reuses the notification the flow already created.
        var notificationMessage = securityAction switch
        {
            SecurityAction.Lock => "Tài khoản của bạn đã bị khóa vì lý do bảo mật.",
            SecurityAction.Unlock => "Tài khoản của bạn đã được mở khóa và có thể đăng nhập lại.",
            _ => $"Tài khoản của bạn đã được {(newStatus == UserStatuses.Active ? "kích hoạt" : newStatus == UserStatuses.Inactive ? "vô hiệu hóa" : "khóa")}.",
        };
        // The HO/Staff-Leader business-status branch (the `_ =>` case) is Active/Inactive — give it
        // its own eventKey pair too, distinct from the two ADMIN security actions, instead of
        // leaving it on MetadataJson=null (that branch never reaches VISITOR — targetInScope above
        // excludes it — but it does reach Staff/Department/Student, who need the same semantic
        // vocabulary as everyone else now).
        var accountStatusEventKey = securityAction switch
        {
            SecurityAction.Lock => PEMS.Application.Notifications.Common.NotificationEventKeys.AccountLocked,
            SecurityAction.Unlock => PEMS.Application.Notifications.Common.NotificationEventKeys.AccountUnlocked,
            _ => newStatus == UserStatuses.Active
                ? PEMS.Application.Notifications.Common.NotificationEventKeys.AccountStatusActivated
                : newStatus == UserStatuses.Inactive
                    ? PEMS.Application.Notifications.Common.NotificationEventKeys.AccountStatusDeactivated
                    : null,
        };
        // Same ACCOUNT_LIST route-guard mismatch as account creation: only ADMIN/HO/Staff-Leader can
        // open '/dashboard/accounts'. The recipient here is the account HOLDER, who is frequently
        // none of those (Staff, Department, Student) — route them to their own Profile instead.
        var recipientCanViewAccountList = user.Role.RoleCode == RoleCodes.Admin
            || user.Role.RoleCode == RoleCodes.Ho
            || (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Leader);
        await _notificationService.CreateAsync(
            new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: user.UserId,
                Title: "Cập nhật trạng thái tài khoản",
                Message: notificationMessage,
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.AccountStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.Account,
                RelatedId: user.UserId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Account,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenAccountDetail,
                ActionUrl: recipientCanViewAccountList ? "/dashboard/accounts" : "/dashboard/profile",
                MetadataJson: accountStatusEventKey is null
                    ? null
                    : PEMS.Application.Notifications.Common.NotificationEventKeys.BuildMetadata(
                        accountStatusEventKey, new { })),
            cancellationToken
        );

        // Revoke all active sessions when the account leaves the ACTIVE state. A security lock names
        // itself in the revoke reason so an operator reading user_sessions can tell a locked-out
        // account from a deactivated one. Unlock deliberately revokes nothing and restores nothing:
        // the old sessions stay dead and the holder signs in again (spec §19).
        var revoked = 0;
        var leftActiveState = previousStatus == UserStatuses.Active && newStatus != UserStatuses.Active;
        if (leftActiveState)
        {
            revoked = await _sessionService.RevokeAllActiveSessionsAsync(
                user.UserId,
                securityAction == SecurityAction.Lock
                    ? SessionRevokeReasons.AccountSecurityLocked
                    : SessionRevokeReasons.AccountDeactivated,
                actorId, cancellationToken);
        }

        return new ManageAccountStatusResponse
        {
            UserId = user.UserId,
            Status = newStatus,
            RevokedSessions = revoked,
            Message = securityAction switch
            {
                SecurityAction.Lock => "Đã khóa tài khoản vì lý do bảo mật và thu hồi toàn bộ phiên đăng nhập.",
                SecurityAction.Unlock => "Đã mở khóa tài khoản. Người dùng cần đăng nhập lại; các phiên cũ không được khôi phục.",
                _ => leftActiveState
                    ? "Account status updated. All active sessions have been revoked."
                    : "Account status updated successfully.",
            },
        };
    }

    /// <summary>What an ADMIN request resolves to once the state matrix has accepted it.</summary>
    private enum SecurityAction { None, Lock, Unlock }

    /// <summary>
    /// The ADMIN state matrix (spec §4/§16.1). Exactly two transitions are permitted, both need a
    /// reason, and everything else is refused with the code that says which rule it broke:
    /// <list type="bullet">
    /// <item>ACTIVE → LOCKED — security lock.</item>
    /// <item>LOCKED → ACTIVE — security unlock.</item>
    /// </list>
    /// Notably refused: ACTIVE ↔ INACTIVE (that is business/personnel status, owned by HO / Staff
    /// Leader), INACTIVE → LOCKED (an account that already cannot sign in needs no security lock on
    /// top), and anything involving PENDING_EMAIL_CONFIRMATION.
    /// </summary>
    private static SecurityAction ResolveAdminSecurityAction(string currentStatus, string requestedStatus, string? reason)
    {
        // The reason is what turns a status write into a security record, so it is required before the
        // transition is even considered — an ADMIN who forgot it is told that, not told their
        // transition is invalid.
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException(
                "Vui lòng nhập lý do khóa hoặc mở khóa bảo mật.",
                AccountErrorCodes.SecurityReasonRequired);

        if (requestedStatus == UserStatuses.Locked)
        {
            if (currentStatus == UserStatuses.Active)
                return SecurityAction.Lock;

            throw new BusinessRuleException(
                currentStatus == UserStatuses.Inactive
                    ? "Tài khoản đang vô hiệu hóa theo nghiệp vụ nên không cần khóa bảo mật."
                    : currentStatus == UserStatuses.PendingEmailConfirmation
                        ? "Tài khoản đang chờ xác nhận email nên chưa thể khóa bảo mật."
                        : "Chỉ tài khoản đang hoạt động mới có thể khóa bảo mật.",
                AccountErrorCodes.SecurityLockInvalidState);
        }

        if (requestedStatus == UserStatuses.Active)
        {
            if (currentStatus == UserStatuses.Locked)
                return SecurityAction.Unlock;

            throw new BusinessRuleException(
                currentStatus == UserStatuses.Inactive
                    ? "Kích hoạt lại tài khoản đang vô hiệu hóa là nghiệp vụ nhân sự, thuộc về Head Office / Trưởng phòng IC."
                    : "Chỉ tài khoản đang bị khóa bảo mật mới có thể mở khóa.",
                currentStatus == UserStatuses.Inactive
                    ? AccountErrorCodes.AdminBusinessStatusChangeNotAllowed
                    : AccountErrorCodes.SecurityUnlockInvalidState);
        }

        // requestedStatus == INACTIVE (the validator's whitelist admits nothing else).
        throw new BusinessRuleException(
            "ADMIN không thay đổi trạng thái nghiệp vụ (kích hoạt / vô hiệu hóa) của tài khoản; thao tác này thuộc về Head Office / Trưởng phòng IC.",
            AccountErrorCodes.AdminBusinessStatusChangeNotAllowed);
    }
}
