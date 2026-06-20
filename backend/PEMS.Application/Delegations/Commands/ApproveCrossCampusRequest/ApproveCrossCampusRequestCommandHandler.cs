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

    public ApproveCrossCampusRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
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

        // Interim host of each campus = that campus's IC head (Staff Leader).
        var campusIds = visit.CampusInstances.Select(c => c.CampusId).Distinct().ToList();
        var icHeads = await _db.Campuses
            .Where(c => campusIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.IcHeadUserId, cancellationToken);

        visit.Status = VisitRequestStatuses.Approved;
        visit.DecidedBy = actorId;
        visit.DecidedAt = now;
        visit.DecisionActorRole = DecisionActorRole.Ho;
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;

        _db.VisitStatusLogs.Add(new VisitStatusLog
        {
            VisitRequestId = visit.VisitRequestId,
            StatusOwnerType = StatusOwnerType.Request,
            OldStatus = VisitRequestStatuses.PendingApproval,
            NewStatus = VisitRequestStatuses.Approved,
            ChangedBy = actorId,
            ChangedAt = now
        });

        var assigned = new List<AssignedCampusDto>();
        foreach (var inst in visit.CampusInstances.Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval))
        {
            icHeads.TryGetValue(inst.CampusId, out var icHead);

            var oldStatus = inst.Status;
            inst.Status = VisitInstanceStatus.Assigned;
            inst.CurrentHostUserId = icHead; // interim host; SL hands off to a real staff via UC-22
            inst.HostAssignedBy = actorId;
            inst.HostAssignedAt = now;
            inst.HostAssignmentSource = HostAssignmentSource.AutoStaffLeader;
            inst.UpdatedAt = now;
            inst.UpdatedBy = actorId;
            inst.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitInstanceId = inst.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.CampusInstance,
                OldStatus = oldStatus,
                NewStatus = VisitInstanceStatus.Assigned,
                ChangedBy = actorId,
                ChangedAt = now
            });

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

        return new ApproveCrossCampusRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            assigned,
            "Đã duyệt đơn liên cơ sở. Mỗi cơ sở được tạm gán host là Trưởng IC; Staff Leader có thể chuyển host cho nhân sự phụ trách.");
    }
}
