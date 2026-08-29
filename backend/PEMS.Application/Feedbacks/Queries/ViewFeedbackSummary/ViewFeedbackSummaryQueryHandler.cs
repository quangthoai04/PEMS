using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
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

        if (_currentUserService.RoleCode != "HO" &&
            _currentUserService.RoleCode != "ADMIN")
        {
            // Security fix: this used to be `if (PrimaryCampusId.HasValue && role-not-HO/ADMIN)`, so a
            // non-HO/ADMIN caller with no PrimaryCampusId (auto-provisioned Visitor accounts never get
            // one set) skipped scoping entirely — AND fell through to the request.CampusId branch below,
            // which was meant only for HO/ADMIN's own optional filter, so an unscoped Visitor could even
            // choose which campus's feedback to read. A non-HO/ADMIN caller's visibility IS "their own
            // campus's feedback" — with no campus to scope to, that is zero rows, never every campus's.
            if (_currentUserService.PrimaryCampusId.HasValue)
            {
                var campusId = _currentUserService.PrimaryCampusId.Value;
                var allowedInstanceIds = _context.VisitRequestCampuses
                    .Where(x => x.CampusId == campusId)
                    .Select(x => x.VisitInstanceId);

                query = query.Where(x => x.VisitInstanceId.HasValue && allowedInstanceIds.Contains(x.VisitInstanceId.Value));
            }
            else
            {
                query = query.Where(x => false);
            }
        }
        else if (request.CampusId.HasValue)
        {
            // HO/ADMIN: optional self-chosen filter (never applies to Staff — their scope
            // above already fixes the campus and ignores whatever the client sends).
            var chosenCampusId = request.CampusId.Value;
            var chosenInstanceIds = _context.VisitRequestCampuses
                .Where(x => x.CampusId == chosenCampusId)
                .Select(x => x.VisitInstanceId);

            query = query.Where(x => x.VisitInstanceId.HasValue && chosenInstanceIds.Contains(x.VisitInstanceId.Value));
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
            // Mixed per-campus v2 requests match when ANY campus detail matches — the global
            // projection is never business content for mixed requests.
            var q = request.Q;
            var matchingRequestIds = _context.VisitRequests
                .Where(r => r.CampusInstances.Any(ci => ci.FormDetail != null && ci.FormDetail.DelegationName.Contains(q)))
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
        // Request-level fallback title: a MIXED v2 request has no single business name (plan §8.3).
        var visitTitles = await _context.VisitRequests.Where(r => requestIds.Contains(r.VisitRequestId))
            .ToDictionaryAsync(r => r.VisitRequestId,
                r => r.HasMixedCampusDetails ? "Khác nhau theo cơ sở" : (r.CampusInstances.Select(ci => ci.FormDetail.DelegationName).FirstOrDefault() ?? r.RequestCode ?? "Đoàn khách"),
                cancellationToken);

        var instanceIds = projections.Where(x => x.VisitInstanceId.HasValue).Select(x => x.VisitInstanceId!.Value).ToList();
        // Instance-scoped rows title from THIS instance's detail (mixed v2), single batched query.
        var effectiveInstanceTitles = await Delegations.Services.VisitFormRead.VisitInstanceEffectiveName
            .ForInstancesAsync(_context, instanceIds, cancellationToken);
        var campusIds = await _context.VisitRequestCampuses.Where(rc => instanceIds.Contains(rc.VisitInstanceId))
            .ToDictionaryAsync(rc => rc.VisitInstanceId, rc => rc.CampusId, cancellationToken);
            
        var cIds = campusIds.Values.Distinct().ToList();
        var campusNames = await _context.Campuses.Where(c => cIds.Contains(c.CampusId))
            .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);

        // Batch: every feedback row for this page's requests, grouped by the SAME (VisitRequestId,
        // VisitInstanceId) key `grouped` used above, instead of one OrderByDescending+First query per
        // page row. Scoped to `requestIds` (already computed above) is a safe superset — it may include
        // a few extra instances of the same requests that did not land on this page, never a wrong request.
        var latestSubmitterByGroup = (await _context.Feedbacks
                .Where(f => requestIds.Contains(f.VisitRequestId))
                .Select(f => new { f.VisitRequestId, f.VisitInstanceId, f.SubmittedAt, f.SubmitterNameSnapshot })
                .ToListAsync(cancellationToken))
            .GroupBy(f => (f.VisitRequestId, f.VisitInstanceId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.SubmittedAt).First().SubmitterNameSnapshot);

        foreach (var item in projections)
        {
            item.VisitTitle = item.VisitInstanceId.HasValue
                              && effectiveInstanceTitles.TryGetValue(item.VisitInstanceId.Value, out var instTitle)
                ? instTitle ?? ""
                : (visitTitles.TryGetValue(item.VisitRequestId, out var title) ? title : "");
            if (item.VisitInstanceId.HasValue && campusIds.TryGetValue(item.VisitInstanceId.Value, out var cId) && campusNames.TryGetValue(cId, out var cName))
            {
                item.CampusName = cName;
            }

            item.LatestSubmitterName =
                latestSubmitterByGroup.GetValueOrDefault((item.VisitRequestId, item.VisitInstanceId));
        }
        
        return PaginatedResult<FeedbackVisitSummaryItem>.Create(projections, request.Page, request.PageSize, total);
    }
}