using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;

namespace PEMS.Application.Feedbacks.Queries.GetPendingFeedbackNotifications;

public sealed class GetPendingFeedbackNotificationsQueryHandler
    : IRequestHandler<GetPendingFeedbackNotificationsQuery, GetPendingFeedbackNotificationsResponse>
{
    private const int MaxItems = 50;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPendingFeedbackNotificationsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetPendingFeedbackNotificationsResponse> Handle(
        GetPendingFeedbackNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return new GetPendingFeedbackNotificationsResponse();
        var userId = _currentUser.UserId.Value;

        var eligibleStatuses = FeedbackEligibility.EligibleInstanceStatuses;

        var rows = await (
            from i in _db.VisitRequestCampuses.AsNoTracking()
            join r in _db.VisitRequests.AsNoTracking() on i.VisitRequestId equals r.VisitRequestId
            join c in _db.Campuses.AsNoTracking() on i.CampusId equals c.CampusId
            where eligibleStatuses.Contains(i.Status)
                  && (i.CurrentHostUserId == userId || r.VisitorUserId == userId)
            orderby i.PlannedStartAt descending
            select new
            {
                i.VisitInstanceId,
                r.VisitRequestId,
                // Mixed per-campus v2 rows show THIS instance's detail name (no global fallback).
                DelegationName = i.FormDetail != null ? i.FormDetail.DelegationName : null,
                CampusName = c.Name,
                i.Status,
                i.PlannedStartAt,
                IsHost = i.CurrentHostUserId == userId,
            })
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new GetPendingFeedbackNotificationsResponse();

        var instanceIds = rows.Select(x => x.VisitInstanceId).ToList();
        var submittedInstanceIds = (await _db.Feedbacks.AsNoTracking()
            .Where(f => f.SubmittedByUserId == userId
                        && f.VisitInstanceId != null
                        && instanceIds.Contains(f.VisitInstanceId.Value))
            .Select(f => f.VisitInstanceId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var items = rows.Select(x => new PendingFeedbackItemDto
        {
            VisitRequestId = x.VisitRequestId,
            VisitInstanceId = x.VisitInstanceId,
            ActorType = x.IsHost ? FeedbackSubmitterRoles.Host : FeedbackSubmitterRoles.Visitor,
            DelegationName = x.DelegationName,
            CampusName = x.CampusName,
            InstanceStatus = x.Status,
            PlannedStartAt = x.PlannedStartAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            AlreadySubmitted = submittedInstanceIds.Contains(x.VisitInstanceId),
        }).ToList();

        return new GetPendingFeedbackNotificationsResponse
        {
            Items = items,
            PendingCount = items.Count(x => !x.AlreadySubmitted),
        };
    }
}
