using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.RejectCampusInstance;

public sealed class RejectCampusInstanceCommandHandler
    : IRequestHandler<RejectCampusInstanceCommand, RejectCampusInstanceResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IVisitRequestAggregateStatusService _aggregateStatus;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public RejectCampusInstanceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestAggregateStatusService aggregateStatus,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _aggregateStatus = aggregateStatus;
        _notificationService = notificationService;
    }

    public async Task<RejectCampusInstanceResponse> Handle(
        RejectCampusInstanceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only the campus Staff Leader rejects their own campus instance (HO never decides).
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader của cơ sở mới được từ chối cơ sở này.");

        var actorId = _currentUser.UserId.Value;

        // Reason is mandatory (also enforced by the validator; re-checked for internal callers —
        // the DB trigger requires a non-empty decision_note on REJECTED as well).
        var reason = request.DecisionNote?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new BusinessRuleException("Vui lòng nhập lý do từ chối.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        if (visit.Status == VisitRequestStatuses.Cancelled)
            throw new ConflictException("Đơn đã bị hủy nên không thể từ chối.");

        if (instance.Status != VisitInstanceStatus.WaitingRequestApproval)
            throw new ConflictException("Cơ sở này đã được xử lý hoặc trạng thái đã thay đổi.");

        var now = _clock.UtcNow;

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        // Reject records the decision on the instance only — never a host, never participants,
        // never logistics/minutes/calendar.
        instance.Status = VisitInstanceStatus.Rejected;
        instance.DecidedBy = actorId;
        instance.DecidedAt = now;
        instance.DecisionActorRole = DecisionActorRole.StaffLeader;
        instance.DecisionNote = reason;
        instance.UpdatedAt = now;
        instance.UpdatedBy = actorId;
        instance.RowVersion += 1;

        // Rejecting ONE campus never rejects the whole request while other campuses are still
        // pending/assigned — the aggregate service (mirroring the DB trigger) decides the total.
        _aggregateStatus.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "REJECT_CAMPUS_INSTANCE",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notification: the Visitor sees which campus declined and why ---
        if (visit.VisitorUserId.HasValue)
        {
            var campusName = await _db.Campuses
                .Where(c => c.CampusId == instance.CampusId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

            await _notificationService.CreateAsync(
                visit.VisitorUserId.Value,
                "Cơ sở từ chối tiếp nhận",
                $"Cơ sở {campusName} đã từ chối tiếp nhận yêu cầu tham quan {visit.RequestCode}. Lý do: {reason}",
                PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestRejected,
                PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                instance.VisitInstanceId,
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new RejectCampusInstanceResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            "Đã từ chối tiếp nhận tại cơ sở này.");
    }
}
