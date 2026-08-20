using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Queries.ResolveNotificationVisitTarget;

/// <summary>
/// Resolves a notification's exact business target WITHOUT going through the "all"-tab merge
/// (<c>ViewGuestDelegationListQueryHandler.QueryAllMergedAsync</c>), whose job is to collapse every
/// relation the caller holds on a request into ONE row for display. That collapse is exactly wrong
/// for a notification: a Staff Leader who is both campus reviewer at HN and a participant at DN
/// would have the merge keep only HN's relation context, silently losing DN — so a
/// "you were invited (DN)" notification could resolve against HN's state instead.
///
/// <para>
/// Instead this re-sends <see cref="ViewGuestDelegationListQuery"/> once per POPULATION ("responsible",
/// "attending", "registered", "hosted") — the same, already-trusted authorization/relation pipeline
/// every list row goes through — and unions the (already correctly-scoped) <c>RelationContexts</c> each
/// returns for this one request. A tab invalid for the caller's role simply comes back empty (the
/// existing handler's own role gate), so no role-specific branching is duplicated here.
/// </para>
/// </summary>
public sealed class ResolveNotificationVisitTargetQueryHandler
    : IRequestHandler<ResolveNotificationVisitTargetQuery, NotificationVisitTargetDto>
{
    private static readonly string[] Populations = { "responsible", "attending", "registered", "hosted" };

    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ResolveNotificationVisitTargetQueryHandler(
        IMediator mediator, IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<NotificationVisitTargetDto> Handle(
        ResolveNotificationVisitTargetQuery request, CancellationToken cancellationToken)
    {
        // ADMIN takes no part in the reception flow at all (PERMISSION_MATRIX §5.4) — no silent
        // navigate, an explicit no-access answer.
        if (!_currentUser.UserId.HasValue || _currentUser.RoleCode == RoleCodes.Admin)
        {
            return new NotificationVisitTargetDto
            {
                Exists = false,
                HasAccess = false,
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = request.VisitInstanceId,
            };
        }

        var items = new List<VisitRequestManagementItemDto>();
        foreach (var tab in Populations)
        {
            var page = await _mediator.Send(
                new ViewGuestDelegationListQuery
                {
                    Tab = tab,
                    VisitRequestId = request.VisitRequestId,
                    Page = 1,
                    PageSize = 5,
                },
                cancellationToken);
            items.AddRange(page.Items);
        }

        if (items.Count == 0)
        {
            var requestExists = await _context.VisitRequests
                .AnyAsync(r => r.VisitRequestId == request.VisitRequestId, cancellationToken);
            var instanceExists = requestExists && (!request.VisitInstanceId.HasValue || await _context.VisitRequestCampuses
                .AnyAsync(c => c.VisitRequestId == request.VisitRequestId
                    && c.VisitInstanceId == request.VisitInstanceId.Value, cancellationToken));
            return new NotificationVisitTargetDto
            {
                Exists = requestExists && instanceExists,
                HasAccess = false,
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = request.VisitInstanceId,
            };
        }

        var first = items[0];
        var canViewRequestDetail = items.Any(i => i.CanViewRequestDetail);

        // Union every relation context every population returned for this one request — each context
        // already carries its own real VisitInstanceId/CampusId/EntryContext, computed by the SAME
        // BuildRelationContexts every list row uses. De-duplicated on (relation, instance): the same
        // relation can legitimately surface from more than one population (e.g. "responsible" and
        // "hosted" both carry a regular Staff's own Host relation).
        var allContexts = items
            .SelectMany(i => i.RelationContexts)
            .GroupBy(c => (c.Relation, c.Scope, c.VisitInstanceId))
            .Select(g => g.First())
            .ToList();

        if (!request.VisitInstanceId.HasValue)
        {
            // Request-scoped notification: only the REQUEST-scope relation (Registrant) is in play —
            // never guess a campus that was not named.
            return new NotificationVisitTargetDto
            {
                Exists = true,
                HasAccess = canViewRequestDetail || allContexts.Count > 0,
                VisitRequestId = request.VisitRequestId,
                RequestStatus = first.RequestStatus,
                VisitScope = first.VisitScope,
                RequestCode = first.RequestCode,
                DelegationName = first.DelegationName,
                CanViewRequestDetail = canViewRequestDetail,
                RelationContexts = allContexts.Where(c => c.Scope == VisitActionScopes.Request).ToList(),
            };
        }

        var instanceId = request.VisitInstanceId.Value;
        var scopedContexts = allContexts.Where(c => c.VisitInstanceId == instanceId).ToList();

        // Exact per-instance display facts: an instance-level row that IS this instance, or (for a
        // Visitor/HO request-level row) the matching entry in its per-campus accordion.
        var directItem = items.FirstOrDefault(i => i.VisitInstanceId == instanceId);
        var progressItem = items
            .SelectMany(i => i.CampusProgressItems)
            .FirstOrDefault(cp => cp.VisitInstanceId == instanceId);

        if (directItem is null && progressItem is null && scopedContexts.Count == 0)
        {
            var instanceExists = await _context.VisitRequestCampuses
                .AnyAsync(c => c.VisitRequestId == request.VisitRequestId && c.VisitInstanceId == instanceId,
                    cancellationToken);
            return new NotificationVisitTargetDto
            {
                Exists = instanceExists,
                HasAccess = false,
                VisitRequestId = request.VisitRequestId,
                VisitInstanceId = instanceId,
            };
        }

        var participantItem = items.FirstOrDefault(
            i => i.VisitInstanceId == instanceId && i.ParticipantId.HasValue);

        return new NotificationVisitTargetDto
        {
            Exists = true,
            HasAccess = true,
            VisitRequestId = request.VisitRequestId,
            VisitInstanceId = instanceId,
            CampusId = directItem?.CampusId ?? progressItem?.CampusId ?? scopedContexts.FirstOrDefault()?.CampusId,
            CampusName = directItem?.CampusName ?? progressItem?.CampusName ?? scopedContexts.FirstOrDefault()?.CampusName,
            RequestStatus = first.RequestStatus,
            CampusStatus = directItem?.CampusStatus ?? progressItem?.InstanceStatus,
            VisitScope = first.VisitScope,
            RequestCode = first.RequestCode,
            DelegationName = first.DelegationName,
            CanViewRequestDetail = canViewRequestDetail,
            RelationContexts = scopedContexts,
            ParticipantId = participantItem?.ParticipantId,
            ParticipantStatus = participantItem?.ParticipantStatus,
        };
    }
}
