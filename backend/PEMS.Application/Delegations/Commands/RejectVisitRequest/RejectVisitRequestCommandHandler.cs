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

namespace PEMS.Application.Delegations.Commands.RejectVisitRequest;

public sealed class RejectVisitRequestCommandHandler
    : IRequestHandler<RejectVisitRequestCommand, RejectVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public RejectVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<RejectVisitRequestResponse> Handle(
        RejectVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;
        var isHo = roleCode == RoleCodes.Ho;
        var isStaffLeader = roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader;

        if (!isHo && !isStaffLeader)
            throw new ForbiddenException("Bạn không có quyền từ chối đơn tham quan.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        // HO has read-only monitoring on single-campus (chốt 2026-06) — never processing.
        if (isHo && visit.VisitScope == VisitScopes.SingleCampus)
            throw new BusinessRuleException(
                "HO chỉ được xem đơn một cơ sở ở chế độ theo dõi, không được xử lý nghiệp vụ trên đơn này.",
                "HO_SINGLE_CAMPUS_READ_ONLY");

        // Reject is a decision-stage action only.
        if (visit.Status != VisitRequestStatuses.PendingApproval)
            throw new ConflictException("Đơn đã được người khác xử lý hoặc trạng thái đã thay đổi.");

        string actorRole;
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            if (!isHo)
                throw new ForbiddenException("Đơn liên cơ sở chỉ do HO từ chối.");
            actorRole = DecisionActorRole.Ho;
        }
        else
        {
            if (!isStaffLeader)
                throw new ForbiddenException("Đơn một cơ sở chỉ do Staff Leader của cơ sở từ chối.");

            var instance = visit.CampusInstances.FirstOrDefault();
            if (instance is null || _currentUser.PrimaryCampusId != instance.CampusId)
                throw new ForbiddenException("Đơn không thuộc cơ sở bạn phụ trách.");

            actorRole = DecisionActorRole.StaffLeader;
        }

        var now = _clock.UtcNow;
        visit.Status = VisitRequestStatuses.Rejected;
        visit.DecisionNote = request.Reason;
        visit.DecidedBy = actorId;
        visit.DecidedAt = now;
        visit.DecisionActorRole = actorRole;
        visit.UpdatedAt = now;
        visit.UpdatedBy = actorId;
        visit.RowVersion += 1;



        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "REJECT_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new RejectVisitRequestResponse(visit.VisitRequestId, visit.Status, "Đã từ chối đơn tham quan.");
    }
}
