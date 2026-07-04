using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.ViewPublicNewsDetail;

public sealed class ViewPublicNewsDetailQueryHandler : IRequestHandler<ViewPublicNewsDetailQuery, PublicNewsDetailDto>
{
    private readonly IApplicationDbContext _dbContext;

    public ViewPublicNewsDetailQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PublicNewsDetailDto> Handle(ViewPublicNewsDetailQuery request, CancellationToken cancellationToken)
    {
        var news = await _dbContext.News
            .AsNoTracking()
            .Where(n => n.NewsId == request.NewsId && n.Status == NewsConstants.Status.Published)
            .FirstOrDefaultAsync(cancellationToken);

        if (news == null)
        {
            throw new NotFoundException("Bài viết", request.NewsId);
        }

        var translations = await _dbContext.NewsTranslations
            .AsNoTracking()
            .Where(t => t.NewsId == request.NewsId)
            .Include(t => t.Sections)
                .ThenInclude(s => s.SectionFiles)
                    .ThenInclude(sf => sf.File)
            .ToListAsync(cancellationToken);

        var requestedLang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? "vi"
            : request.LanguageCode.Trim();

        var translation =
            translations.FirstOrDefault(t => string.Equals(t.LanguageCode, requestedLang, StringComparison.OrdinalIgnoreCase))
            ?? translations.FirstOrDefault(t => t.LanguageCode == "vi")
            ?? translations.FirstOrDefault();

        var availableLanguages = translations
            .Select(t => t.LanguageCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(l => l == "vi" ? 0 : 1)
            .ThenBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserId == news.AuthorUserId)
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        // Images are streamed through the anonymous-safe backend proxy (only files of
        // published news resolve there) — Drive webViewLink does not render in <img>.
        string? thumbnailUrl = news.CoverFileId.HasValue
            ? $"/api/public/news-files/{news.CoverFileId.Value}"
            : null;

        var dto = new PublicNewsDetailDto
        {
            NewsId = news.NewsId,
            Slug = translation?.Slug,
            Title = translation?.Title ?? string.Empty,
            Summary = translation?.Summary,
            ThumbnailUrl = thumbnailUrl,
            PublishedAt = news.PublishedAt,
            AuthorName = users.TryGetValue(news.AuthorUserId, out var name) ? name : null,
            LanguageCode = translation?.LanguageCode ?? "vi",
            AvailableLanguages = availableLanguages,
            Sections = translation?.Sections
                .OrderBy(s => s.SectionOrder)
                .Select(s => new PublicNewsSectionDto
                {
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    SectionBodyHtml = s.SectionBodyHtml,
                    SectionBodyText = s.SectionBodyText,
                    DisplayOrder = s.SectionOrder,
                    Files = s.SectionFiles
                        .OrderBy(sf => sf.DisplayOrder)
                        .Select(sf => new PublicNewsMediaDto
                        {
                            FileId = sf.FileId,
                            FileName = sf.File.OriginalFilename,
                            MimeType = sf.File.MimeType,
                            Url = $"/api/public/news-files/{sf.FileId}",
                            ThumbnailUrl = sf.File.ThumbnailUrl,
                            DisplayOrder = sf.DisplayOrder
                        })
                        .ToList()
                })
                .ToList() ?? new List<PublicNewsSectionDto>()
        };

        return dto;
    }
}
