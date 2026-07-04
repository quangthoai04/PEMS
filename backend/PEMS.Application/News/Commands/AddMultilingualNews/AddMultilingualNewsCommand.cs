using MediatR;

namespace PEMS.Application.News.Commands.AddMultilingualNews;

/// <summary>
/// Adds a translation (news_translations + its sections) to an existing news post.
/// Used both for manual translations and for saving the result of auto-translation.
/// Section image mappings can be copied from an existing translation so images are
/// shared, not re-uploaded.
/// </summary>
public sealed record AddMultilingualNewsCommand : IRequest<AddMultilingualNewsResponse>
{
    public ulong NewsId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public IReadOnlyList<AddMultilingualNewsSectionDto> Sections { get; init; }
        = Array.Empty<AddMultilingualNewsSectionDto>();

    /// <summary>
    /// When set (e.g. "vi"), section file mappings are copied by section order from
    /// that translation for sections that don't provide their own SectionFiles.
    /// </summary>
    public string? CopySectionFilesFromLanguage { get; init; }
}

public sealed record AddMultilingualNewsSectionDto
{
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
    public IReadOnlyList<AddMultilingualNewsSectionFileDto>? SectionFiles { get; init; }
}

public sealed record AddMultilingualNewsSectionFileDto
{
    public ulong FileId { get; init; }
    public string UsageType { get; init; } = "INLINE_IMAGE";
    public int DisplayOrder { get; init; }
}
