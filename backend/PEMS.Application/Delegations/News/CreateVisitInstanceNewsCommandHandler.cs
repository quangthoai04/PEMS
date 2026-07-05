using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.News;
using PEMS.Shared;

namespace PEMS.Application.Delegations.News;

public sealed class CreateVisitInstanceNewsCommandHandler
    : IRequestHandler<CreateVisitInstanceNewsCommand, VisitNewsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public CreateVisitInstanceNewsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<VisitNewsDto> Handle(CreateVisitInstanceNewsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Tiêu đề bài tin không được để trống.");

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == request.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", request.VisitInstanceId);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var actor = VisitNewsAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!actor.InScope)
            throw new ForbiddenException("Bạn không có quyền xem tin tức của chuyến thăm này.");
        if (!actor.CanCreate)
            throw new ForbiddenException("Bạn không có quyền viết tin tức cho chuyến thăm này.");

        if (instance.NewsNotRequired)
            throw new ForbiddenException("Chuyến thăm này không yêu cầu bài tin tức.");

        if (instance.VisitRequest.MediaConsentStatus != PEMS.Shared.MediaConsentStatus.Agreed)
            throw new ForbiddenException("Khách không đồng ý truyền thông, không thể tạo bài tin.");

        var now = _clock.UtcNow;
        var news = new PEMS.Domain.Entities.News.News
        {
            CampusId = instance.CampusId,
            VisitInstanceId = instance.VisitInstanceId,
            AuthorUserId = userId,
            // TODO (backlog): implement the news review/publish flow PENDING_REVIEW → PUBLISHED via the
            // existing News review UC (ApproveNews/PublishNews handlers are still stubs). Until then a
            // Visitor sees no posts here, since visitors only ever see PUBLISHED news (by design).
            Status = NewsStatus.PendingReview,
            SubmittedAt = now,
            IsFeatured = false,
            RowVersion = 0,
            CreatedAt = now,
            CreatedBy = userId,
        };
        var translation = new NewsTranslation
        {
            LanguageCode = "vi",
            Title = request.Title.Trim(),
            // Slug must be unique per language; a short random suffix guarantees it without diacritics.
            Slug = "vn-" + Guid.NewGuid().ToString("N").Substring(0, 12),
            Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary!.Trim(),
            CreatedAt = now,
        };
        var section = new NewsContentSection
        {
            SectionOrder = 1,
            SectionTitle = request.Title.Trim().Length > 255 ? request.Title.Trim()[..255] : request.Title.Trim(),
            SectionBodyHtml = ToHtml(request.Body),
            SectionBodyText = request.Body,
            CreatedAt = now,
        };
        translation.Sections.Add(section);
        news.Translations.Add(translation);

        _db.News.Add(news);
        await _db.SaveChangesAsync(cancellationToken);

        return new VisitNewsDto
        {
            NewsId = news.NewsId,
            VisitInstanceId = instance.VisitInstanceId,
            Title = translation.Title,
            Summary = translation.Summary,
            Body = section.SectionBodyText,
            Status = news.Status,
            IsPublished = false,
            AuthorUserId = userId,
            SubmittedAt = news.SubmittedAt,
            RowVersion = news.RowVersion,
            CanEdit = true,
        };
    }

    // Wrap author plain text as sanitized HTML (encode, newlines → <br/>) for the rich-text column.
    private static string ToHtml(string? text)
        => "<p>" + WebUtility.HtmlEncode(text ?? string.Empty).Replace("\n", "<br/>") + "</p>";
}
