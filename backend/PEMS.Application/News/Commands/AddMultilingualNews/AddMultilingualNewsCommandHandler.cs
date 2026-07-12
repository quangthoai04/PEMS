using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.News;

using PEMS.Application.Common;
namespace PEMS.Application.News.Commands.AddMultilingualNews;

public sealed class AddMultilingualNewsCommandHandler
    : IRequestHandler<AddMultilingualNewsCommand, AddMultilingualNewsResponse>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHtmlSanitizerService _sanitizer;

    public AddMultilingualNewsCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHtmlSanitizerService sanitizer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sanitizer = sanitizer;
    }

    public async Task<AddMultilingualNewsResponse> Handle(
        AddMultilingualNewsCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUser.UserId
            ?? throw new ForbiddenException("Bạn chưa đăng nhập.");

        var roleCode = _currentUser.RoleCode ?? string.Empty;
        var subRole  = _currentUser.SubRole  ?? string.Empty;
        var campusId = _currentUser.PrimaryCampusId;

        var news = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.NewsId == request.NewsId)
            .Select(n => new { n.NewsId, n.CampusId, n.AuthorUserId, n.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tin tức", request.NewsId);

        // Staff Leader (same campus) may translate at any time — they are the reviewer.
        // The author may translate only while the post is still under their control
        // (PENDING_REVIEW / REJECTED) so unreviewed content cannot reach a published post.
        var isLeaderOfCampus = roleCode == RoleCodes.Staff
                            && subRole == UserSubRoles.Leader
                            && news.CampusId == campusId;
        var isAuthor = ((roleCode == RoleCodes.Staff && subRole == UserSubRoles.Staff)
                        || roleCode == RoleCodes.Student)
                       && news.AuthorUserId == currentUserId;
        var authorMayTranslate = isAuthor
            && (news.Status == NewsConstants.Status.PendingReview
             || news.Status == NewsConstants.Status.Rejected);

        if (!isLeaderOfCampus && !authorMayTranslate)
            throw new ForbiddenException("Bạn không có quyền thêm bản dịch cho bài viết này.");

        var languageCode = request.LanguageCode.Trim();
        if (!NewsConstants.Languages.Allowed.Contains(languageCode))
            throw new ValidationException(
                $"Ngôn ngữ '{languageCode}' không được hỗ trợ. Các ngôn ngữ hợp lệ: {string.Join(", ", NewsConstants.Languages.Allowed)}.");

        var exists = await _dbContext.NewsTranslations
            .AsNoTracking()
            .AnyAsync(t => t.NewsId == request.NewsId && t.LanguageCode == languageCode, cancellationToken);
        if (exists)
            throw new ConflictException($"Bài viết đã có bản dịch '{languageCode}'. Vui lòng chỉnh sửa bản dịch hiện có.");

        if (request.Sections.Count == 0)
            throw new ValidationException("Bản dịch phải có ít nhất một mục nội dung.");

        var sanitizedTitle   = _sanitizer.Sanitize(request.Title.Trim());
        var sanitizedSummary = _sanitizer.Sanitize((request.Summary ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(sanitizedTitle))
            throw new ValidationException("Tiêu đề bản dịch không hợp lệ.");

        // Validate referenced file ids (explicit section files, if any)
        var explicitFileIds = request.Sections
            .Where(s => s.SectionFiles != null)
            .SelectMany(s => s.SectionFiles!)
            .Select(f => f.FileId)
            .Distinct()
            .ToList();
        if (explicitFileIds.Count > 0)
        {
            var existingIds = await _dbContext.Files
                .AsNoTracking()
                .Where(f => explicitFileIds.Contains(f.FileId))
                .Select(f => f.FileId)
                .ToListAsync(cancellationToken);
            var missing = explicitFileIds.FirstOrDefault(id => !existingIds.Contains(id));
            if (missing != default)
                throw new NotFoundException("File đính kèm", missing);
        }

        // Source translation to copy image mappings from (by section order)
        Dictionary<int, List<NewsSectionFile>> copyMap = new();
        var copyLang = request.CopySectionFilesFromLanguage?.Trim();
        if (!string.IsNullOrEmpty(copyLang))
        {
            var sourceTranslationId = await _dbContext.NewsTranslations
                .AsNoTracking()
                .Where(t => t.NewsId == request.NewsId && t.LanguageCode == copyLang)
                .Select(t => (ulong?)t.NewsTranslationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceTranslationId.HasValue)
            {
                var sourceSections = await _dbContext.NewsContentSections
                    .AsNoTracking()
                    .Where(s => s.NewsTranslationId == sourceTranslationId.Value)
                    .Select(s => new { s.SectionId, s.SectionOrder })
                    .ToListAsync(cancellationToken);

                var sourceSectionIds = sourceSections.Select(s => s.SectionId).ToList();
                var sourceFiles = sourceSectionIds.Count > 0
                    ? await _dbContext.NewsSectionFiles
                        .AsNoTracking()
                        .Where(sf => sourceSectionIds.Contains(sf.SectionId))
                        .Select(sf => new { sf.SectionId, sf.FileId, sf.UsageType, sf.DisplayOrder })
                        .ToListAsync(cancellationToken)
                    : [];

                var orderBySectionId = sourceSections.ToDictionary(s => s.SectionId, s => s.SectionOrder);
                foreach (var f in sourceFiles)
                {
                    var order = orderBySectionId[f.SectionId];
                    if (!copyMap.TryGetValue(order, out var list))
                        copyMap[order] = list = new List<NewsSectionFile>();
                    list.Add(new NewsSectionFile
                    {
                        FileId = f.FileId,
                        UsageType = f.UsageType,
                        DisplayOrder = f.DisplayOrder
                    });
                }
            }
        }

        var now = VietnamTime.Now();

        await using var tx = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var baseSlug = ToSlug(sanitizedTitle);
            var slug = string.IsNullOrEmpty(baseSlug) || baseSlug == "tin-tuc"
                ? $"news-{request.NewsId}-{languageCode.ToLowerInvariant()}"
                : $"{baseSlug}-{request.NewsId}";

            var translation = new NewsTranslation
            {
                NewsId         = request.NewsId,
                LanguageCode   = languageCode,
                Title          = sanitizedTitle,
                Slug           = slug,
                Summary        = string.IsNullOrWhiteSpace(sanitizedSummary) ? null : sanitizedSummary,
                SeoTitle       = _sanitizer.Sanitize((request.SeoTitle ?? request.Title).Trim()),
                SeoDescription = _sanitizer.Sanitize((request.SeoDescription ?? request.Summary ?? string.Empty).Trim()),
                CreatedAt      = now,
                UpdatedAt      = null
            };
            _dbContext.NewsTranslations.Add(translation);
            await _dbContext.SaveChangesAsync(cancellationToken); // get NewsTranslationId

            foreach (var dto in request.Sections.OrderBy(s => s.SectionOrder))
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

                // Explicit files win; otherwise copy the source translation's mappings.
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
                else if (copyMap.TryGetValue(dto.SectionOrder, out var copied))
                {
                    foreach (var c in copied)
                    {
                        _dbContext.NewsSectionFiles.Add(new NewsSectionFile
                        {
                            SectionId    = section.SectionId,
                            FileId       = c.FileId,
                            UsageType    = c.UsageType,
                            DisplayOrder = c.DisplayOrder
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new AddMultilingualNewsResponse
            {
                Success       = true,
                Message       = $"Đã thêm bản dịch '{languageCode}' cho bài viết.",
                NewsId        = request.NewsId,
                LanguageCode  = languageCode,
                TranslationId = translation.NewsTranslationId
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
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
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&")
                   .Replace("&lt;",   "<").Replace("&gt;",  ">")
                   .Replace("&quot;", "\"").Replace("&apos;", "'");
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
