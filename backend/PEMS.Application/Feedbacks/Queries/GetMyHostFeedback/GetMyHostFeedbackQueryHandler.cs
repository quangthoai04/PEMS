using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Feedbacks.Common;

namespace PEMS.Application.Feedbacks.Queries.GetMyHostFeedback;

public sealed class GetMyHostFeedbackQueryHandler
    : IRequestHandler<GetMyHostFeedbackQuery, GetMyHostFeedbackResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyHostFeedbackQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetMyHostFeedbackResponse> Handle(
        GetMyHostFeedbackQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);
        var visitRequest = await _db.VisitRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("VisitRequest", instance.VisitRequestId);

        // The feedback rows below were already correctly scoped to TargetUserId == userId, but the
        // surrounding visit/delegation/host metadata was not gated at all — any authenticated user
        // could read another delegation's org name, campus, host and schedule by guessing an id. This
        // screen's audience is whoever the Host could have rated (an internal relation to the
        // instance — host, staff leader, IC/dept support, student), the same primitive
        // GetVisitInstanceParticipantsQueryHandler already uses for the same instance.
        var relation = await PEMS.Application.Delegations.Common.VisitInstanceAccess.ResolveRelationAsync(
            _db, _currentUser, instance, visitRequest, cancellationToken);
        if (!PEMS.Application.Delegations.Common.VisitInstanceAccess.CanViewInternal(relation))
            throw new ForbiddenException("Bạn không có quyền xem đánh giá cho chuyến thăm này.");

        var campusName = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusId == instance.CampusId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken);

        string? hostName = null;
        if (instance.CurrentHostUserId.HasValue)
        {
            hostName = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Always resolve the target to the CALLER — never trust a targetUserId from the client.
        var feedbacks = await _db.Feedbacks.AsNoTracking()
            .Include(f => f.RatingItems)
            .Where(f => f.VisitInstanceId == request.VisitInstanceId
                        && f.FeedbackType == FeedbackTypes.HostParticipant
                        && f.TargetUserId == userId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(cancellationToken);

        // Mixed per-campus v2: instance-scoped screen → THIS instance's detail name.
        var effectiveDelegationName = (await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_db, new[] { instance.VisitInstanceId }, cancellationToken))
            .GetValueOrDefault(instance.VisitInstanceId);

        return new GetMyHostFeedbackResponse
        {
            VisitInstanceId = instance.VisitInstanceId,
            VisitRequestId = instance.VisitRequestId,
            RequestCode = visitRequest.RequestCode,
            DelegationName = effectiveDelegationName,
            OrganizationName = visitRequest.RegistrantOrganization,
            CampusName = campusName,
            HostName = hostName,
            InstanceStatus = instance.Status,
            PlannedStartAt = instance.PlannedStartAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            PlannedEndAt = instance.PlannedEndAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            Feedbacks = feedbacks.Select(f => new HostFeedbackItemDto
            {
                FeedbackId = f.FeedbackId,
                HostName = f.SubmitterNameSnapshot,
                Rating = f.Rating,
                Comment = f.Comment,
                SubmittedAt = f.SubmittedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                RatingItems = f.RatingItems.OrderBy(ri => ri.DisplayOrder).Select(ri => new HostFeedbackRatingItemDto
                {
                    CriterionCode = ri.CriterionCode,
                    CriterionLabel = ri.CriterionLabel,
                    Rating = ri.Rating,
                }).ToList(),
            }).ToList(),
        };
    }
}
