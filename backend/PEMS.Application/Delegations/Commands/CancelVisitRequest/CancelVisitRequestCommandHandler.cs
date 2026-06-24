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

        // Cancellation is a POST-APPROVAL action only. A request that is still PENDING_APPROVAL
        // must be ended via the reject flow — the visit_request_campuses trigger likewise blocks
        // cancelling an instance whose owning request is not APPROVED. We pre-validate and return
        // a clean Vietnamese business error HERE, before any SaveChanges, so the user never sees a
        // raw EF/MySQL trigger exception.
        if (visit.Status == VisitRequestStatuses.PendingApproval)
            throw new BusinessRuleException(
                "Không thể hủy lịch thăm. Đơn đang chờ duyệt nên chưa thể hủy theo luồng hiện tại.");

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

        // If every campus instance is now cancelled, the overall request becomes CANCELLED.
        var allCancelled = visit.CampusInstances.All(c => c.Status == VisitInstanceStatus.Cancelled);
        if (allCancelled)
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

        await tx.CommitAsync(cancellationToken);

        return new CancelVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            cancelled,
            allCancelled
                ? "Đơn tham quan đã được hủy."
                : "Cơ sở đã được hủy. Các cơ sở còn lại của đơn vẫn giữ nguyên.");
    }
}
