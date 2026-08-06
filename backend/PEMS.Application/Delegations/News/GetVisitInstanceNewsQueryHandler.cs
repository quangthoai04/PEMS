using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.News;

public sealed class GetVisitInstanceNewsQueryHandler
    : IRequestHandler<GetVisitInstanceNewsQuery, VisitNewsListDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetVisitInstanceNewsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VisitNewsListDto> Handle(GetVisitInstanceNewsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned) && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var actor = VisitNewsAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!actor.InScope)
            throw new ForbiddenException("Bạn không có quyền xem tin tức của chuyến thăm này.");

        var query = _db.News
            .Include(n => n.Translations).ThenInclude(t => t.Sections)
            .Where(n => n.VisitInstanceId == instance.VisitInstanceId);

        // Visibility (logic duyệt mới):
        //  • Host / Staff Leader of campus: every post of the instance.
        //  • HO / Visitor owner: PUBLISHED posts only (HO là read-only overview — không thấy
        //    PENDING_REVIEW/REJECTED/HIDDEN và không tham gia workflow duyệt).
        //  • Accepted participant (not host): ONLY their own posts.
        if (actor.IsHost || actor.IsStaffLeaderOfCampus)
        {
            // full list
        }
        else if (actor.IsHo || actor.IsGuestSide)
        {
            query = query.Where(n => n.Status == NewsStatus.Published);
        }
        else
        {
            query = query.Where(n => n.AuthorUserId == userId);
        }

        var posts = await query.OrderByDescending(n => n.SubmittedAt).ToListAsync(cancellationToken);

        var authorIds = posts.Select(n => n.AuthorUserId).Distinct().ToList();
        var authorNames = authorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => authorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        // HO / Visitor: read-only — no internal workflow fields, no action flags.
        bool isReadonlyViewer = actor.IsHo || actor.IsGuestSide;

        var items = posts.Select(n =>
        {
            var tr = n.Translations.FirstOrDefault(t => t.LanguageCode == "vi") ?? n.Translations.FirstOrDefault();
            var section = tr?.Sections.OrderBy(s => s.SectionOrder).FirstOrDefault();
            // Chỉ TÁC GIẢ được sửa bài của mình, khi bài đang chờ duyệt hoặc bị từ chối.
            bool canEdit = !isReadonlyViewer
                && n.AuthorUserId == userId
                && (n.Status == NewsStatus.PendingReview || n.Status == NewsStatus.Rejected);
            // Chỉ Staff Leader đúng campus duyệt/từ chối bài đang chờ duyệt.
            bool canReview = !isReadonlyViewer
                && actor.IsStaffLeaderOfCampus && n.Status == NewsStatus.PendingReview;
            return new VisitNewsDto
            {
                NewsId = n.NewsId,
                VisitInstanceId = instance.VisitInstanceId,
                Title = tr?.Title ?? "(Không có tiêu đề)",
                Summary = tr?.Summary,
                Body = section?.SectionBodyText ?? section?.SectionBodyHtml,
                Status = n.Status,
                IsPublished = n.Status == NewsStatus.Published,
                AuthorUserId = n.AuthorUserId,
                AuthorName = authorNames.TryGetValue(n.AuthorUserId, out var an) ? an : null,
                SubmittedAt = n.SubmittedAt,
                PublishedAt = n.PublishedAt,
                // HO/Visitor: không trả reviewNote nội bộ.
                ReviewNote = isReadonlyViewer ? null : n.ReviewNote,
                RowVersion = n.RowVersion,
                CanEdit = canEdit,
                CanApprove = canReview,
                CanReject = canReview,
            };
        }).ToList();

        return new VisitNewsListDto
        {
            VisitInstanceId = instance.VisitInstanceId,
            CanView = true,
            // HO/Visitor: never create — VisitNewsAccess already returns false,
            // but we enforce explicitly for defense-in-depth.
            CanCreate = !isReadonlyViewer && actor.CanCreate,
            Items = items,
        };
    }
}
