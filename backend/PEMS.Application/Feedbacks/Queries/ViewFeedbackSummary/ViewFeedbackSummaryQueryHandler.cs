using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary;

public class ViewFeedbackSummaryQueryHandler : IRequestHandler<ViewFeedbackSummaryQuery, PaginatedResult<FeedbackVisitSummaryItem>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    
    public ViewFeedbackSummaryQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedResult<FeedbackVisitSummaryItem>> Handle(ViewFeedbackSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Feedbacks.AsNoTracking();

        if (_currentUserService.PrimaryCampusId.HasValue && 
            _currentUserService.RoleCode != "HO" && 
            _currentUserService.RoleCode != "ADMIN")
        {
            var campusId = _currentUserService.PrimaryCampusId.Value;
            var allowedInstanceIds = _context.VisitRequestCampuses
                .Where(x => x.CampusId == campusId)
                .Select(x => x.VisitInstanceId);

            query = query.Where(x => x.VisitInstanceId.HasValue && allowedInstanceIds.Contains(x.VisitInstanceId.Value));
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(x => x.SubmittedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(x => x.SubmittedAt <= request.ToDate.Value);
        }

        if (!string.IsNullOrEmpty(request.Q))
        {
            var matchingRequestIds = _context.VisitRequests
                .Where(r => r.DelegationName.Contains(request.Q))
                .Select(r => r.VisitRequestId);
            query = query.Where(x =>
                matchingRequestIds.Contains(x.VisitRequestId) ||
                x.SubmitterNameSnapshot.Contains(request.Q) ||
                x.TargetNameSnapshot.Contains(request.Q));
        }

        if (!string.IsNullOrEmpty(request.RatingLevel))
        {
            if (request.RatingLevel == "LOW") query = query.Where(x => x.Rating <= 2);
            else if (request.RatingLevel == "GOOD") query = query.Where(x => x.Rating >= 4);
            else if (int.TryParse(request.RatingLevel, out var r)) query = query.Where(x => x.Rating == r);
        }

        if (!string.IsNullOrEmpty(request.SubmitterRole))
        {
            query = query.Where(x => x.SubmitterRole == request.SubmitterRole);
        }

        var grouped = query.GroupBy(f => new { f.VisitRequestId, f.VisitInstanceId });

        var total = await grouped.CountAsync(cancellationToken);

        var projections = await grouped
            .OrderByDescending(g => g.Max(x => x.SubmittedAt))
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new FeedbackVisitSummaryItem
            {
                VisitRequestId = g.Key.VisitRequestId,
                VisitInstanceId = g.Key.VisitInstanceId,
                TotalFeedbacks = g.Count(),
                AverageRating = g.Average(x => (double)x.Rating),
                LatestSubmittedAt = g.Max(x => x.SubmittedAt),
                LowRatingCount = g.Count(x => x.Rating <= 2)
            })
            .ToListAsync(cancellationToken);

        var requestIds = projections.Select(x => x.VisitRequestId).Distinct().ToList();
        var visitTitles = await _context.VisitRequests.Where(r => requestIds.Contains(r.VisitRequestId))
            .ToDictionaryAsync(r => r.VisitRequestId, r => r.DelegationName, cancellationToken);
            
        var instanceIds = projections.Where(x => x.VisitInstanceId.HasValue).Select(x => x.VisitInstanceId!.Value).ToList();
        var campusIds = await _context.VisitRequestCampuses.Where(rc => instanceIds.Contains(rc.VisitInstanceId))
            .ToDictionaryAsync(rc => rc.VisitInstanceId, rc => rc.CampusId, cancellationToken);
            
        var cIds = campusIds.Values.Distinct().ToList();
        var campusNames = await _context.Campuses.Where(c => cIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        foreach (var item in projections)
        {
            item.VisitTitle = visitTitles.TryGetValue(item.VisitRequestId, out var title) ? title : "";
            if (item.VisitInstanceId.HasValue && campusIds.TryGetValue(item.VisitInstanceId.Value, out var cId) && campusNames.TryGetValue(cId, out var cName))
            {
                item.CampusName = cName;
            }
            
            var latestFb = await _context.Feedbacks.Where(f => f.VisitRequestId == item.VisitRequestId && f.VisitInstanceId == item.VisitInstanceId)
                .OrderByDescending(f => f.SubmittedAt).FirstOrDefaultAsync(cancellationToken);
            item.LatestSubmitterName = latestFb?.SubmitterNameSnapshot;
        }
        
        return PaginatedResult<FeedbackVisitSummaryItem>.Create(projections, request.Page, request.PageSize, total);
    }
}