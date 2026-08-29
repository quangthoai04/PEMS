using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Domain.Constants;
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

        if (_currentUserService.RoleCode != "HO" &&
            _currentUserService.RoleCode != "ADMIN")
        {
            // Security fix: this used to be `if (PrimaryCampusId.HasValue && role-not-HO/ADMIN)`, so a
            // non-HO/ADMIN caller with no PrimaryCampusId (auto-provisioned Visitor accounts never get
            // one set — see UserProvisionService) skipped scoping entirely and read every feedback row
            // system-wide. A non-HO/ADMIN caller's visibility IS "their own campus's feedback" — with
            // no campus to scope to, that is zero rows, never every campus's.
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

        // DB-PAGE-002: bound page/pageSize the same way the other list queries already do.
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var list = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Feedback rows are INSTANCE-scoped: each row is titled with THAT instance's own detail name.
        // The visit request itself carries no name, so there is nothing else the title could come from.
        var instanceIds = list.Where(x => x.VisitInstanceId.HasValue)
            .Select(x => x.VisitInstanceId!.Value).Distinct().ToList();
        var visitTitles = await _context.VisitRequestCampuses
            .Where(c => instanceIds.Contains(c.VisitInstanceId))
            .Select(c => new
            {
                c.VisitInstanceId,
                Title = c.FormDetail != null ? c.FormDetail.DelegationName : null,
            })
            .ToDictionaryAsync(c => c.VisitInstanceId, c => c.Title, cancellationToken);

        var items = list.Select(x => new FeedbackListItem
        {
            FeedbackId = x.FeedbackId,
            VisitRequestId = x.VisitRequestId,
            VisitInstanceId = x.VisitInstanceId,
            VisitTitle = x.VisitInstanceId.HasValue
                         && visitTitles.TryGetValue(x.VisitInstanceId.Value, out var title) ? title : "",
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

        return PaginatedResult<FeedbackListItem>.Create(items, page, pageSize, total);
    }
}