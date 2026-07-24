using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.News;
using PEMS.Shared;

namespace PEMS.Application.Delegations.News;

public sealed class UpdateVisitInstanceNewsCommandHandler
    : IRequestHandler<UpdateVisitInstanceNewsCommand, VisitNewsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateVisitInstanceNewsCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<VisitNewsDto> Handle(UpdateVisitInstanceNewsCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var userId = _currentUser.UserId.Value;

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Tiêu đề bài tin không được để trống.");

        var news = await _db.News
            .Include(n => n.Translations).ThenInclude(t => t.Sections)
            .FirstOrDefaultAsync(n => n.NewsId == request.NewsId, cancellationToken)
            ?? throw new NotFoundException("News", request.NewsId);

        if (news.VisitInstanceId is null)
            throw new BusinessRuleException("Bài tin này không gắn với chuyến thăm.");

        var instance = await _db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .Include(c => c.FormDetail)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == news.VisitInstanceId, cancellationToken)
            ?? throw new NotFoundException("VisitRequestCampus", news.VisitInstanceId.Value);

        var acceptedRole = await _db.VisitParticipants
            .Where(p => p.VisitInstanceId == instance.VisitInstanceId && p.UserId == userId
                && p.Status == ParticipantStatuses.Accepted && !p.IsHost)
            .Select(p => p.ParticipantRole)
            .FirstOrDefaultAsync(cancellationToken);

        var actor = VisitNewsAccess.Evaluate(instance, instance.VisitRequest, _currentUser, acceptedRole);
        if (!actor.InScope)
            throw new ForbiddenException("Bạn không có quyền xem tin tức của chuyến thăm này.");

        if (instance.NewsNotRequired)
            throw new ForbiddenException("Chuyến thăm này không yêu cầu bài tin tức.");

        // Media consent is per campus — gate on THIS instance's own detail.
        if (instance.FormDetail?.MediaConsentStatus != PEMS.Shared.MediaConsentStatus.Agreed)
            throw new ForbiddenException("Khách không đồng ý truyền thông, không thể cập nhật bài tin.");

        bool isCancelled = instance.Status == VisitInstanceStatus.Cancelled
            || instance.VisitRequest.Status == VisitRequestStatuses.Cancelled;

        // Ai viết bài nào thì người đó sửa bài đó — Host KHÔNG được sửa bài của participant.
        if (news.AuthorUserId != userId || isCancelled)
            throw new ForbiddenException("Bạn chỉ có thể chỉnh sửa bài viết do chính mình tạo.");
        if (news.Status != NewsStatus.PendingReview && news.Status != NewsStatus.Rejected)
            throw new BusinessRuleException("Chỉ có thể chỉnh sửa bài viết đang chờ duyệt hoặc bị từ chối.");
        if (news.RowVersion != request.RowVersion)
            throw new ConflictException("Bài tin đã được cập nhật bởi người khác. Vui lòng tải lại nội dung mới nhất.");

        var now = _clock.VietnamNow;
        var translation = news.Translations.FirstOrDefault(t => t.LanguageCode == "vi") ?? news.Translations.FirstOrDefault();
        if (translation == null)
        {
            translation = new NewsTranslation { NewsId = news.NewsId, LanguageCode = "vi", Slug = "vn-" + Guid.NewGuid().ToString("N").Substring(0, 12), CreatedAt = now };
            news.Translations.Add(translation);
        }
        translation.Title = request.Title.Trim();
        translation.Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary!.Trim();
        translation.UpdatedAt = now;

        var section = translation.Sections.OrderBy(s => s.SectionOrder).FirstOrDefault();
        if (section == null)
        {
            section = new NewsContentSection { SectionOrder = 1, CreatedAt = now };
            translation.Sections.Add(section);
        }
        section.SectionTitle = translation.Title.Length > 255 ? translation.Title[..255] : translation.Title;
        section.SectionBodyHtml = ToHtml(request.Body);
        section.SectionBodyText = request.Body;
        section.UpdatedAt = now;

        // Editing resubmits for review.
        news.Status = NewsStatus.PendingReview;
        news.SubmittedAt = now;
        news.ReviewNote = null;
        news.RowVersion += 1;
        news.UpdatedAt = now;
        news.UpdatedBy = userId;

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
            AuthorUserId = news.AuthorUserId,
            SubmittedAt = news.SubmittedAt,
            RowVersion = news.RowVersion,
            CanEdit = true,
        };
    }

    private static string ToHtml(string? text)
        => "<p>" + WebUtility.HtmlEncode(text ?? string.Empty).Replace("\n", "<br/>") + "</p>";
}
