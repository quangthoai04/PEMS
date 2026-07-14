using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using NewsEntity = PEMS.Domain.Entities.News.News;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.CreateNews;

public sealed class CreateNewsCommandHandler
    : IRequestHandler<CreateNewsCommand, CreateNewsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly ILogger<CreateNewsCommandHandler> _logger;

    public CreateNewsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer,
        ILogger<CreateNewsCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    public async Task<CreateNewsResponse> Handle(
        CreateNewsCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;

        // Step 1: Role check — only Staff (regular) and Student
        var isAllowed = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                     || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Chỉ Staff thường và Student mới được tạo tin tức.");

        // Step 2: Load visit instance (+ media consent / news_not_required flags)
        var visitInstance = await _dbContext.VisitRequestCampuses
            .AsNoTracking()
            .Where(vrc => vrc.VisitInstanceId == request.VisitInstanceId)
            .Select(vrc => new
            {
                vrc.VisitInstanceId,
                vrc.CampusId,
                vrc.Status,
                vrc.CurrentHostUserId,
                vrc.NewsNotRequired,
                MediaConsentStatus = vrc.VisitRequest.MediaConsentStatus
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Chuyến tiếp khách", request.VisitInstanceId);

        // Step 3: Writing window — from AFTER_VISIT onward (news được viết SAU tiếp khách và là
        // một điều kiện đóng đoàn, nên phải mở TRƯỚC khi đóng; sau khi đóng vẫn được viết thêm).
        if (visitInstance.Status != VisitInstanceStatuses.AfterVisit
            && visitInstance.Status != VisitInstanceStatuses.Closed)
            throw new ConflictException(
                "Chỉ có thể tạo tin tức từ giai đoạn Sau tiếp khách của chuyến tiếp khách.");

        if (visitInstance.NewsNotRequired)
            throw new ConflictException("Chuyến tiếp khách này đã được xác nhận không yêu cầu tin tức.");

        if (visitInstance.MediaConsentStatus != PEMS.Shared.MediaConsentStatus.Agreed)
            throw new ConflictException("Khách không đồng ý truyền thông, không thể tạo bài tin tức.");

        // Step 4: Current user must be the Host or an ACCEPTED participant of the instance
        var isHost = visitInstance.CurrentHostUserId == currentUserId;
        var isParticipant = isHost || await _dbContext.VisitParticipants
            .AsNoTracking()
            .AnyAsync(vp =>
                vp.VisitInstanceId == request.VisitInstanceId &&
                vp.UserId == currentUserId &&
                vp.Status == ParticipantStatuses.Accepted,
                cancellationToken);

        if (!isParticipant)
            throw new ForbiddenException(
                "Bạn chỉ có thể tạo tin tức cho chuyến tiếp khách mà bạn là Host hoặc đã xác nhận tham gia.");

        // Step 5: One news per AUTHOR per visit instance — nhiều người cùng chuyến đều được viết
        // bài riêng (Host + các participant), nhưng mỗi người chỉ một bài; sửa bài hiện có nếu đã tạo.
        var existingNews = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.VisitInstanceId == request.VisitInstanceId
                     && n.AuthorUserId == currentUserId)
            .Select(n => new { n.NewsId, n.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (existingNews is not null)
        {
            var canEdit = existingNews.Status == NewsConstants.Status.PendingReview ||
                          existingNews.Status == NewsConstants.Status.Rejected;

            return new CreateNewsResponse
            {
                Success   = false,
                Message   = "Bạn đã có bài viết cho chuyến này. Vui lòng chỉnh sửa bài hiện có.",
                ErrorCode = "NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE",
                Data      = new CreateNewsData
                {
                    ExistingNewsId  = existingNews.NewsId,
                    CanEditExisting = canEdit
                }
            };
        }

        // Step 6: Validate cover file exists (if provided)
        if (request.CoverFileId.HasValue)
        {
            var coverExists = await _dbContext.Files
                .AsNoTracking()
                .AnyAsync(f => f.FileId == request.CoverFileId.Value, cancellationToken);
            if (!coverExists)
                throw new NotFoundException("File ảnh bìa", request.CoverFileId.Value);
        }

        // Step 7: Validate all section file IDs exist
        var allSectionFileIds = request.ContentSections
            .Where(s => s.SectionFiles != null)
            .SelectMany(s => s.SectionFiles!)
            .Select(f => f.FileId)
            .Distinct()
            .ToList();

        if (allSectionFileIds.Count > 0)
        {
            var existingFileIds = await _dbContext.Files
                .AsNoTracking()
                .Where(f => allSectionFileIds.Contains(f.FileId))
                .Select(f => f.FileId)
                .ToListAsync(cancellationToken);

            var missingFileId = allSectionFileIds.FirstOrDefault(id => !existingFileIds.Contains(id));
            if (missingFileId != default)
                throw new NotFoundException("File đính kèm", missingFileId);
        }

        // Step 8: Sanitize inputs
        var sanitizedTitle   = _sanitizer.Sanitize(request.Title.Trim());
        var sanitizedSummary = _sanitizer.Sanitize(request.Summary.Trim());

        if (string.IsNullOrWhiteSpace(sanitizedTitle))
            throw new ValidationException("Tiêu đề không hợp lệ sau khi xử lý.");
        if (string.IsNullOrWhiteSpace(sanitizedSummary))
            throw new ValidationException("Mô tả ngắn không hợp lệ sau khi xử lý.");

        var now = VietnamTime.Now();

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            // Step 9: Create news
            var news = new NewsEntity
            {
                CampusId        = visitInstance.CampusId,
                VisitInstanceId = request.VisitInstanceId,
                AuthorUserId    = currentUserId,
                CoverFileId     = request.CoverFileId,
                Status          = NewsConstants.Status.PendingReview,
                SubmittedAt     = now,
                IsFeatured      = false,
                RowVersion      = 0,
                CreatedAt       = now,
                CreatedBy       = currentUserId,
                UpdatedAt       = null,
                UpdatedBy       = null
            };
            _dbContext.News.Add(news);
            await _dbContext.SaveChangesAsync(cancellationToken); // get NewsId

            // Step 10: Create Vietnamese translation
            var baseSlug = ToSlug(sanitizedTitle);
            var slug     = $"{baseSlug}-{news.NewsId}";

            var translation = new NewsTranslation
            {
                NewsId         = news.NewsId,
                LanguageCode   = "vi",
                Title          = sanitizedTitle,
                Slug           = slug,
                Summary        = sanitizedSummary,
                SeoTitle       = sanitizedTitle,
                SeoDescription = sanitizedSummary,
                CreatedAt      = now,
                UpdatedAt      = null
            };
            _dbContext.NewsTranslations.Add(translation);
            await _dbContext.SaveChangesAsync(cancellationToken); // get NewsTranslationId

            // Step 11: Create content sections (ordered)
            var sections = request.ContentSections
                .OrderBy(s => s.SectionOrder)
                .ToList();

            foreach (var sectionDto in sections)
            {
                var sanitizedSectionTitle = _sanitizer.Sanitize(sectionDto.SectionTitle.Trim());
                var sanitizedBodyHtml     = _sanitizer.Sanitize(sectionDto.SectionBodyHtml);
                var bodyText              = ExtractPlainText(sanitizedBodyHtml);

                if (string.IsNullOrWhiteSpace(sanitizedSectionTitle))
                    throw new ValidationException($"Tiêu đề nội dung {sectionDto.SectionOrder} không hợp lệ.");
                if (string.IsNullOrWhiteSpace(bodyText))
                    throw new ValidationException($"Nội dung chi tiết {sectionDto.SectionOrder} không được rỗng.");
                if (sanitizedBodyHtml.Contains("data:image", StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException(
                        $"Nội dung mục {sectionDto.SectionOrder} chứa ảnh nhúng base64. Vui lòng tải ảnh lên hệ thống thay vì nhúng trực tiếp.");

                var section = new NewsContentSection
                {
                    NewsTranslationId = translation.NewsTranslationId,
                    SectionOrder      = sectionDto.SectionOrder,
                    SectionTitle      = sanitizedSectionTitle,
                    SectionBodyHtml   = sanitizedBodyHtml,
                    SectionBodyText   = bodyText,
                    CreatedAt         = now,
                    UpdatedAt         = null
                };
                _dbContext.NewsContentSections.Add(section);
                await _dbContext.SaveChangesAsync(cancellationToken); // get SectionId

                // Step 12: Create section files
                if (sectionDto.SectionFiles is { Count: > 0 })
                {
                    foreach (var fileDto in sectionDto.SectionFiles)
                    {
                        var sectionFile = new NewsSectionFile
                        {
                            SectionId    = section.SectionId,
                            FileId       = fileDto.FileId,
                            UsageType    = fileDto.UsageType.ToUpperInvariant(),
                            DisplayOrder = fileDto.DisplayOrder
                        };
                        _dbContext.NewsSectionFiles.Add(sectionFile);
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            // Step 13: Notify Staff Leaders of the same campus
            var authorName = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == currentUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? "Nhân viên";

            var staffLeaderIds = await _dbContext.Users
                .AsNoTracking()
                .Where(u =>
                    u.SubRole == UserSubRoles.Leader &&
                    u.PrimaryCampusId == visitInstance.CampusId &&
                    u.Status == UserStatuses.Active)
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            if (staffLeaderIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active Staff Leader found for campus {CampusId} to notify about news {NewsId}.",
                    visitInstance.CampusId, news.NewsId);
            }
            else
            {
                foreach (var leaderId in staffLeaderIds)
                {
                    _dbContext.Notifications.Add(new Notification
                    {
                        RecipientUserId  = leaderId,
                        NotificationType = "NEWS_PENDING_REVIEW",
                        Title            = "Tin tức mới chờ duyệt",
                        Message          = $"{authorName} mới đăng tin tức cần chờ bạn duyệt: {sanitizedTitle}",
                        RelatedType      = "NEWS",
                        RelatedId        = news.NewsId,
                        // Trang quản lý tin tức lọc đúng 1 bài (có nút "Xem tất cả").
                        ActionUrl        = $"/dashboard/news?newsId={news.NewsId}",
                        IsRead           = false,
                        CreatedAt        = now
                    });
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return new CreateNewsResponse
            {
                Success = true,
                Message = "Đã gửi bài viết, đang chờ Staff Leader duyệt.",
                Data    = new CreateNewsData
                {
                    NewsId          = news.NewsId,
                    VisitInstanceId = request.VisitInstanceId,
                    Status          = NewsConstants.Status.PendingReview,
                    StatusLabel     = NewsConstants.ToVietnameseStatusLabel(NewsConstants.Status.PendingReview),
                    SubmittedAt     = now
                }
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ToSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "tin-tuc";

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var slug  = Regex.Replace(ascii, @"[^a-z0-9\s-]", string.Empty);
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        return slug.Length > 100 ? slug[..100] : slug;
    }

    private static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&apos;", "'");
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
