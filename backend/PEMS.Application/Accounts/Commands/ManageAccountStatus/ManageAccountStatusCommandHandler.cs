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

        var newStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // An actor cannot change the status of their own account (prevents self-lockout).
        if (actorId is not null && user.UserId == actorId.Value)
            throw new ForbiddenException("You cannot change the status of your own account.");

        // ── UC-97 HO scope: HO may only enable/disable HO and Staff-Leader accounts, only
        //    between ACTIVE and INACTIVE, and never a LOCKED (security) account. ──
        if (_currentUser.RoleCode == RoleCodes.Ho)
        {
            // HO accounts require a dedicated Re-enable/Unlock/Replace flow — never the generic
            // toggle. Block every HO target regardless of campus (spec §7.3). The frontend hides
            // the toggle, but the backend is the final gate against a direct API call.
            if (user.Role.RoleCode == RoleCodes.Ho)
                throw new AuthBusinessException(
                    AccountErrorCodes.HoStatusChangeRequiresSpecialFlow,
                    "Không thể thay đổi trạng thái tài khoản HO bằng thao tác thông thường. Vui lòng sử dụng luồng xử lý HO chuyên biệt.",
                    403);

            // HO may only enable/disable Staff-Leader accounts, only between ACTIVE and INACTIVE,
            // and never a LOCKED (security) account.
            if (!(user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Leader))
                throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của HO.");

            if (newStatus == UserStatuses.Locked)
                throw new BusinessRuleException("UC-97 chỉ cho phép kích hoạt hoặc vô hiệu hóa tài khoản.");

            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và không thể thay đổi tại đây.");
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
        if (_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader)
        {
            var targetInScope =
                (user.Role.RoleCode == RoleCodes.Staff && user.SubRole == UserSubRoles.Staff)
                || (user.Role.RoleCode == RoleCodes.Department && user.SubRole == UserSubRoles.Leader)
                || user.Role.RoleCode == RoleCodes.Student;
            if (!targetInScope)
                throw new ForbiddenException("Tài khoản này nằm ngoài phạm vi quản lý của bạn.");

            if (newStatus == UserStatuses.Locked)
                throw new BusinessRuleException("Chỉ cho phép kích hoạt hoặc vô hiệu hóa tài khoản.");

            if (user.Status == UserStatuses.Locked)
                throw new BusinessRuleException(
                    "Tài khoản đang bị khóa vì lý do bảo mật và không thể thay đổi tại đây.");
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

        var now = _clock.UtcNow;
        user.Status = newStatus;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;

        // Re-activating an account clears any leftover temporary lockout so the user can sign in again.
        if (newStatus == UserStatuses.Active)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = user.PrimaryCampusId ?? actorCampus,
            Action = "MANAGE_ACCOUNT_STATUS",
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

        string statusText = newStatus == UserStatuses.Active ? "kích hoạt" : (newStatus == UserStatuses.Inactive ? "vô hiệu hóa" : "khóa");
        await _notificationService.CreateAsync(
            new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: user.UserId,
                Title: "Cập nhật trạng thái tài khoản",
                Message: $"Tài khoản của bạn đã được {statusText}.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.AccountStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.Account,
                RelatedId: user.UserId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Account,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenAccountDetail,
                ActionUrl: "/dashboard/accounts"),
            cancellationToken
        );

        // Revoke all active sessions when the account leaves the ACTIVE state.
        var revoked = 0;
        var leftActiveState = previousStatus == UserStatuses.Active && newStatus != UserStatuses.Active;
        if (leftActiveState)
        {
            revoked = await _sessionService.RevokeAllActiveSessionsAsync(
                user.UserId, SessionRevokeReasons.AccountDeactivated, actorId, cancellationToken);


        }

        return new ManageAccountStatusResponse
        {
            UserId = user.UserId,
            Status = newStatus,
            RevokedSessions = revoked,
            Message = leftActiveState
                ? "Account status updated. All active sessions have been revoked."
                : "Account status updated successfully.",
        };
    }
}
