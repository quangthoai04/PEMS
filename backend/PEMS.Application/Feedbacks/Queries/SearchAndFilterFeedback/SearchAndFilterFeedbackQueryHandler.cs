using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback;

public class SearchAndFilterFeedbackQueryHandler : IRequestHandler<SearchAndFilterFeedbackQuery, PaginatedResult<FeedbackListItem>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SearchAndFilterFeedbackQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedResult<FeedbackListItem>> Handle(SearchAndFilterFeedbackQuery request, CancellationToken cancellationToken)
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

        if (!string.IsNullOrEmpty(request.FeedbackType))
        {
            query = query.Where(x => x.FeedbackType == request.FeedbackType);
        }

        if (request.VisitRequestId.HasValue)
        {
            query = query.Where(x => x.VisitRequestId == request.VisitRequestId.Value);
        }

        if (!string.IsNullOrEmpty(request.Q))
        {
            query = query.Where(x => 
                x.SubmitterNameSnapshot.Contains(request.Q) || 
                x.TargetNameSnapshot.Contains(request.Q) ||
                x.Comment.Contains(request.Q));
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

        var total = await query.CountAsync(cancellationToken);
        
        var list = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var requestIds = list.Select(x => x.VisitRequestId).Distinct().ToList();
        var visitTitles = await _context.VisitRequests.Where(r => requestIds.Contains(r.VisitRequestId))
            .ToDictionaryAsync(r => r.VisitRequestId, r => r.DelegationName, cancellationToken);

        var items = list.Select(x => new FeedbackListItem
        {
            FeedbackId = x.FeedbackId,
            VisitRequestId = x.VisitRequestId,
            VisitInstanceId = x.VisitInstanceId,
            VisitTitle = visitTitles.TryGetValue(x.VisitRequestId, out var title) ? title : "",
            FeedbackType = x.FeedbackType,
            SubmittedByUserId = x.SubmittedByUserId,
            SubmitterRole = x.SubmitterRole,
            SubmitterContext = x.SubmitterContext,
            SubmitterNameSnapshot = x.SubmitterNameSnapshot,
            TargetUserId = x.TargetUserId,
            TargetRole = x.TargetRole,
            TargetContext = x.TargetContext,
            TargetNameSnapshot = x.TargetNameSnapshot,
            Rating = x.Rating,
            CommentPreview = x.Comment,
            SubmittedAt = x.SubmittedAt
        }).ToList();

        return PaginatedResult<FeedbackListItem>.Create(items, request.Page, request.PageSize, total);
    }
}