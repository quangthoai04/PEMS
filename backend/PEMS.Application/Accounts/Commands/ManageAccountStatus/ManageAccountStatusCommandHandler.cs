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

    public ManageAccountStatusCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISessionService sessionService,
        ISecurityAuditService audit,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _sessionService = sessionService;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ManageAccountStatusResponse> Handle(ManageAccountStatusCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUser.UserId;
        var actorCampus = _currentUser.PrimaryCampusId;
        var privileged = AccountProvisioningRules.IsPrivileged(_currentUser.RoleCode);

        var newStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("Account", request.UserId);

        // An actor cannot change the status of their own account (prevents self-lockout).
        if (actorId is not null && user.UserId == actorId.Value)
            throw new ForbiddenException("You cannot change the status of your own account.");

        // Staff Leaders (non-privileged) may only manage users within their own campus.
        if (!privileged)
        {
            if (actorCampus is null)
                throw new ForbiddenException("Your account is not assigned to a campus and cannot manage accounts.");
            if (user.PrimaryCampusId is null || user.PrimaryCampusId != actorCampus)
                throw new ForbiddenException("You can only manage accounts within your own campus.");
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
