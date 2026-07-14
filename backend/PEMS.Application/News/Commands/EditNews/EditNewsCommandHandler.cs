using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.EditNews;

public sealed class EditNewsCommandHandler
    : IRequestHandler<EditNewsCommand, EditNewsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService   _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public EditNewsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService   currentUser,
        IHtmlSanitizerService sanitizer)
    {
        _dbContext   = dbContext;
        _currentUser = currentUser;
        _sanitizer   = sanitizer;
    }

    public async Task<EditNewsResponse> Handle(
        EditNewsCommand   request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;

        // Only Staff (non-leader) and Student may edit their own posts
        var isAllowed = (roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                     || roleCode == RoleCodes.Student;
        if (!isAllowed)
            throw new ForbiddenException("Chỉ Staff thường và Student mới được chỉnh sửa tin tức.");

        // Load news (tracked)
        var news = await _dbContext.News
            .Where(n => n.NewsId == request.NewsId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        if (news.AuthorUserId != currentUserId)
            throw new ForbiddenException("Bạn chỉ có thể chỉnh sửa bài viết của mình.");

        if (news.Status != NewsConstants.Status.PendingReview &&
            news.Status != NewsConstants.Status.Rejected)
            throw new ConflictException("Chỉ có thể chỉnh sửa bài viết đang chờ duyệt hoặc bị từ chối.");

        if (news.RowVersion != request.RowVersion)
            throw new ConflictException("Bài viết đã được cập nhật bởi người khác. Vui lòng tải lại trang.");

        // Validate cover file
        if (request.CoverFileId.HasValue)
        {
            var coverExists = await _dbContext.Files
                .AsNoTracking()
                .AnyAsync(f => f.FileId == request.CoverFileId.Value, cancellationToken);
            if (!coverExists)
                throw new NotFoundException("File ảnh bìa", request.CoverFileId.Value);
        }

        // Validate all referenced section file ids exist
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

        // Sanitize
        var sanitizedTitle   = _sanitizer.Sanitize(request.Title.Trim());
        var sanitizedSummary = _sanitizer.Sanitize((request.Summary ?? string.Empty).Trim());

        if (string.IsNullOrWhiteSpace(sanitizedTitle))
            throw new ValidationException("Tiêu đề không hợp lệ sau khi xử lý.");

        var now         = VietnamTime.Now();
        var wasRejected = news.Status == NewsConstants.Status.Rejected;
        var campusId    = news.CampusId;

        await using var tx = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            // Update news
            news.CoverFileId = request.CoverFileId;
            news.UpdatedAt   = now;
            news.UpdatedBy   = currentUserId;
            news.RowVersion++;

            if (wasRejected)
            {
                news.Status      = NewsConstants.Status.PendingReview;
                news.SubmittedAt = now;
                news.ReviewNote  = null;
                news.ReviewedAt  = null;
                news.ReviewedBy  = null;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Update the requested translation (default: the Vietnamese original)
            var languageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "vi" : request.LanguageCode.Trim();
            var translation = await _dbContext.NewsTranslations
                .Where(t => t.NewsId == request.NewsId && t.LanguageCode == languageCode)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Bản dịch tin tức", request.NewsId);

            translation.Title          = sanitizedTitle;
            translation.Summary        = sanitizedSummary;
            translation.SeoTitle       = sanitizedTitle;
            translation.SeoDescription = sanitizedSummary;
            translation.UpdatedAt      = now;

            // Delete old section files then old sections
            var oldSectionIds = await _dbContext.NewsContentSections
                .AsNoTracking()
                .Where(s => s.NewsTranslationId == translation.NewsTranslationId)
                .Select(s => s.SectionId)
                .ToListAsync(cancellationToken);

            if (oldSectionIds.Count > 0)
            {
                var oldFiles = await _dbContext.NewsSectionFiles
                    .Where(f => oldSectionIds.Contains(f.SectionId))
                    .ToListAsync(cancellationToken);
                _dbContext.NewsSectionFiles.RemoveRange(oldFiles);

                var oldSections = await _dbContext.NewsContentSections
                    .Where(s => s.NewsTranslationId == translation.NewsTranslationId)
                    .ToListAsync(cancellationToken);
                _dbContext.NewsContentSections.RemoveRange(oldSections);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Create new sections (+ their file mappings)
            foreach (var dto in request.ContentSections.OrderBy(s => s.SectionOrder))
            {
                var sTitle    = _sanitizer.Sanitize(dto.SectionTitle.Trim());
                var sBodyHtml = _sanitizer.Sanitize(dto.SectionBodyHtml);
                var sBodyText = ExtractPlainText(sBodyHtml);

                if (string.IsNullOrWhiteSpace(sTitle))
                    throw new ValidationException($"Tiêu đề nội dung {dto.SectionOrder} không hợp lệ.");
                if (string.IsNullOrWhiteSpace(sBodyText))
                    throw new ValidationException($"Nội dung chi tiết {dto.SectionOrder} không được rỗng.");
                if (sBodyHtml.Contains("data:image", StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException(
                        $"Nội dung mục {dto.SectionOrder} chứa ảnh nhúng base64. Vui lòng tải ảnh lên hệ thống thay vì nhúng trực tiếp.");

                var section = new NewsContentSection
                {
                    NewsTranslationId = translation.NewsTranslationId,
                    SectionOrder      = dto.SectionOrder,
                    SectionTitle      = sTitle,
                    SectionBodyHtml   = sBodyHtml,
                    SectionBodyText   = sBodyText,
                    CreatedAt         = now,
                    UpdatedAt         = null
                };
                _dbContext.NewsContentSections.Add(section);
                await _dbContext.SaveChangesAsync(cancellationToken); // get SectionId

                if (dto.SectionFiles is { Count: > 0 })
                {
                    foreach (var fileDto in dto.SectionFiles)
                    {
                        _dbContext.NewsSectionFiles.Add(new NewsSectionFile
                        {
                            SectionId    = section.SectionId,
                            FileId       = fileDto.FileId,
                            UsageType    = string.IsNullOrWhiteSpace(fileDto.UsageType)
                                ? "INLINE_IMAGE"
                                : fileDto.UsageType.ToUpperInvariant(),
                            DisplayOrder = fileDto.DisplayOrder
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Notify Staff Leaders when resubmitting after rejection
            if (wasRejected && campusId.HasValue)
            {
                var authorName = await _dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == currentUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken) ?? "Nhân viên";

                var leaderIds = await _dbContext.Users
                    .AsNoTracking()
                    .Where(u =>
                        u.Role.RoleCode == RoleCodes.Staff &&
                        u.SubRole == UserSubRoles.Leader &&
                        u.PrimaryCampusId == campusId.Value &&
                        u.Status == UserStatuses.Active)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken);

                foreach (var leaderId in leaderIds)
                {
                    _dbContext.Notifications.Add(new Notification
                    {
                        RecipientUserId  = leaderId,
                        NotificationType = "NEWS_PENDING_REVIEW",
                        Title            = "Tin tức đã được cập nhật và nộp lại",
                        Message          = $"{authorName} đã chỉnh sửa và nộp lại bài viết cần bạn duyệt: {sanitizedTitle}",
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

            await tx.CommitAsync(cancellationToken);

            return new EditNewsResponse
            {
                Success        = true,
                Message        = wasRejected
                    ? "Bài viết đã được cập nhật và nộp lại chờ duyệt."
                    : "Bài viết đã được cập nhật thành công.",
                NewStatus      = news.Status,
                NewStatusLabel = NewsConstants.ToVietnameseStatusLabel(news.Status),
                NewRowVersion  = news.RowVersion
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&")
                   .Replace("&lt;",   "<").Replace("&gt;",  ">")
                   .Replace("&quot;", "\"").Replace("&apos;", "'");
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
