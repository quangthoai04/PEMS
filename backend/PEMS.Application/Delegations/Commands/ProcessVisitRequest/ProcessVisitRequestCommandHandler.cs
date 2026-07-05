using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed class ProcessVisitRequestCommandHandler
    : IRequestHandler<ProcessVisitRequestCommand, ProcessVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ProcessVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<ProcessVisitRequestResponse> Handle(
        ProcessVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only a Staff Leader processes/assigns hosts (also enforced by UC-22 RBAC).
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chỉ Staff Leader mới được duyệt/gán host.");

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Staff Leader may only act on their own campus.
        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("Cơ sở này không thuộc phạm vi phụ trách của bạn.");

        // Host được gán MỘT lần cho mỗi cơ sở (UC chốt). Khi đã có host thì không cho gán lại
        // (không có chức năng đổi/chuyển host trong phase này). Nếu sau này Host nghỉ/sai → tạo UC riêng.
        if (instance.CurrentHostUserId != null)
            throw new ConflictException("Cơ sở này đã có host phụ trách; không thể gán lại host.");

        // The chosen host must be an active STAFF of the same campus.
        var host = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.HostUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.HostUserId);

        // The DB triggers (trg_visit_campuses_assignment_validate_*) require the official host to be
        // a STAFF with sub_role = STAFF (not a Staff Leader) of the same campus. Mirror that here so a
        // bad HostUserId fails as a clean 422 instead of bubbling up the trigger SIGNAL as a 500.
        if (host.Role.RoleCode != RoleCodes.Staff
            || host.SubRole != UserSubRoles.Staff
            || host.PrimaryCampusId != instance.CampusId
            || host.Status != UserStatuses.Active)
        {
            throw new BusinessRuleException("Host được chọn phải là nhân sự (STAFF) đang hoạt động thuộc đúng cơ sở.");
        }

        var now = _clock.UtcNow;

        // The approve+assign writes touch two tables that have cross-row DB triggers: the
        // visit_request_campuses BEFORE UPDATE trigger reads visit_requests.status and rejects the
        // move to an operational status (ASSIGNED) unless the parent request is already APPROVED.
        // EF Core orders UPDATE statements by table name, so within a single SaveChanges the
        // campus row ("visit_request_campuses") is flushed BEFORE the request ("visit_requests"),
        // making the trigger see the still-PENDING parent → SIGNAL 45000 → generic 500.
        // Persist the approval FIRST, then the assignment, inside one transaction so the trigger
        // always observes APPROVED and the two writes still commit atomically.
        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        if (visit.VisitScope == VisitScopes.SingleCampus)
        {
            // Approve + assign in one step.
            if (visit.Status != VisitRequestStatuses.PendingApproval || instance.Status != VisitInstanceStatus.WaitingRequestApproval)
                throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");

            // Phase 1 — approve the request so the campus-instance trigger sees status = APPROVED.
            visit.Status = VisitRequestStatuses.Approved;
            visit.DecidedBy = actorId;
            visit.DecidedAt = now;
            visit.DecisionActorRole = DecisionActorRole.StaffLeader;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;
            await _db.SaveChangesAsync(cancellationToken);

            // Phase 2 — now flip the campus instance to ASSIGNED with the chosen host.
            instance.Status = VisitInstanceStatus.Assigned;
            instance.CurrentHostUserId = request.HostUserId;
            instance.HostAssignedBy = actorId;
            instance.HostAssignedAt = now;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }
        else // MULTI_CAMPUS — HO has already approved; Staff Leader assigns the actual staff.
        {
            if (visit.Status != VisitRequestStatuses.Approved || instance.Status != "WAITING_HOST_ASSIGNMENT")
                throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");
            // (One-time host guard already enforced above for both single & multi campus.)
            // Request is already APPROVED, so a single update is safe for the trigger.

            instance.Status = VisitInstanceStatus.Assigned;
            instance.CurrentHostUserId = request.HostUserId;
            instance.HostAssignedBy = actorId;
            instance.HostAssignedAt = now;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "APPROVE_AND_ASSIGN_HOST",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notifications (in-app only — không gửi email mời host) ---
        var notifications = new List<PEMS.Application.Notifications.Common.CreateNotificationItem>();

        if (visit.VisitScope == VisitScopes.SingleCampus)
        {
            if (visit.VisitorUserId.HasValue)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    visit.VisitorUserId.Value,
                    "Yêu cầu được phê duyệt",
                    $"Yêu cầu tham quan {visit.RequestCode} của bạn đã được phê duyệt.",
                    PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestApproved,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                    visit.VisitRequestId
                ));
            }
        }

        notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
            request.HostUserId,
            "Bạn được gán phụ trách đoàn khách",
            $"Bạn được phân công làm host cho đoàn tiếp khách {visit.DelegationName}. Vui lòng vào Setup đoàn khách để chuẩn bị.",
            PEMS.Application.Notifications.Common.NotificationTypes.HostAssigned,
            PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
            instance.VisitInstanceId
        ));

        if (notifications.Any())
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new ProcessVisitRequestResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            request.HostUserId,
            visit.VisitScope == VisitScopes.SingleCampus
                ? "Đã duyệt đơn và gán host phụ trách."
                : "Đã gán host phụ trách cho cơ sở.");
    }
}
