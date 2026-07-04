using System;
using System.Collections.Generic;

namespace PEMS.Application.PublicContent.Queries.ViewPublicNewsDetail;

public sealed class PublicNewsDetailDto
{
    public ulong NewsId { get; set; }
    public string? Slug { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AuthorName { get; set; }
    /// <summary>Language of the returned content (after fallback resolution).</summary>
    public string LanguageCode { get; set; } = "vi";
    /// <summary>All languages this post has a translation for.</summary>
    public IReadOnlyList<string> AvailableLanguages { get; set; } = Array.Empty<string>();
    public IReadOnlyList<PublicNewsSectionDto> Sections { get; set; } = Array.Empty<PublicNewsSectionDto>();
}

public sealed class PublicNewsSectionDto
{
    public ulong SectionId { get; set; }
    public string? SectionTitle { get; set; }
    public string? SectionBodyHtml { get; set; }
    public string? SectionBodyText { get; set; }
    public int DisplayOrder { get; set; }
    public IReadOnlyList<PublicNewsMediaDto> Files { get; set; } = Array.Empty<PublicNewsMediaDto>();
}

public sealed class PublicNewsMediaDto
{
    public ulong FileId { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int DisplayOrder { get; set; }
}
