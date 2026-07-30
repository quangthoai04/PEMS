using System;
using System.Collections.Generic;

namespace PEMS.Application.PublicContent.Queries.ViewNews;

public sealed class ViewNewsDto
{
    public IReadOnlyList<PublicNewsListItemDto> Items { get; init; } = Array.Empty<PublicNewsListItemDto>();
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class PublicNewsListItemDto
{
    public ulong NewsId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? Summary { get; init; }
    public ulong? CoverFileId { get; init; }
    /// <summary>Anonymous-safe backend URL (streams only files attached to published news).</summary>
    public string? CoverUrl { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string LanguageCode { get; init; } = "vi";
    public bool IsFeatured { get; init; }
    public bool IsPinned { get; init; }
    public ulong? CampusId { get; init; }
    public string? CampusName { get; init; }
    public string? CampusCode { get; init; }
    public bool IsVisitRelated { get; init; }
}
