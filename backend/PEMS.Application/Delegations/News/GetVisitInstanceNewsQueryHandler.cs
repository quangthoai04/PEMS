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
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, canCreate, canSeeUnpublished) =
            VisitNewsAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem tin tức của chuyến thăm này.");

        bool isHost = instance.CurrentHostUserId == userId;
        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled
            && instance.VisitRequest.Status != VisitRequestStatuses.Cancelled;

        var query = _db.News
            .Include(n => n.Translations).ThenInclude(t => t.Sections)
            .Where(n => n.VisitInstanceId == instance.VisitInstanceId);
        if (!canSeeUnpublished)
            query = query.Where(n => n.Status == NewsStatus.Published);

        var posts = await query.OrderByDescending(n => n.SubmittedAt).ToListAsync(cancellationToken);

        var authorIds = posts.Select(n => n.AuthorUserId).Distinct().ToList();
        var authorNames = authorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.Where(u => authorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var items = posts.Select(n =>
        {
            var tr = n.Translations.FirstOrDefault(t => t.LanguageCode == "vi") ?? n.Translations.FirstOrDefault();
            var section = tr?.Sections.OrderBy(s => s.SectionOrder).FirstOrDefault();
            bool canEdit = (n.AuthorUserId == userId || isHost) && n.Status != NewsStatus.Published && isLive;
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
                ReviewNote = n.ReviewNote,
                RowVersion = n.RowVersion,
                CanEdit = canEdit,
            };
        }).ToList();

        return new VisitNewsListDto
        {
            VisitInstanceId = instance.VisitInstanceId,
            CanView = true,
            CanCreate = canCreate,
            Items = items,
        };
    }
}
