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

            // Rule 24h (spec §2.3): the Visitor may only self-cancel while EVERY still-active
            // campus starts ≥ 24h from now. planned_start_at is local wall-clock → VietnamNow.
            var vnNowPending = _clock.VietnamNow;
            if (visit.CampusInstances.Any(c =>
                    c.Status != VisitInstanceStatus.Cancelled
                    && c.Status != VisitInstanceStatus.Rejected
                    && c.PlannedStartAt < vnNowPending.AddHours(24)))
                throw new BusinessRuleException(
                    "Lịch thăm sắp diễn ra trong vòng 24 giờ. Vui lòng liên hệ FPTU để được hỗ trợ hủy/thay đổi.",
                    VisitRequestErrorCodes.VisitCancelWindowExpired);

            var nowPending = _clock.VietnamNow;
            await using var txPending = await _db.BeginTransactionAsync(cancellationToken);

            // Phase 1 — flip the request FIRST. The visit_request_campuses cancel trigger only
            // allows a WAITING_REQUEST_APPROVAL instance to become CANCELLED when the parent
            // request is already CANCELLED (pending-request cascade), and EF flushes campus rows
            // BEFORE visit_requests within one SaveChanges — so this must be two-phase.
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

            await _db.SaveChangesAsync(cancellationToken);

            // Phase 2 — cascade-cancel the still-pending campus instances.
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
            // Campus-independent approval: the pending instances sat with the campus Staff
            // Leaders (never HO) — notify them for every scope.
            var notifs = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
            {
                var campusIds = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
                var staffLeaders = await _db.Users
                    .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == "LEADER" && u.PrimaryCampusId.HasValue && campusIds.Contains(u.PrimaryCampusId.Value) && u.Status == "ACTIVE")
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);

                notifs.AddRange(staffLeaders.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                    RecipientUserId: id,
                    Title: "Yêu cầu tham quan đã bị hủy",
                    Message: $"Visitor đã hủy yêu cầu {visit.RequestCode} trước khi được xử lý.",
                    NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                    RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    RelatedId: visit.VisitRequestId,
                    ActorUserId: actorId,
                    Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                    VisitRequestId: visit.VisitRequestId,
                    ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                    ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                )));
            }

            if (notifs.Any())
            {
                await _notificationService.CreateManyAsync(notifs, cancellationToken);
            }

            await txPending.CommitAsync(cancellationToken);

            return new CancelVisitRequestResponse(
                visit.VisitRequestId,
                visit.Status,
                cancelledPendingCampuses,
                "Đơn tham quan đã được hủy.");
        }

        if (visit.Status != VisitRequestStatuses.Approved && visit.Status != VisitRequestStatuses.PartiallyApproved)
            throw new BusinessRuleException(
                "Không thể hủy lịch thăm. Chỉ có thể hủy đơn đã được duyệt (toàn phần hoặc một phần).");

        // Campus instances that may be cancelled: only after approval and before the visit starts,
        // OR while it is still waiting for request approval (pending).
        var cancellableStatuses = new[]
        {
            VisitInstanceStatus.WaitingRequestApproval,
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
                throw new BusinessRuleException(
                    "Cơ sở này đã bắt đầu hoặc đã hoàn tất tiếp khách nên không thể hủy.",
                    VisitRequestErrorCodes.VisitAlreadyStartedCannotCancel);

            targets = new[] { instance };
        }
        else
        {
            // Request-level cancellation
            if (isVisitorOwner)
            {
                // Rule 1: Visitor cancel at Request level requires ALL active campuses to be >= 24h
                var activeInstances = visit.CampusInstances.Where(c =>
                    c.Status != VisitInstanceStatus.Cancelled &&
                    c.Status != VisitInstanceStatus.Rejected).ToList();

                if (activeInstances.Any(c => c.PlannedStartAt < _clock.VietnamNow.AddHours(24)))
                {
                    throw new BusinessRuleException(
                        "Lịch thăm sắp diễn ra trong vòng 24 giờ. Vui lòng liên hệ FPTU để được hỗ trợ hủy/thay đổi.",
                        VisitRequestErrorCodes.VisitCancelWindowExpired);
                }
            }

            if (visit.VisitScope == VisitScopes.MultiCampus)
            {
                bool hasStartedCampus = visit.CampusInstances.Any(c => 
                    c.Status == VisitInstanceStatus.DuringVisit || 
                    c.Status == VisitInstanceStatus.AfterVisit || 
                    c.Status == VisitInstanceStatus.Closed);
                
                if (hasStartedCampus)
                    throw new BusinessRuleException(
                        "Đơn liên cơ sở đã bắt đầu tại một số cơ sở. Vui lòng hủy từng cơ sở chưa diễn ra.",
                        VisitRequestErrorCodes.VisitAlreadyStartedCannotCancel);
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

        var now = _clock.VietnamNow;
        // planned_start_at is a LOCAL wall-clock DATETIME → time-window checks use VietnamNow
        // (UtcNow is 7h behind the wall clock and would let cancels through after the start).
        var vnNow = _clock.VietnamNow;

        // Pre-validate every target BEFORE any write, so a violation never reaches SaveChanges
        // (and the user never sees a raw EF/MySQL exception).
        foreach (var instance in targets)
        {
            if (instance.Status == VisitInstanceStatus.DuringVisit ||
                instance.Status == VisitInstanceStatus.AfterVisit ||
                instance.Status == VisitInstanceStatus.Closed)
                throw new BusinessRuleException(
                    "Cơ sở này đã bắt đầu hoặc đã hoàn tất tiếp khách nên không thể hủy.",
                    VisitRequestErrorCodes.VisitAlreadyStartedCannotCancel);

            if (!cancellableStatuses.Contains(instance.Status))
                throw new BusinessRuleException(
                    "Không thể hủy lịch thăm. Cơ sở đang ở trạng thái không thể hủy.");

            if (vnNow >= instance.PlannedStartAt)
                throw new BusinessRuleException(
                    actorType == CancellationActorType.Host
                        ? "Không thể hủy vì lịch tiếp khách đã bắt đầu hoặc đã diễn ra."
                        : "Không thể hủy lịch thăm. Đã đến hoặc quá thời gian bắt đầu.",
                    actorType == CancellationActorType.Host
                        ? VisitRequestErrorCodes.HostCannotCancelAfterVisitStarted
                        : VisitRequestErrorCodes.VisitAlreadyStartedCannotCancel);

            // Rule 24h (spec §2.3) applies to the VISITOR self-service flow only: the Host may
            // cancel any time BEFORE the start (external confirmation), but the Visitor must
            // cancel ≥ 24h in advance.
            if (actorType == CancellationActorType.Visitor
                && instance.PlannedStartAt < vnNow.AddHours(24))
                throw new BusinessRuleException(
                    "Lịch thăm sắp diễn ra trong vòng 24 giờ. Vui lòng liên hệ FPTU để được hỗ trợ hủy/thay đổi.",
                    VisitRequestErrorCodes.VisitCancelWindowExpired);
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
        // as-is with the instance CANCELLED (the list shows an instance-level cancellation).
        // REJECTED instances are terminal and count as "settled" for the rollup; still-pending
        // (WAITING_REQUEST_APPROVAL) instances are cascade-cancelled AFTER the request flips
        // (the campus trigger only allows a pending instance to cancel under a CANCELLED request).
        var whollyCancellable = request.VisitInstanceId is null && isVisitorOwner
            && visit.CampusInstances.All(c =>
                c.Status == VisitInstanceStatus.Cancelled
                || c.Status == VisitInstanceStatus.Rejected
                || c.Status == VisitInstanceStatus.WaitingRequestApproval);
        var requestRolledUp = whollyCancellable;
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

            // Cascade-cancel any instance still waiting for a campus decision.
            var pendingLeft = visit.CampusInstances
                .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval)
                .ToList();
            if (pendingLeft.Count > 0)
            {
                foreach (var instance in pendingLeft)
                {
                    instance.Status = VisitInstanceStatus.Cancelled;
                    instance.CancelledBy = actorId;
                    instance.CancelledAt = now;
                    instance.CancellationActorType = CancellationActorType.Visitor;
                    instance.CancellationSource = CancellationSource.SelfService;
                    instance.CancellationReason = reason;
                    instance.UpdatedAt = now;
                    instance.UpdatedBy = actorId;
                    instance.RowVersion += 1;
                    cancelled.Add(new CancelledCampusDto(instance.VisitInstanceId, instance.Status));
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        // --- Notifications for AFTER_APPROVAL cancellation ---
        var afterNotifs = new List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();
        var hoUsersToNotify = new List<ulong>();
        if (visit.VisitScope == VisitScopes.MultiCampus && isVisitorOwner)
        {
            hoUsersToNotify = await _db.Users
                .Where(u => u.Role.RoleCode == "HO" && u.Status == "ACTIVE")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);
        }

        // Notify every active Staff Leader of each target campus (not just the coordinator who
        // happened to approve it) — consistent with the PENDING_APPROVAL branch above, so a
        // campus with several/rotated Leaders all learn about the cancellation.
        var targetCampusIds = targets.Select(c => c.CampusId).Distinct().ToList();
        var staffLeadersByCampus = (await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == "LEADER"
                    && u.PrimaryCampusId.HasValue && targetCampusIds.Contains(u.PrimaryCampusId.Value)
                    && u.Status == "ACTIVE")
                .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(u => u.CampusId)
            .ToDictionary(g => g.Key, g => g.Select(u => u.UserId).ToList());

        foreach (var instance in targets)
        {
            var staffLeaderIds = staffLeadersByCampus.TryGetValue(instance.CampusId, out var leaders)
                ? leaders
                : new List<ulong>();
            var actionUrl = $"/dashboard/visit/process/{instance.VisitInstanceId}";
            if (isVisitorOwner)
            {
                if (instance.CurrentHostUserId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: instance.CurrentHostUserId.Value,
                        Title: "Lịch thăm quan bị hủy",
                        Message: $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        RelatedId: instance.VisitInstanceId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        VisitInstanceId: instance.VisitInstanceId,
                        CampusId: instance.CampusId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: actionUrl
                    ));
                }
                foreach (var staffLeaderId in staffLeaderIds)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: staffLeaderId,
                        Title: "Lịch thăm quan bị hủy",
                        Message: $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        RelatedId: instance.VisitInstanceId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        VisitInstanceId: instance.VisitInstanceId,
                        CampusId: instance.CampusId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: actionUrl
                    ));
                }
                foreach (var ho in hoUsersToNotify)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: ho,
                        Title: "Lịch thăm quan bị hủy",
                        Message: $"Khách đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        RelatedId: instance.VisitInstanceId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        VisitInstanceId: instance.VisitInstanceId,
                        CampusId: instance.CampusId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: actionUrl
                    ));
                }
            }
            else // Host cancelled
            {
                if (visit.VisitorUserId.HasValue)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: visit.VisitorUserId.Value,
                        Title: "Lịch thăm quan bị hủy",
                        Message: $"Host đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        RelatedId: instance.VisitInstanceId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        VisitInstanceId: instance.VisitInstanceId,
                        CampusId: instance.CampusId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                    ));
                }
                foreach (var staffLeaderId in staffLeaderIds)
                {
                    afterNotifs.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: staffLeaderId,
                        Title: "Lịch thăm quan bị hủy",
                        Message: $"Host đã hủy cơ sở {instance.CampusId} thuộc đơn {visit.RequestCode}.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitCancelled,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                        RelatedId: instance.VisitInstanceId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        VisitInstanceId: instance.VisitInstanceId,
                        CampusId: instance.CampusId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: actionUrl
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
