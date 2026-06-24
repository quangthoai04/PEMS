using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Shared;

namespace PEMS.Application.Delegations.News;

public sealed class SubmitVisitInstanceNewsCommandHandler
    : IRequestHandler<SubmitVisitInstanceNewsCommand, VisitNewsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public SubmitVisitInstanceNewsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<VisitNewsDto> Handle(SubmitVisitInstanceNewsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        var news = await _db.News
            .Include(n => n.Translations).ThenInclude(t => t.Sections)
            .FirstOrDefaultAsync(n => n.NewsId == request.NewsId, cancellationToken)
            ?? throw new NotFoundException("News", request.NewsId);

        if (news.VisitInstanceId is null)
            throw new BusinessRuleException("Bài tin này không gắn với chuyến thăm.");

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == news.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", news.VisitInstanceId.Value);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var (inScope, _, _) = VisitNewsAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!inScope)
            throw new ForbiddenException("Bạn không có quyền xem tin tức của chuyến thăm này.");

        bool isHost = instance.CurrentHostUserId == userId;
        bool isLive = instance.Status != VisitInstanceStatus.Closed
            && instance.Status != VisitInstanceStatus.Cancelled
            && instance.VisitRequest.Status != VisitRequestStatuses.Cancelled;
        if (!((news.AuthorUserId == userId || isHost) && isLive))
            throw new ForbiddenException("Bạn không có quyền gửi duyệt bài tin này.");
        if (news.Status == NewsStatus.Published)
            throw new BusinessRuleException("Bài tin đã được đăng.");

        var now = _clock.UtcNow;
        news.Status = NewsStatus.PendingReview;
        news.SubmittedAt = now;
        news.ReviewNote = null;
        news.RowVersion += 1;
        news.UpdatedAt = now;
        news.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);

        var tr = news.Translations.FirstOrDefault(t => t.LanguageCode == "vi") ?? news.Translations.FirstOrDefault();
        var section = tr?.Sections.OrderBy(s => s.SectionOrder).FirstOrDefault();
        return new VisitNewsDto
        {
            NewsId = news.NewsId,
            VisitInstanceId = instance.VisitInstanceId,
            Title = tr?.Title ?? "(Không có tiêu đề)",
            Summary = tr?.Summary,
            Body = section?.SectionBodyText ?? section?.SectionBodyHtml,
            Status = news.Status,
            IsPublished = false,
            AuthorUserId = news.AuthorUserId,
            SubmittedAt = news.SubmittedAt,
            RowVersion = news.RowVersion,
            CanEdit = (news.AuthorUserId == userId || isHost) && isLive,
        };
    }
}
