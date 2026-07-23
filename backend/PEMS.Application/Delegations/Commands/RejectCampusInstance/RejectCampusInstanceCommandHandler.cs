using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
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
    private readonly IVisitFormReadService _formReadService;

    public RejectCampusInstanceCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        IVisitRequestAggregateStatusService aggregateStatus,
        PEMS.Application.Notifications.Common.INotificationService notificationService,
        IVisitFormReadService formReadService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _aggregateStatus = aggregateStatus;
        _notificationService = notificationService;
        _formReadService = formReadService;
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

        var now = _clock.VietnamNow;

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
        var previousVisitStatus = visit.Status;
        _aggregateStatus.Apply(visit);
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        // Same per-campus audit context as the approve decision — see CampusDecisionAudit. The free-text
        // reason is NOT copied here: it already lives on the instance, and this column is a short
        // structured summary, not a second copy of staff-authored prose.
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "REJECT_CAMPUS_INSTANCE",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CampusId = instance.CampusId,
            VisitRequestId = visit.VisitRequestId,
            VisitInstanceId = instance.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED",
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notification: the Visitor sees which campus declined and why ---
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? $"#{instance.CampusId}";

        // This command acts on ONE campus instance, so the delegation name comes from THAT instance's own
        // detail — never a sibling's, and never a request-level value, which does not exist. A missing
        // detail fails loudly rather than going blank.
        var formContent = await _formReadService.ResolveCampusFormContentAsync(
            visit, new[] { instance.VisitInstanceId }, cancellationToken);
        var delegationName = formContent[instance.VisitInstanceId].DelegationName;

        var notifications = new System.Collections.Generic.List<PEMS.Application.Notifications.Common.CreateNotificationRequest>();

        if (visit.VisitorUserId.HasValue)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: visit.VisitorUserId.Value,
                Title: "Cơ sở từ chối tiếp nhận",
                Message: $"Cơ sở {campusName} đã từ chối tiếp nhận yêu cầu tham quan {visit.RequestCode}. Lý do: {reason}",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestRejected,
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

        // G8/G9/G10 (spec §5 HO): visibility on multi-campus per-campus decisions + aggregate
        // status transitions — same logic as ApproveCampusInstanceCommandHandler.
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            var hoUsers = await _db.Users
                .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == "ACTIVE")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                RecipientUserId: id,
                Title: "Cơ sở đã từ chối đơn liên cơ sở",
                Message: $"Cơ sở {campusName} đã từ chối đơn {visit.RequestCode} ({delegationName}). Lý do: {reason}",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                RelatedId: visit.VisitRequestId,
                ActorUserId: actorId,
                Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                VisitRequestId: visit.VisitRequestId,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit/process/{instance.VisitInstanceId}"
            )));

            if (previousVisitStatus != visit.Status)
            {
                if (visit.Status == VisitRequestStatuses.PartiallyApproved)
                {
                    notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: "Đơn liên cơ sở được duyệt một phần",
                        Message: $"Đơn {visit.RequestCode} ({delegationName}) hiện đã được duyệt một phần.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                        RelatedId: visit.VisitRequestId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                    )));
                }
                else if (visit.Status == VisitRequestStatuses.Approved || visit.Status == VisitRequestStatuses.Rejected)
                {
                    notifications.AddRange(hoUsers.Select(id => new PEMS.Application.Notifications.Common.CreateNotificationRequest(
                        RecipientUserId: id,
                        Title: "Đơn liên cơ sở đã xử lý xong",
                        Message: $"Tất cả cơ sở của đơn {visit.RequestCode} ({delegationName}) đã xử lý xong.",
                        NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitStatusChanged,
                        RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                        RelatedId: visit.VisitRequestId,
                        ActorUserId: actorId,
                        Category: PEMS.Application.Notifications.Common.NotificationCategories.Visit,
                        VisitRequestId: visit.VisitRequestId,
                        ActionType: PEMS.Application.Notifications.Common.NotificationActionTypes.OpenVisitDetail,
                        ActionUrl: $"/dashboard/visit?visitRequestId={visit.VisitRequestId}"
                    )));
                }
            }
        }

        if (notifications.Count > 0)
            await _notificationService.CreateManyAsync(notifications, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return new RejectCampusInstanceResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            "Đã từ chối tiếp nhận tại cơ sở này.");
    }
}
