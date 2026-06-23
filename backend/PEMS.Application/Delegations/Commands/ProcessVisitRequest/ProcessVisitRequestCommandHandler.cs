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
        if (!(_currentUser.RoleCode == RoleCodes.Staff && _currentUser.SubRole == UserSubRoles.Leader))
            throw new ForbiddenException("Chá»‰ Staff Leader má»›i Ä‘Æ°á»£c duyá»‡t/gÃ¡n host.");

        var actorId = _currentUser.UserId.Value;

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == request.VisitInstanceId)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        // Staff Leader may only act on their own campus.
        if (_currentUser.PrimaryCampusId != instance.CampusId)
            throw new ForbiddenException("CÆ¡ sá»Ÿ nÃ y khÃ´ng thuá»™c pháº¡m vi phá»¥ trÃ¡ch cá»§a báº¡n.");

        // The chosen host must be an active STAFF of the same campus.
        var host = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.HostUserId, cancellationToken)
            ?? throw new NotFoundException("User", request.HostUserId);

        if (host.Role.RoleCode != RoleCodes.Staff
            || host.PrimaryCampusId != instance.CampusId
            || host.Status != UserStatuses.Active)
        {
            throw new BusinessRuleException("Host Ä‘Æ°á»£c chá»n pháº£i lÃ  nhÃ¢n sá»± (STAFF) Ä‘ang hoáº¡t Ä‘á»™ng thuá»™c Ä‘Ãºng cÆ¡ sá»Ÿ.");
        }

        var now = _clock.UtcNow;

        if (visit.VisitScope == VisitScopes.SingleCampus)
        {
            // Approve + assign in one step.
            if (visit.Status != VisitRequestStatuses.PendingApproval || instance.Status != VisitInstanceStatus.WaitingRequestApproval)
                throw new ConflictException("ÄÆ¡n Ä‘Ã£ Ä‘Æ°á»£c ngÆ°á»i khÃ¡c xá»­ lÃ½ hoáº·c tráº¡ng thÃ¡i Ä‘Ã£ thay Ä‘á»•i.");

            visit.Status = VisitRequestStatuses.Approved;
            visit.DecidedBy = actorId;
            visit.DecidedAt = now;
            visit.DecisionActorRole = DecisionActorRole.StaffLeader;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;



            var oldInstanceStatus = instance.Status;
            instance.Status = VisitInstanceStatus.Assigned;
            instance.CurrentHostUserId = request.HostUserId;
            instance.HostAssignedBy = actorId;
            instance.HostAssignedAt = now;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;
        }
        else // MULTI_CAMPUS â€” HO has already approved; Staff Leader assigns the actual staff.
        {
            if (visit.Status != VisitRequestStatuses.Approved || instance.Status != "WAITING_HOST_ASSIGNMENT")
                throw new ConflictException("Ä Æ¡n Ä‘Ã£ Ä‘Æ°á»£c ngÆ°á» i khÃ¡c xá»­ lÃ½ hoáº·c tráº¡ng thÃ¡i Ä‘Ã£ thay Ä‘á»•i.");

            if (instance.CurrentHostUserId != null)
                throw new ConflictException("Campus instance này đã có host chính thức, không thể thay đổi host.");

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

        return new ProcessVisitRequestResponse(
            visit.VisitRequestId,
            instance.VisitInstanceId,
            visit.Status,
            instance.Status,
            request.HostUserId,
            visit.VisitScope == VisitScopes.SingleCampus
                ? "Ä Ã£ duyá»‡t Ä‘Æ¡n vÃ  gÃ¡n host phá»¥ trÃ¡ch."
                : "Ä Ã£ gán host phá»¥ trÃ¡ch cho cÆ¡ sá»Ÿ.");
    }
}
