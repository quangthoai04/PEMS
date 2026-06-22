using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.CancelVisitRequest;

public sealed class CancelVisitRequestCommandHandler
    : IRequestHandler<CancelVisitRequestCommand, CancelVisitRequestResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CancelVisitRequestCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CancelVisitRequestResponse> Handle(
        CancelVisitRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var actorId = _currentUser.UserId.Value;
        var roleCode = _currentUser.RoleCode;
        var subRole = _currentUser.SubRole;

        // Admin must NOT cancel delegations (also enforced by the missing RBAC grant).
        if (roleCode == RoleCodes.Admin)
            throw new ForbiddenException("Admin khÃ´ng cÃ³ quyá»n há»§y Ä‘Æ¡n tham quan.");

        var visit = await _db.VisitRequests
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", request.VisitRequestId);

        var isVisitorOwner = roleCode == RoleCodes.Visitor && visit.VisitorUserId == actorId;
        var isHo = roleCode == RoleCodes.Ho;
        var isStaffLeader = roleCode == RoleCodes.Staff && subRole == UserSubRoles.Leader;

        // HO has read-only monitoring on single-campus (chá»‘t 2026-06): HO may cancel only
        // MULTI_CAMPUS (handled below), never a single-campus request.
        if (isHo && visit.VisitScope == VisitScopes.SingleCampus)
            throw new BusinessRuleException(
                "HO chá»‰ Ä‘Æ°á»£c xem Ä‘Æ¡n má»™t cÆ¡ sá»Ÿ á»Ÿ cháº¿ Ä‘á»™ theo dÃµi, khÃ´ng Ä‘Æ°á»£c xá»­ lÃ½ nghiá»‡p vá»¥ trÃªn Ä‘Æ¡n nÃ y.",
                "HO_SINGLE_CAMPUS_READ_ONLY");

        // Status rules:
        //  â€¢ Visitor may self-cancel (withdraw) their own request while PENDING or APPROVED.
        //  â€¢ Everyone else cancels only after approval (pre-approval is ended via reject).
        if (isVisitorOwner)
        {
            if (visit.Status != VisitRequestStatuses.PendingApproval && visit.Status != VisitRequestStatuses.Approved)
                throw new BusinessRuleException("Chá»‰ cÃ³ thá»ƒ há»§y Ä‘Æ¡n Ä‘ang chá» duyá»‡t hoáº·c Ä‘Ã£ Ä‘Æ°á»£c duyá»‡t.");
        }
        else
        {
            if (visit.Status != VisitRequestStatuses.Approved)
                throw new BusinessRuleException(visit.Status == VisitRequestStatuses.PendingApproval
                    ? "ÄÆ¡n chÆ°a Ä‘Æ°á»£c duyá»‡t. TrÆ°á»›c khi duyá»‡t hÃ£y dÃ¹ng chá»©c nÄƒng tá»« chá»‘i (reject), khÃ´ng pháº£i há»§y."
                    : "Chá»‰ cÃ³ thá»ƒ há»§y Ä‘Æ¡n Ä‘Ã£ Ä‘Æ°á»£c duyá»‡t.");
        }

        // A visitor withdrawing a pending request cancels its still-waiting instances too.
        var cancellableStatuses = isVisitorOwner
            ? new[] { VisitInstanceStatus.WaitingRequestApproval, VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit }
            : new[] { VisitInstanceStatus.Assigned, VisitInstanceStatus.BeforeVisit };

        IReadOnlyList<VisitRequestCampus> targets;
        if (request.VisitInstanceId is { } instanceId)
        {
            var instance = visit.CampusInstances.FirstOrDefault(c => c.VisitInstanceId == instanceId)
                ?? throw new NotFoundException("VisitRequestCampus", instanceId);
            targets = new[] { instance };
        }
        else
        {
            targets = visit.CampusInstances.Where(c => cancellableStatuses.Contains(c.Status)).ToList();
        }

        // Authorization + actor classification.
        string actorType;
        string source;
        if (isVisitorOwner)
        {
            actorType = CancellationActorType.Visitor;
            source = CancellationSource.SelfService;
        }
        else if (isHo && visit.VisitScope == VisitScopes.MultiCampus)
        {
            actorType = CancellationActorType.Ho;
            source = CancellationSource.ExternalConfirmation;
        }
        else if (isStaffLeader && targets.All(t => t.CampusId == _currentUser.PrimaryCampusId))
        {
            actorType = CancellationActorType.StaffLeader;
            source = CancellationSource.ExternalConfirmation;
        }
        else if (targets.All(t => t.CurrentHostUserId == actorId))
        {
            // Current host cancels after the guest confirms via an external channel.
            actorType = CancellationActorType.Host;
            source = CancellationSource.ExternalConfirmation;
        }
        else
        {
            throw new ForbiddenException("Báº¡n khÃ´ng cÃ³ quyá»n há»§y lá»‹ch thÄƒm nÃ y.");
        }

        var now = _clock.UtcNow;
        var enforceBeforeStart = actorType == CancellationActorType.Visitor || actorType == CancellationActorType.Host;

        var cancelled = new List<CancelledCampusDto>();
        foreach (var instance in targets)
        {
            if (!cancellableStatuses.Contains(instance.Status))
                throw new BusinessRuleException($"KhÃ´ng thá»ƒ há»§y cÆ¡ sá»Ÿ á»Ÿ tráº¡ng thÃ¡i '{instance.Status}'.");

            if (enforceBeforeStart && now >= instance.PlannedStartAt)
                throw new BusinessRuleException("ÄÃ£ Ä‘áº¿n hoáº·c qua thá»i gian báº¯t Ä‘áº§u, khÃ´ng thá»ƒ há»§y.");

            var oldStatus = instance.Status;
            instance.Status = VisitInstanceStatus.Cancelled;
            instance.CancelledBy = actorId;
            instance.CancelledAt = now;
            instance.CancellationActorType = actorType;
            instance.CancellationSource = source;
            instance.CancellationReason = request.CancellationReason;
            instance.UpdatedAt = now;
            instance.UpdatedBy = actorId;
            instance.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitInstanceId = instance.VisitInstanceId,
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.CampusInstance,
                OldStatus = oldStatus,
                NewStatus = VisitInstanceStatus.Cancelled,
                ChangedBy = actorId,
                Reason = request.CancellationReason,
                ChangedAt = now
            });

            cancelled.Add(new CancelledCampusDto(instance.VisitInstanceId, instance.Status));
        }

        if (cancelled.Count == 0)
            throw new BusinessRuleException("KhÃ´ng cÃ³ cÆ¡ sá»Ÿ nÃ o á»Ÿ tráº¡ng thÃ¡i cÃ³ thá»ƒ há»§y.");

        // Single-campus, or all campuses now cancelled â†’ the overall request becomes CANCELLED.
        var allCancelled = visit.CampusInstances.All(c => c.Status == VisitInstanceStatus.Cancelled);
        if (allCancelled)
        {
            var oldReqStatus = visit.Status;
            visit.Status = VisitRequestStatuses.Cancelled;
            visit.CancelledBy = actorId;
            visit.CancelledAt = now;
            visit.CancellationActorType = actorType;
            visit.CancellationSource = source;
            visit.CancellationReason = request.CancellationReason;
            visit.UpdatedAt = now;
            visit.UpdatedBy = actorId;
            visit.RowVersion += 1;

            _db.VisitStatusLogs.Add(new VisitStatusLog
            {
                VisitRequestId = visit.VisitRequestId,
                StatusOwnerType = StatusOwnerType.Request,
                OldStatus = oldReqStatus,
                NewStatus = VisitRequestStatuses.Cancelled,
                ChangedBy = actorId,
                Reason = request.CancellationReason,
                ChangedAt = now
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            Action = "CANCEL_VISIT_REQUEST",
            EntityType = "VisitRequest",
            EntityId = visit.VisitRequestId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelVisitRequestResponse(
            visit.VisitRequestId,
            visit.Status,
            cancelled,
            allCancelled
                ? "ÄÆ¡n tham quan Ä‘Ã£ Ä‘Æ°á»£c há»§y."
                : "CÆ¡ sá»Ÿ Ä‘Ã£ Ä‘Æ°á»£c há»§y. CÃ¡c cÆ¡ sá»Ÿ cÃ²n láº¡i cá»§a Ä‘Æ¡n váº«n giá»¯ nguyÃªn.");
    }
}
