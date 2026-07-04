namespace PEMS.Application.News.Queries.ViewNewsDetails;

public sealed class ViewNewsDetailsDto
{
    public ulong NewsId { get; init; }
    public ulong? VisitInstanceId { get; init; }
    public ulong? CampusId { get; init; }
    public string? CampusName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public ulong AuthorUserId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public ulong? ReviewedBy { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewNote { get; init; }
    public DateTime? PublishedAt { get; init; }
    public int RowVersion { get; init; }
    public string LanguageCode { get; init; } = "vi";
    /// <summary>All languages this post has a translation for (vi first).</summary>
    public IReadOnlyList<string> AvailableLanguages { get; init; } = Array.Empty<string>();
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Slug { get; init; }
    public NewsFileDto? CoverFile { get; init; }
    public IReadOnlyList<NewsSectionDto> Sections { get; init; } = Array.Empty<NewsSectionDto>();
    public NewsDetailAvailableActionsDto AvailableActions { get; init; } = new();
}

public sealed class NewsSectionDto
{
    public ulong SectionId { get; init; }
    public int SectionOrder { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string SectionBodyHtml { get; init; } = string.Empty;
    public string? SectionBodyText { get; init; }
    public IReadOnlyList<NewsSectionFileDto> Files { get; init; } = Array.Empty<NewsSectionFileDto>();
}

public sealed class NewsFileDto
{
    public ulong FileId { get; init; }
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? FileName { get; init; }
    public string? MimeType { get; init; }
}

public sealed class NewsSectionFileDto
{
    public ulong SectionFileId { get; init; }
    public ulong FileId { get; init; }
    public string? Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? FileName { get; init; }
    public string? MimeType { get; init; }
    public string UsageType { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}

public sealed class NewsDetailAvailableActionsDto
{
    public bool CanViewDetail { get; init; }
    public bool CanEdit { get; init; }
    public bool CanApprove { get; init; }
    public bool CanReject { get; init; }
    public bool CanHide { get; init; }
    public bool CanShow { get; init; }
    /// <summary>May add translations (author while editable, Staff Leader of the campus anytime).</summary>
    public bool CanTranslate { get; init; }
}
