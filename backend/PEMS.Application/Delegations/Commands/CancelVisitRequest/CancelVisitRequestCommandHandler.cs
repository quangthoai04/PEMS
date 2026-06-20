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

    public CancelVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CancelVisitRequestResponse> Handle(
        CancelVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        // Admin must NOT cancel delegations (also enforced by the missing RBAC grant).
        if (roleCode == RoleCodes.Admin)
            throw new ForbiddenException("Admin không có quyền hủy đơn tham quan.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == actorId;
        var isHo = roleCode == RoleCodes.Ho;
        var isStaffLeader = roleCode == RoleCodes.Staff && subRole == SubRoles.Leader;

        // HO has read-only monitoring on single-campus (chốt 2026-06): HO may cancel only
        // MULTI_CAMPUS (handled below), never a single-campus request.
        if (isHo && visit.VisitScope == VisitScopes.SingleCampus)
            throw new BusinessRuleException(
                "HO chỉ được xem đơn một cơ sở ở chế độ theo dõi, không được xử lý nghiệp vụ trên đơn này.",
                "HO_SINGLE_CAMPUS_READ_ONLY");

        // Status rules:
        //  • Visitor may self-cancel (withdraw) their own request while PENDING or APPROVED.
        //  • Everyone else cancels only after approval (pre-approval is ended via reject).
        if (isVisitorOwner)
        {
            if (visit.Status != VisitRequestStatuses.PendingApproval && visit.Status != VisitRequestStatuses.Approved)
                throw new BusinessRuleException("Chỉ có thể hủy đơn đang chờ duyệt hoặc đã được duyệt.");
        }
        else
        {
            if (visit.Status != VisitRequestStatuses.Approved)
                throw new BusinessRuleException(visit.Status == VisitRequestStatuses.PendingApproval
                    ? "Đơn chưa được duyệt. Trước khi duyệt hãy dùng chức năng từ chối (reject), không phải hủy."
                    : "Chỉ có thể hủy đơn đã được duyệt.");
        }

        // A visitor withdrawing a pending request cancels its still-waiting instances too.
        var cancellableStatuses = isVisitorOwner
            ? new[] { VisitInstanceStatus.WaitingRequestApproval, VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit }
            : new[] { VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit };

        IReadOnlyList<VisitRequestCampus> targets;
        if (request.VisitInstanceId is { } instanceId)
        {
            var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == instanceId)
                ?? throw new NotFoundException("VisitRequestCampus", instanceId);
            targets = new[] { instance };
        }
        else
        {
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
        else if (isHo && visit.VisitScope == VisitScopes.MultiCampus)
        {
            actorType = CancellationActorType.Ho;
            source = CancellationSource.ExternalConfirmation;
        }
        else if (isStaffLeader && targets.All(t => t.CampusId == _currentUser.PrimaryCampusId))
        {
            actorType = CancellationActorType.StaffLeader;
            source = CancellationSource.ExternalConfirmation;
        }
        else if (targets.All(t => t.CurrentHostUserId == actorId))
        {
            // Current host cancels after the guest confirms via an external channel.
            actorType = CancellationActorType.Host;
            source = CancellationSource.ExternalConfirmation;
        }
        else
        {
            throw new ForbiddenException("Bạn không có quyền hủy lịch thăm này.");
        }

        var now = _clock.UtcNow;
        var enforceBeforeStart = actorType == CancellationActorType.Visitor || actorType == CancellationActorType.Host;

        var cancelled = new List<CancelledCampusDto>();
        foreach (var instance in targets)
        {
            if (!cancellableStatuses.Contains(instance.Status))
                throw new BusinessRuleException($"Không thể hủy cơ sở ở trạng thái '{instance.Status}'.");

            if (enforceBeforeStart && now >= instance.PlannedStartAt)
                throw new BusinessRuleException("Đã đến hoặc qua thời gian bắt đầu, không thể hủy.");

            var oldStatus = instance.Status;
            instance.Status = VisitInstanceStatus.Cancelled;
            instance.CancelledBy = actorId;
            instance.CancelledAt = now;
            instance.CancellationActorType = actorType;
            instance.CancellationSource = source;
            instance.CancellationReason = request.CancellationReason;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitInstanceId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.CampusInstance,
                OldStatus = oldStatus,
                NewStatus = VisitInstanceStatus.Cancelled,
                ChangedBy = actorId,
                Reason = request.CancellationReason,
                ChangedAt = now
            });

            cancelled.Add(new CancelledCampusDto(instance.VisitInstanceId, instance.Status));
        }

        if (cancelled.Count == 0)
            throw new BusinessRuleException("Không có cơ sở nào ở trạng thái có thể hủy.");

        // Single-campus, or all campuses now cancelled → the overall request becomes CANCELLED.
        var allCancelled = visit.CampusInstances.All(c => c.Status == VisitInstanceStatus.Cancelled);
        if (allCancelled)
        {
            var oldReqStatus = visit.Status;
            visit.Status = VisitRequestStatuses.Cancelled;
            visit.CancelledBy = actorId;
            visit.CancelledAt = now;
            visit.CancellationActorType = actorType;
            visit.CancellationSource = source;
            visit.CancellationReason = request.CancellationReason;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.Request,
                OldStatus = oldReqStatus,
                NewStatus = VisitRequestStatuses.Cancelled,
                ChangedBy = actorId,
                Reason = request.CancellationReason,
                ChangedAt = now
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "CANCEL_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            cancelled,
            allCancelled
                ? "Đơn tham quan đã được hủy."
                : "Cơ sở đã được hủy. Các cơ sở còn lại của đơn vẫn giữ nguyên.");
    }
}
