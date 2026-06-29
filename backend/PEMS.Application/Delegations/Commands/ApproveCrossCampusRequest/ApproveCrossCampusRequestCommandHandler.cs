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

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

public sealed class ApproveCrossCampusRequestCommandHandler
    : IRequestHandler<ApproveCrossCampusRequestCommand, ApproveCrossCampusRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly PEMS.Application.Notifications.Common.INotificationService _notificationService;

    public ApproveCrossCampusRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock, PEMS.Application.Notifications.Common.INotificationService notificationService)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _notificationService = notificationService;
    }

    public async Task<ApproveCrossCampusRequestResponse> Handle(
        ApproveCrossCampusRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only HO approves cross-campus requests (also enforced by UC-18 RBAC).
        if (_currentUser.RoleCode != RoleCodes.Ho)
            throw new ForbiddenException("Chỉ HO mới được duyệt đơn liên cơ sở.");

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        // HO may only DECIDE multi-campus. Single-campus is read-only monitoring for HO
        // (chốt 2026-06) — surfaced with a machine-readable code the frontend can branch on.
        if (visit.VisitScope != VisitScopes.MultiCampus)
            throw new BusinessRuleException(
                "HO chỉ được xem đơn một cơ sở ở chế độ theo dõi, không được xử lý nghiệp vụ trên đơn này.",
                "HO_SINGLE_CAMPUS_READ_ONLY");

        // Status guard = optimistic concurrency protection (see CancelVisitRequestCommandHandler).
        if (visit.Status != VisitRequestStatuses.PendingApproval)
            throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");

        var now = _clock.UtcNow;

        // Interim coordinator of each campus = that campus's IC head (Staff Leader). The
        // visit_request_campuses BEFORE-UPDATE trigger requires coordinator_user_id to be an
        // ACTIVE Staff Leader (STAFF + sub_role LEADER) of the SAME campus, so a NULL or invalid
        // ic_head_user_id would otherwise blow up as a raw DB trigger SIGNAL → generic 500.
        // Pre-validate every relevant campus HERE (before any SaveChanges) and surface a clean
        // business error (422) the frontend can show, naming the campus that needs configuration.
        var relevantCampusIds = visit.CampusInstances
            .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval)
            .Select(c => c.CampusId).Distinct().ToList();

        var campusRows = await _db.Campuses
            .Where(c => relevantCampusIds.Contains(c.CampusId))
            .Select(c => new { c.CampusId, c.Name, c.IcHeadUserId })
            .ToListAsync(cancellationToken);
        var campusById = campusRows.ToDictionary(c => c.CampusId);

        var icHeadIds = campusRows.Where(c => c.IcHeadUserId.HasValue)
            .Select(c => c.IcHeadUserId!.Value).Distinct().ToList();
        var icHeadUsers = icHeadIds.Count == 0
            ? new Dictionary<ulong, User>()
            : await _db.Users.Include(u => u.Role)
                .Where(u => icHeadIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u, cancellationToken);

        foreach (var campusId in relevantCampusIds)
        {
            campusById.TryGetValue(campusId, out var campus);
            var campusName = campus?.Name ?? $"#{campusId}";

            if (campus?.IcHeadUserId is not { } icHeadId)
                throw new BusinessRuleException(
                    $"Không thể duyệt đơn liên cơ sở vì cơ sở {campusName} chưa có IC Head/Staff Leader phụ trách. Vui lòng cấu hình trước khi duyệt.",
                    "CAMPUS_IC_HEAD_MISSING");

            if (!icHeadUsers.TryGetValue(icHeadId, out var icHeadUser))
                throw new BusinessRuleException(
                    $"Không thể duyệt đơn liên cơ sở vì IC Head của cơ sở {campusName} không tồn tại. Vui lòng cấu hình lại.",
                    "CAMPUS_IC_HEAD_INVALID");

            if (icHeadUser.Status != UserStatuses.Active)
                throw new BusinessRuleException(
                    $"Không thể duyệt đơn liên cơ sở vì IC Head của cơ sở {campusName} đang bị khóa/ngừng hoạt động. Vui lòng cấu hình lại.",
                    "CAMPUS_IC_HEAD_INACTIVE");

            if (icHeadUser.Role.RoleCode != RoleCodes.Staff
                || icHeadUser.SubRole != UserSubRoles.Leader
                || icHeadUser.PrimaryCampusId != campusId)
                throw new BusinessRuleException(
                    $"Không thể duyệt đơn liên cơ sở vì IC Head của cơ sở {campusName} không phải Staff Leader hợp lệ của cơ sở. Vui lòng cấu hình lại.",
                    "CAMPUS_IC_HEAD_NOT_STAFF_LEADER");
        }

        var icHeads = campusRows.ToDictionary(c => c.CampusId, c => c.IcHeadUserId);

        // Same trigger-ordering hazard as ProcessVisitRequest: the visit_request_campuses BEFORE
        // UPDATE trigger requires the parent request to already be APPROVED before an instance may
        // move to WAITING_HOST_ASSIGNMENT. EF flushes the campus rows before visit_requests (table
        // name ordering), so persist the approval FIRST (phase 1), then the instances (phase 2),
        // inside one transaction.
        await using var tx = await _db.BeginTransactionAsync(cancellationToken);

        visit.Status = VisitRequestStatuses.Approved;
        visit.DecidedBy = actorId;
        visit.DecidedAt = now;
        visit.DecisionActorRole = DecisionActorRole.Ho;
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;
        await _db.SaveChangesAsync(cancellationToken);

        var assigned = new List<AssignedCampusDto>();
        foreach (var inst in visit.CampusInstances.Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval))
        {
            icHeads.TryGetValue(inst.CampusId, out var icHead);

            var oldStatus = inst.Status;
            inst.Status = "WAITING_HOST_ASSIGNMENT";
            inst.CoordinatorUserId = icHead; 
            inst.CoordinatorAssignedBy = actorId;
            inst.CoordinatorAssignedAt = now;
            inst.CurrentHostUserId = null;
            inst.HostAssignedBy = null;
            inst.HostAssignedAt = null;
            inst.UpdatedAt = now;
            inst.UpdatedBy = actorId;
            inst.RowVersion += 1;

            assigned.Add(new AssignedCampusDto(inst.VisitInstanceId, inst.CampusId, icHead, inst.Status));
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "HO_APPROVE_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        // --- Notifications ---
        var notifications = new List<PEMS.Application.Notifications.Common.CreateNotificationItem>();
        if (visit.VisitorUserId.HasValue)
        {
            notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                visit.VisitorUserId.Value,
                "Yêu cầu được phê duyệt",
                $"Yêu cầu liên cơ sở {visit.RequestCode} của bạn đã được HO phê duyệt.",
                PEMS.Application.Notifications.Common.NotificationTypes.VisitRequestApproved,
                PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitRequest,
                visit.VisitRequestId
            ));
        }

        foreach (var inst in visit.CampusInstances.Where(c => c.Status == "WAITING_HOST_ASSIGNMENT"))
        {
            if (inst.CoordinatorUserId.HasValue)
            {
                notifications.Add(new PEMS.Application.Notifications.Common.CreateNotificationItem(
                    inst.CoordinatorUserId.Value,
                    "Cần phân công Host",
                    $"Đoàn {visit.DelegationName} vừa được HO duyệt. Vui lòng phân công Host phụ trách cho cơ sở của bạn.",
                    PEMS.Application.Notifications.Common.NotificationTypes.WaitingHostAssignment,
                    PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                    inst.VisitInstanceId
                ));
            }
        }

        if (notifications.Any())
        {
            await _notificationService.CreateManyAsync(notifications, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new ApproveCrossCampusRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            assigned,
            "Đã duyệt đơn liên cơ sở. Mỗi cơ sở được tạm gán host là Trưởng IC; Staff Leader có thể chuyển host cho nhân sự phụ trách.");
    }
}
