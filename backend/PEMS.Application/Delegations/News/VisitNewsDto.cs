namespace PEMS.Application.Delegations.News;

/// <summary>One news post attached to a campus instance (UC tin tức). Title/summary live on the
/// vi translation; the body is the first content section's plain text.</summary>
public sealed class VisitNewsDto
{
    public ulong NewsId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string Status { get; set; } = default!;
    public bool IsPublished { get; set; }
    public ulong AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ReviewNote { get; set; }
    public int RowVersion { get; set; }
    /// <summary>True when the caller is the AUTHOR and the post is editable (PENDING_REVIEW / REJECTED).</summary>
    public bool CanEdit { get; set; }
    /// <summary>True when the caller is the Staff Leader of the campus and the post is PENDING_REVIEW.</summary>
    public bool CanApprove { get; set; }
    /// <summary>True when the caller is the Staff Leader of the campus and the post is PENDING_REVIEW.</summary>
    public bool CanReject { get; set; }
}

/// <summary>News list for a campus instance + whether the caller may add a new post.</summary>
public sealed class VisitNewsListDto
{
    public ulong VisitInstanceId { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public List<VisitNewsDto> Items { get; set; } = new();
}
