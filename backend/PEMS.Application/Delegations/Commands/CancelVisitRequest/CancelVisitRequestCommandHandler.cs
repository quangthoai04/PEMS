using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CancelVisitRequest;

public sealed class CancelVisitRequestCommandHandler
    : IRequestHandler<CancelVisitRequestCommand, CancelVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public CancelVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<CancelVisitRequestResponse> Handle(
        CancelVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Reason is required (also enforced by the FluentValidation validator; re-checked here
        // so a direct/internal caller can never persist an empty cancellation reason).
        var reason = request.CancellationReason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new BusinessRuleException("Không thể hủy lịch thăm. Vui lòng nhập lý do hủy.");

        var actorId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;

        // Admin must NOT cancel delegations (also enforced by the missing RBAC grant).
        if (roleCode == RoleCodes.Admin)
            throw new ForbiddenException("Admin không có quyền hủy đơn tham quan.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == actorId;

        // §1.1/§4.1: the Visitor owner may cancel a request that is still PENDING_APPROVAL. At this
        // point NO campus instance has a valid lifecycle yet (they stay WAITING_REQUEST_APPROVAL), so
        // we ONLY flip the parent request to CANCELLED and never touch campus instances / logistics.
        // The DB trigger trg_visit_requests_cancel_validate_bu (after the lifecycle patch) permits
        // PENDING→CANCELLED only when the canceller has the VISITOR role — matching this guard.
        if (visit.Status == VisitRequestStatuses.PendingApproval)
        {
            if (!isVisitorOwner)
                throw new ForbiddenException("Chỉ khách sở hữu đơn mới được hủy đơn đang chờ duyệt.");

            var nowPending = _clock.UtcNow;
            await using var txPending = await _db.BeginTransactionAsync(cancellationToken);

            visit.Status = VisitRequestStatuses.Cancelled;
            visit.CancelledBy = actorId;
            visit.CancelledAt = nowPending;
            visit.CancellationReason = reason;
            visit.UpdatedAt = nowPending;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                Action = "CANCEL_VISIT_REQUEST",
                EntityType = "VisitRequest",
                EntityId = visit.VisitRequestId,
                CreatedAt = nowPending
            });

            var cancelledPendingCampuses = new List<CancelledCampusDto>();
            foreach (var instance in visit.CampusInstances.Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval))
            {
                instance.Status = VisitInstanceStatus.Cancelled;
                instance.CancelledBy = actorId;
                instance.CancelledAt = nowPending;
                instance.CancellationActorType = CancellationActorType.Visitor;
                instance.CancellationSource = CancellationSource.SelfService;
                instance.CancellationReason = reason;
                instance.UpdatedAt = nowPending;
                instance.UpdatedBy = actorId;
                instance.RowVersion += 1;
                cancelledPendingCampuses.Add(new CancelledCampusDto(instance.VisitInstanceId, instance.Status));
            }

            await _db.SaveChangesAsync(cancellationToken);

            // --- Notifications for PENDING_APPROVAL cancellation ---
            var notifs = new List<PEMS.Application.Notifications.Common.CreateNotificationItem>();
            if (visit.VisitScope == VisitScopes.MultiCampus)
            {
                var hoUsers = await _db.Users
                    .Where(u => u.Role.RoleCode == "HO" && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                
                notifs.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    id,
                    "Yêu cầu tham quan đã bị hủy",
                    $"Visitor đã hủy yêu cầu liên cơ sở {visit.RequestCode} trước khi được duyệt.",
                    PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    visit.VisitRequestId
                )));
            }
            else
            {
                var campusIds = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
                var staffLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == "CAMPUS" && u.SubRole == "LEADER" && u.PrimaryCampusId.HasValue && campusIds.Contains(u.PrimaryCampusId.Value) && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);
                
                notifs.AddRange(staffLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    id,
                    "Yêu cầu tham quan đã bị hủy",
                    $"Visitor đã hủy yêu cầu {visit.RequestCode} trước khi được duyệt.",
                    PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    visit.VisitRequestId
                )));
            }

            if (notifs.Any())
            {
                await _notificationService.CreateManyAsync(notifs, cancellationToken);
            }

            await txPending.CommitAsync(cancellationToken);

            // No campus instance is cancelled in the pre-approval flow unless they are in WAITING_REQUEST_APPROVAL state.
            return new CancelVisitRequestResponse(
                visit.VisitRequestId,
                visit.Status,
                cancelledPendingCampuses,
                "Đơn tham quan đã được hủy.");
        }

        if (visit.Status != VisitRequestStatuses.Approved)
            throw new BusinessRuleException(
                "Không thể hủy lịch thăm. Chỉ có thể hủy đơn đã được duyệt.");

        // Campus instances that may be cancelled: only after approval and before the visit starts.
        // WAITING_REQUEST_APPROVAL (pending), DURING_VISIT / AFTER_VISIT / CLOSED / CANCELLED are
        // never cancellable through this self-service / external-confirmation flow.
        var cancellableStatuses = new[]
        {
            VisitInstanceStatus.WaitingHostAssignment,
            VisitInstanceStatus.Assigned,
            VisitInstanceStatus.BeforeVisit,
        };

        IReadOnlyList<VisitRequestCampus> targets;
        if (request.VisitInstanceId is { } instanceId)
        {
            var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == instanceId)
                ?? throw new NotFoundException("VisitRequestCampus", instanceId);

            if (instance.Status == VisitInstanceStatus.DuringVisit || 
                instance.Status == VisitInstanceStatus.AfterVisit || 
                instance.Status == VisitInstanceStatus.Closed)
                throw new BusinessRuleException("Cơ sở này đã bắt đầu hoặc đã hoàn tất tiếp khách nên không thể hủy.");

            targets = new[] { instance };
        }
        else
        {
            // Request-level cancellation
            if (visit.VisitScope == VisitScopes.MultiCampus)
            {
                bool hasStartedCampus = visit.CampusInstances.Any(c => 
                    c.Status == VisitInstanceStatus.DuringVisit || 
                    c.Status == VisitInstanceStatus.AfterVisit || 
                    c.Status == VisitInstanceStatus.Closed);
                
                if (hasStartedCampus)
                    throw new BusinessRuleException("Đơn liên cơ sở đã bắt đầu tại một số cơ sở. Vui lòng hủy từng cơ sở chưa diễn ra.");
            }

            targets = visit.CampusInstances.Where(c => cancellableStatuses.Contains(c.Status)).ToList();
        }

        // Authorization + actor classification.
        string actorType;
        string source;
        if (isVisitorOwner)
        {
            actorType = CancellationActorType.Visitor;
            source = CancellationSource.SelfService;
        }
        else if (targets.Count > 0 && targets.All(t => t.CurrentHostUserId == actorId))
        {
            // Current host cancels after the guest confirms via an external channel.
            actorType = CancellationActorType.Host;
            source = CancellationSource.ExternalConfirmation;
        }
        else
        {
            throw new ForbiddenException("Bạn không có quyền hủy lịch thăm này.");
        }

        if (targets.Count == 0)
            throw new BusinessRuleException(
                "Không thể hủy lịch thăm. Không có cơ sở nào ở trạng thái có thể hủy.");

        var now = _clock.UtcNow;

        // Pre-validate every target BEFORE any write, so a violation never reaches SaveChanges
        // (and the user never sees a raw EF/MySQL exception).
        foreach (var instance in targets)
        {
            if (instance.Status == VisitInstanceStatus.DuringVisit || 
                instance.Status == VisitInstanceStatus.AfterVisit || 
                instance.Status == VisitInstanceStatus.Closed)
                throw new BusinessRuleException("Cơ sở này đã bắt đầu hoặc đã hoàn tất tiếp khách nên không thể hủy.");

            if (!cancellableStatuses.Contains(instance.Status))
                throw new BusinessRuleException(
                    "Không thể hủy lịch thăm. Cơ sở đang ở trạng thái không thể hủy.");

            if (now >= instance.PlannedStartAt)
                throw new BusinessRuleException(
                    "Không thể hủy lịch thăm. Đã đến hoặc quá thời gian bắt đầu.");
        }

        // ── Write phase. Child campus instances are cancelled and persisted FIRST, while the
        // parent request is still APPROVED, because the visit_request_campuses trigger requires
        // the owning request to be APPROVED at the moment a campus moves to CANCELLED. Only then
        // is the parent request flipped to CANCELLED. The whole thing runs in one transaction so
        // it commits atomically (no half-cancelled state if the second step fails). ──
        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        var cancelled = new List<CancelledCampusDto>();
        foreach (var instance in targets)
        {
            instance.Status = VisitInstanceStatus.Cancelled;
            instance.CancelledBy = actorId;
            instance.CancelledAt = now;
            instance.CancellationActorType = actorType;
            instance.CancellationSource = source;
            instance.CancellationReason = reason;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;

            cancelled.Add(new CancelledCampusDto(instance.VisitInstanceId, instance.Status));

            await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidateTokensForVisitInstanceAsync(
                _db, instance.VisitInstanceId, "Chuyến tiếp khách này đã bị hủy.", now, cancellationToken);
        }

        // Cascade: when a campus instance is cancelled, every NON-terminal logistics item of that
        // instance must follow it to CANCELLED so no department keeps preparing for a dead visit
        // (đặc tả mục 4.4 / 11). Terminal items (DONE/REJECTED/DECLINED/CANCELLED) are left as-is.
        // Pending logistics email-action tokens are already invalidated above via
        // InvalidateTokensForVisitInstanceAsync, so a later token click returns INVALID_STATE.
        var targetInstanceIds = targets.Select(t => t.VisitInstanceId).ToList();
        var terminalLogisticsStatuses = new[]
        {
            LogisticsItemStatus.Done,
            LogisticsItemStatus.Rejected,
            LogisticsItemStatus.Declined,
            LogisticsItemStatus.Cancelled,
        };
        var logisticsToCancel = await _db.VisitLogisticsItems
            .Where(l => targetInstanceIds.Contains(l.VisitInstanceId)
                        && !terminalLogisticsStatuses.Contains(l.Status))
            .ToListAsync(cancellationToken);
        foreach (var item in logisticsToCancel)
        {
            item.Status = LogisticsItemStatus.Cancelled;
            // decision_note nối lý do hủy cụ thể của campus (reason luôn bắt buộc ở luồng này).
            item.DecisionNote = $"Hủy logistics do campus instance đã hủy. Lý do: {reason}";
            item.UpdatedAt = now;
            item.UpdatedBy = actorId;
            item.RowVersion += 1;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "CANCEL_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        // Persist the campus cancellations first (parent still APPROVED → trigger passes).
        await _db.SaveChangesAsync(cancellationToken);

        // Roll the whole request up to CANCELLED ONLY for the Visitor self-service flow. The
        // visit_requests cancel trigger (trg_visit_requests_cancel_validate_bu) requires
        // cancelled_by to have the VISITOR role — a HOST cancels a campus INSTANCE only (external
        // confirmation) and must NEVER flip the parent request, otherwise the trigger SIGNALs and
        // the whole operation surfaces as a generic 500. So a HOST cancel leaves the request
        // APPROVED with the instance CANCELLED (the list shows an instance-level cancellation).
        var allCancelled = visit.CampusInstances.All(c => c.Status == VisitInstanceStatus.Cancelled);
        var requestRolledUp = allCancelled && isVisitorOwner;
        if (requestRolledUp)
        {
            visit.Status = VisitRequestStatuses.Cancelled;
            visit.CancelledBy = actorId;
            visit.CancelledAt = now;
            // Cancel flow uses cancellation_reason — never decision_note / decided_by / decided_at.
            visit.CancellationReason = reason;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;

            await _db.SaveChangesAsync(cancellationToken);
        }

        // --- Notifications for AFTER_APPROVAL cancellation ---
        var afterNotifs = new List<PEMS.Application.Notifications.Common.CreateNotificationItem>();
        var hoUsersToNotify = new List<ulong>();
        if (visit.VisitScope == VisitScopes.MultiCampus && isVisitorOwner)
        {
            hoUsersToNotify = await _db.Users
                .Where(u => u.Role.RoleCode == "HO" && u.Status == "ACTIVE")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);
        }

        foreach (var instance in targets)
        {
            var staffLeaderId = instance.CoordinatorUserId;
            if (isVisitorOwner)
            {
                if (instance.CurrentHostUserId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                        instance.CurrentHostUserId.Value,
                        "Lịch thăm quan bị hủy",
                        $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        instance.VisitInstanceId
                    ));
                }
                if (staffLeaderId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                        staffLeaderId.Value,
                        "Lịch thăm quan bị hủy",
                        $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        instance.VisitInstanceId
                    ));
                }
                foreach (var ho in hoUsersToNotify)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                        ho,
                        "Lịch thăm quan bị hủy",
                        $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        instance.VisitInstanceId
                    ));
                }
            }
            else // Host cancelled
            {
                if (visit.VisitorUserId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                        visit.VisitorUserId.Value,
                        "Lịch thăm quan bị hủy",
                        $"Host đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        instance.VisitInstanceId
                    ));
                }
                if (staffLeaderId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                        staffLeaderId.Value,
                        "Lịch thăm quan bị hủy",
                        $"Host đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        instance.VisitInstanceId
                    ));
                }
            }
        }
        if (afterNotifs.Any())
        {
            await _notificationService.CreateManyAsync(afterNotifs, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new CancelVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            cancelled,
            requestRolledUp
                ? "Đơn tham quan đã được hủy."
                : "Cơ sở đã được hủy. Các cơ sở còn lại của đơn vẫn giữ nguyên.");
    }
}
