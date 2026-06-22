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
            throw new ForbiddenException("Báº¡n khÃ´ng cÃ³ quyá»n tá»« chá»‘i Ä‘Æ¡n tham quan.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        // HO has read-only monitoring on single-campus (chá»‘t 2026-06) â€” never processing.
        if (isHo && visit.VisitScope == VisitScopes.SingleCampus)
            throw new BusinessRuleException(
                "HO chá»‰ Ä‘Æ°á»£c xem Ä‘Æ¡n má»™t cÆ¡ sá»Ÿ á»Ÿ cháº¿ Ä‘á»™ theo dÃµi, khÃ´ng Ä‘Æ°á»£c xá»­ lÃ½ nghiá»‡p vá»¥ trÃªn Ä‘Æ¡n nÃ y.",
                "HO_SINGLE_CAMPUS_READ_ONLY");

        // Reject is a decision-stage action only.
        if (visit.Status != VisitRequestStatuses.PendingApproval)
            throw new ConflictException("ÄÆ¡n Ä‘Ã£ Ä‘Æ°á»£c ngÆ°á»i khÃ¡c xá»­ lÃ½ hoáº·c tráº¡ng thÃ¡i Ä‘Ã£ thay Ä‘á»•i.");

        string actorRole;
        if (visit.VisitScope == VisitScopes.MultiCampus)
        {
            if (!isHo)
                throw new ForbiddenException("ÄÆ¡n liÃªn cÆ¡ sá»Ÿ chá»‰ do HO tá»« chá»‘i.");
            actorRole = DecisionActorRole.Ho;
        }
        else
        {
            if (!isStaffLeader)
                throw new ForbiddenException("ÄÆ¡n má»™t cÆ¡ sá»Ÿ chá»‰ do Staff Leader cá»§a cÆ¡ sá»Ÿ tá»« chá»‘i.");

            var instance = visit.CampusInstances.FirstOrDefault();
            if (instance is null || _currentUser.PrimaryCampusId != instance.CampusId)
                throw new ForbiddenException("ÄÆ¡n khÃ´ng thuá»™c cÆ¡ sá»Ÿ báº¡n phá»¥ trÃ¡ch.");

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

        _db.VisitStatusLogs.Add(new VisitStatusLog
        {
            VisitRequestId = visit.VisitRequestId,
            StatusOwnerType = StatusOwnerType.Request,
            OldStatus = VisitRequestStatuses.PendingApproval,
            NewStatus = VisitRequestStatuses.Rejected,
            ChangedBy = actorId,
            Reason = request.Reason,
            ChangedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "REJECT_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new RejectVisitRequestResponse(visit.VisitRequestId, visit.Status, "ÄÃ£ tá»« chá»‘i Ä‘Æ¡n tham quan.");
    }
}
