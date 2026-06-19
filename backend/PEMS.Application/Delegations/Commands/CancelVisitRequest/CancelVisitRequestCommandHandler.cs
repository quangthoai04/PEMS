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

    // A request may only be cancelled while its campus instances are still ASSIGNED or
    // BEFORE_VISIT — never once the visit is DURING_VISIT / AFTER_VISIT / CLOSED.
    private static readonly string[] CancellableCampusStatuses =
        { VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit };

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

        // Cancellation is a post-approval action only. PENDING → reject flow; REJECTED/CANCELLED → invalid.
        if (visit.Status != VisitRequestStatuses.Approved)
            throw new BusinessRuleException(visit.Status == VisitRequestStatuses.PendingApproval
                ? "Đơn chưa được duyệt. Trước khi duyệt hãy dùng chức năng từ chối (reject), không phải hủy."
                : "Chỉ có thể hủy đơn đã được duyệt.");

        var isVisitor = visit.VisitorUserId == actorId;

        // Visitor may only cancel their OWN request.
        if (roleCode == RoleCodes.Visitor && !isVisitor)
            throw new ForbiddenException("Bạn chỉ có thể hủy đơn của chính mình.");

        var now = _clock.UtcNow;
        var actorType = ResolveActorType(roleCode, subRole, isVisitor);
        var source = isVisitor ? CancellationSource.SelfService : CancellationSource.ExternalConfirmation;

        // Determine the campus instances to cancel.
        IReadOnlyList<VisitRequestCampus> targets;
        if (request.VisitInstanceId is { } instanceId)
        {
            var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == instanceId)
                ?? throw new NotFoundException("VisitRequestCampus", instanceId);

            // Host (STAFF/Staff) may only cancel a campus instance they currently host.
            if (!isVisitor && roleCode == RoleCodes.Staff && subRole == SubRoles.Staff
                && instance.CurrentHostUserId != actorId)
                throw new ForbiddenException("Bạn chỉ có thể hủy cơ sở mà bạn đang là host.");

            targets = new[] { instance };
        }
        else
        {
            targets = visit.CampusInstances
                .Where(c => CancellableCampusStatuses.Contains(c.Status))
                .ToList();
        }

        var cancelled = new List<CancelledCampusDto>();
        foreach (var instance in targets)
        {
            if (!CancellableCampusStatuses.Contains(instance.Status))
                throw new BusinessRuleException($"Không thể hủy cơ sở ở trạng thái '{instance.Status}'.");

            var oldStatus = instance.Status;
            instance.Status = VisitInstanceStatus.Cancelled;
            instance.CancelledBy = actorId;
            instance.CancelledAt = now;
            instance.CancellationActorType = actorType;
            instance.CancellationSource = source;
            instance.CancellationReason = request.CancellationReason;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;

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
            throw new BusinessRuleException("Không có cơ sở nào ở trạng thái có thể hủy (ASSIGNED/BEFORE_VISIT).");

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

    private static string ResolveActorType(string? roleCode, string? subRole, bool isVisitor)
    {
        if (isVisitor) return CancellationActorType.Visitor;
        if (roleCode == RoleCodes.Ho) return CancellationActorType.Ho;
        if (roleCode == RoleCodes.Staff && subRole == SubRoles.Leader) return CancellationActorType.StaffLeader;
        // STAFF/Staff acting on the campus they host, or any other on-behalf cancel.
        return CancellationActorType.Host;
    }
}
