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

    public ProcessVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ProcessVisitRequestResponse> Handle(
        ProcessVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        // Only a Staff Leader processes/assigns hosts (also enforced by UC-22 RBAC).
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == SubRoles.Leader))
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

        // The chosen host must be an active STAFF of the same campus.
        var host = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.HostUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.HostUserId);

        if (host.Role.RoleCode != RoleCodes.Staff
            || host.PrimaryCampusId != instance.CampusId
            || host.Status != UserStatuses.Active)
        {
            throw new BusinessRuleException("Host được chọn phải là nhân sự (STAFF) đang hoạt động thuộc đúng cơ sở.");
        }

        var now = _clock.UtcNow;

        if (visit.VisitScope == VisitScopes.SingleCampus)
        {
            // Approve + assign in one step.
            if (visit.Status != VisitRequestStatuses.PendingApproval || instance.Status != VisitInstanceStatus.WaitingRequestApproval)
                throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");

            visit.Status = VisitRequestStatuses.Approved;
            visit.DecidedBy = actorId;
            visit.DecidedAt = now;
            visit.DecisionActorRole = DecisionActorRole.StaffLeader;
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

            var oldInstanceStatus = instance.Status;
            instance.Status = VisitInstanceStatus.Assigned;
            instance.CurrentHostUserId = request.HostUserId;
            instance.HostAssignedBy = actorId;
            instance.HostAssignedAt = now;
            instance.HostAssignmentSource = HostAssignmentSource.ManualApproval;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitInstanceId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.CampusInstance,
                OldStatus = oldInstanceStatus,
                NewStatus = VisitInstanceStatus.Assigned,
                ChangedBy = actorId,
                ChangedAt = now
            });
        }
        else // MULTI_CAMPUS — HO has already approved; hand the interim host off to a real staff.
        {
            if (visit.Status != VisitRequestStatuses.Approved || instance.Status != VisitInstanceStatus.Assigned)
                throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");

            instance.CurrentHostUserId = request.HostUserId;
            instance.HostTransferredBy = actorId;
            instance.HostTransferredAt = now;
            instance.HostAssignmentSource = HostAssignmentSource.Transferred;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = visit.VisitScope == VisitScopes.SingleCampus ? "APPROVE_AND_ASSIGN_HOST" : "TRANSFER_HOST",
            EntityType = "VisitRequestCampus",
            EntityId = instance.VisitInstanceId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ProcessVisitRequestResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            request.HostUserId,
            visit.VisitScope == VisitScopes.SingleCampus
                ? "Đã duyệt đơn và gán host phụ trách."
                : "Đã chuyển host phụ trách cho cơ sở.");
    }
}
