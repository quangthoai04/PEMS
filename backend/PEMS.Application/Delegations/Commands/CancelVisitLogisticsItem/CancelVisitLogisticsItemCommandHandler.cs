using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Commands.PrepareVisitLogistics;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Commands.CancelVisitLogisticsItem;

public sealed class CancelVisitLogisticsItemCommandHandler
    : IRequestHandler<CancelVisitLogisticsItemCommand, CancelVisitLogisticsItemResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CancelVisitLogisticsItemCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CancelVisitLogisticsItemResponse> Handle(
        CancelVisitLogisticsItemCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;

        // A cancellation must carry a reason — it is the decision_note (lý do hủy) of the item.
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("Vui lòng nhập lý do hủy yêu cầu logistics.");
        if (reason.Length > 1000)
            throw new BusinessRuleException("Lý do hủy không được vượt quá 1000 ký tự.");

        var instance = await _db.VisitRequestCampuses
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (instance.CurrentHostUserId != actorId)
            throw new ForbiddenException("Chỉ Host phụ trách cơ sở này mới được hủy yêu cầu hậu cần.");
        if (instance.Status != VisitInstanceStatus.Assigned && instance.Status != VisitInstanceStatus.BeforeVisit)
            throw new ConflictException("Chỉ có thể hủy yêu cầu hậu cần trong giai đoạn chuẩn bị.");

        var item = await _db.VisitLogisticsItems
            .FirstOrDefaultAsync(l => l.LogisticsItemId == request.LogisticsItemId
                                      && l.VisitInstanceId == instance.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitLogisticsItem", request.LogisticsItemId);

        // Idempotent: already cancelled → no-op.
        if (item.Status == LogisticsItemStatus.Cancelled)
            return new CancelVisitLogisticsItemResponse(true, item.LogisticsItemId, item.Status, "Yêu cầu đã được hủy trước đó.");

        // Locked once the department has taken it (assigning/working) or completed a SYSTEM request.
        // Offline-coordinated records are the host's own trace (status DONE) → still cancellable.
        var systemDone = item.Status == LogisticsItemStatus.Done
                         && item.CoordinationMode != LogisticsCoordinationModes.OfflineCoordinated;
        if (item.Status == LogisticsItemStatus.Assigned
            || item.Status == LogisticsItemStatus.Accepted
            || item.Status == LogisticsItemStatus.InProgress
            || systemDone)
            throw new ConflictException("Yêu cầu đã được phòng ban tiếp nhận/đang xử lý nên không thể hủy.");

        var now = _clock.UtcNow;
        item.Status = LogisticsItemStatus.Cancelled;
        item.DecisionNote = reason;   // lý do hủy (decision_note) — required, validated above
        item.UpdatedAt = now;
        item.UpdatedBy = actorId;
        item.RowVersion += 1;   // bump the optimistic-concurrency token on every state change

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = instance.CampusId,
            Action = "CANCEL_VISIT_LOGISTICS_ITEM",
            EntityType = "VisitLogisticsItem",
            EntityId = item.LogisticsItemId,
            CreatedAt = now,
        });

        await PEMS.Application.EmailActions.EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(
            _db, EmailActionTargetTypes.LogisticsItem, item.LogisticsItemId, "Yêu cầu hậu cần này đã bị hủy.", now, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelVisitLogisticsItemResponse(true, item.LogisticsItemId, item.Status, "Đã hủy yêu cầu hậu cần.");
    }
}
