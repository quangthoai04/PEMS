using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Commands.ChangePersonnelStatus;

/// <summary>
/// Spec §15. Runs the SAME <see cref="DepartmentPersonnelStatusRules"/> evaluation the preview
/// endpoint shows, under a row lock, and refuses the change if any blocker is present — the preview is
/// a convenience for the operator, never the authorization.
///
/// Deliberately narrow: only ACTIVE↔INACTIVE. A PENDING account activates by confirming its email
/// (activating it here would bypass the ownership proof) and a LOCKED account needs the security
/// unlock flow, so neither can be reached through this toggle. Disabling never deletes the user, never
/// removes them from the department and never reassigns their work — unfinished responsibilities are a
/// blocker, not something to silently transfer (spec §14).
/// </summary>
public sealed class ChangePersonnelStatusCommandHandler
    : IRequestHandler<ChangePersonnelStatusCommand, ChangePersonnelStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IDepartmentLeaderPersonnelScopeService _scopeService;
    private readonly IUserMutationLockService _lockService;
    private readonly ISessionService _sessionService;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;

    public ChangePersonnelStatusCommandHandler(
        IApplicationDbContext db,
        IDepartmentLeaderPersonnelScopeService scopeService,
        IUserMutationLockService lockService,
        ISessionService sessionService,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock)
    {
        _db = db;
        _scopeService = scopeService;
        _lockService = lockService;
        _sessionService = sessionService;
        _dispatcher = dispatcher;
        _clock = clock;
    }

    public async Task<ChangePersonnelStatusResponse> Handle(
        ChangePersonnelStatusCommand request, CancellationToken cancellationToken)
    {
        var scope = await _scopeService.EnsureCurrentUserIsActualDepartmentLeaderAsync(cancellationToken);

        var targetStatus = (request.TargetStatus ?? string.Empty).Trim().ToUpperInvariant();
        if (!DepartmentPersonnelStatusRules.IsSupportedTargetStatus(targetStatus))
            throw new BusinessRuleException(
                "Chỉ hỗ trợ chuyển trạng thái sang ACTIVE hoặc INACTIVE.",
                DepartmentLeaderErrorCodes.PersonnelInvalidStatus);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var now = _clock.VietnamNow;

        string previousStatus;
        string fullName;
        string email;

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            // Lock the target, then the department — the same order every other flow uses, so a
            // concurrent responsibility assignment either lands before this check or blocks until the
            // disable has committed (spec §12.3).
            await _lockService.LockUsersAsync(new[] { request.UserId }, cancellationToken);
            await _lockService.LockDepartmentsAsync(new[] { scope.DepartmentId }, cancellationToken);

            await _scopeService.EnsureTargetBelongsToCurrentDepartmentAsync(
                scope, request.UserId, cancellationToken);

            var target = await _db.Users
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken)
                ?? throw new AuthBusinessException(
                    DepartmentLeaderErrorCodes.PersonnelNotFound,
                    "Không tìm thấy nhân sự trong phòng ban của bạn.", 404);

            var department = await _db.Departments
                .Include(d => d.Campus)
                .FirstAsync(d => d.DepartmentId == scope.DepartmentId, cancellationToken);

            previousStatus = target.Status;
            fullName = target.FullName;
            email = target.Email;

            // Re-evaluated under the lock. The screen's preview may be seconds old; this is the verdict
            // that counts.
            var impact = await DepartmentPersonnelStatusRules.EvaluateAsync(
                _db, scope, target, targetStatus,
                department.Status, department.Campus.Status, department.HeadUserId,
                now, cancellationToken);

            if (!impact.CanChangeStatus) throw BuildRefusal(impact);

            target.Status = targetStatus;
            target.UpdatedAt = now;
            target.UpdatedBy = scope.ActorUserId;

            // Re-enabling clears a temporary login lockout so the account can actually sign in again.
            // A LOCKED account can never reach here (it is blocked above), so this cannot erase a
            // security lock (spec §12.9/§15).
            if (targetStatus == UserStatuses.Active)
            {
                target.FailedLoginCount = 0;
                target.LockedUntil = null;
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = scope.ActorUserId,
                CampusId = scope.CampusId,
                Action = targetStatus == UserStatuses.Inactive
                    ? DepartmentPersonnelAuditActions.DisablePersonnel
                    : DepartmentPersonnelAuditActions.EnablePersonnel,
                EntityType = "User",
                EntityId = target.UserId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Status",
                        OldValueText = previousStatus,
                        NewValueText = JsonSerializer.Serialize(new
                        {
                            status = targetStatus,
                            reason,
                            departmentId = scope.DepartmentId,
                            activeSessionCount = impact.ActiveSessionCount,
                        }),
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── Post-commit. Disable cuts existing access immediately; enable does NOT restore sessions —
        //    the user signs in again to get a fresh token (spec §15). ──
        var revoked = 0;
        if (targetStatus == UserStatuses.Inactive)
        {
            revoked = await _sessionService.RevokeAllActiveSessionsAsync(
                request.UserId, SessionRevokeReasons.AccountDeactivated, scope.ActorUserId, cancellationToken);
        }

        var emailStatus = await SendStatusMailAsync(
            request.UserId, targetStatus, fullName, email, scope.DepartmentName, reason,
            scope.ActorUserId, cancellationToken);

        return new ChangePersonnelStatusResponse
        {
            Success = true,
            UserId = request.UserId,
            PreviousStatus = previousStatus,
            Status = targetStatus,
            RevokedSessions = revoked,
            EmailNotificationStatus = emailStatus,
            Message = targetStatus == UserStatuses.Inactive
                ? $"Đã vô hiệu hóa nhân sự. {revoked} phiên đăng nhập đã bị thu hồi."
                : "Đã kích hoạt lại nhân sự. Nhân sự cần đăng nhập lại để tiếp tục sử dụng hệ thống.",
        };
    }

    /// <summary>
    /// Turns the blocker list into the right HTTP shape: an active-responsibility blocker is a 409
    /// (state conflict the operator can resolve elsewhere) carrying the full list under <c>data</c>;
    /// everything else is a 422 rule violation. The first blocker supplies the code and message.
    /// </summary>
    private static System.Exception BuildRefusal(DepartmentPersonnelStatusImpact impact)
    {
        var primary = impact.Blockers[0];

        var isConflict = impact.Blockers.Any(
            b => b.Code == DepartmentLeaderErrorCodes.PersonnelHasActiveResponsibilities);

        if (isConflict)
        {
            var responsibility = impact.Blockers.First(
                b => b.Code == DepartmentLeaderErrorCodes.PersonnelHasActiveResponsibilities);

            return new ConflictException(
                responsibility.Message,
                DepartmentLeaderErrorCodes.PersonnelHasActiveResponsibilities,
                new { blockers = impact.Blockers });
        }

        return new BusinessRuleException(primary.Message, primary.Code);
    }

    /// <summary>Best-effort notification. A delivery failure never rolls the committed status back.</summary>
    private async Task<string> SendStatusMailAsync(
        ulong targetUserId,
        string targetStatus,
        string fullName,
        string email,
        string departmentName,
        string? reason,
        ulong? actorId,
        CancellationToken cancellationToken)
    {
        var disabling = targetStatus == UserStatuses.Inactive;

        var variables = new Dictionary<string, string>
        {
            ["fullName"] = fullName,
            ["departmentName"] = departmentName,
        };

        if (disabling)
        {
            // The template always renders the reason line, so an omitted reason has to say so rather
            // than leave the recipient reading a blank label.
            variables["reason"] = string.IsNullOrWhiteSpace(reason)
                ? "Không có lý do được cung cấp."
                : reason!;
        }

        try
        {
            var result = await _dispatcher.SendAsync(new SystemEmailRequest(
                disabling
                    ? SystemEmailTemplates.DeptPersonnelAccountDisabled
                    : SystemEmailTemplates.DeptPersonnelAccountEnabled,
                new EmailRecipient(email, fullName),
                variables,
                RelatedType: "User",
                RelatedId: targetUserId,
                SentBy: actorId), cancellationToken);

            return result.Delivery.Status switch
            {
                EmailDeliveryStatus.Sent => DepartmentPersonnelEmails.StatusSent,
                EmailDeliveryStatus.Skipped => DepartmentPersonnelEmails.StatusSkipped,
                _ => DepartmentPersonnelEmails.StatusFailed,
            };
        }
        catch
        {
            return DepartmentPersonnelEmails.StatusFailed;
        }
    }
}
