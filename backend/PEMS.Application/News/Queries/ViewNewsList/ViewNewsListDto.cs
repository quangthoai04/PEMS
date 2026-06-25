namespace PEMS.Application.News.Queries.ViewNewsList;

public sealed class ViewNewsListResponse
{
    public string ViewerMode { get; init; } = string.Empty;
    public bool CanCreateNews { get; init; }
    public IReadOnlyList<ViewNewsListItemDto> Items { get; init; } = Array.Empty<ViewNewsListItemDto>();
    public NewsPaginationDto Pagination { get; init; } = default!;
}

public sealed class NewsPaginationDto
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
}

public sealed class ViewNewsListItemDto
{
    public ulong NewsId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? CoverThumbnailUrl { get; init; }
    public ulong? CampusId { get; init; }
    public string? CampusName { get; init; }
    public ulong? VisitInstanceId { get; init; }
    public ulong AuthorUserId { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public ulong? ReviewedBy { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public NewsAvailableActionsDto AvailableActions { get; init; } = default!;
}

public sealed class NewsAvailableActionsDto
{
    public bool CanViewDetail { get; init; }
    public bool CanEdit { get; init; }
    public bool CanApprove { get; init; }
    public bool CanReject { get; init; }
    public bool CanHide { get; init; }
    public bool CanShow { get; init; }
}
