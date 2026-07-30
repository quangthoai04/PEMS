using MediatR;

namespace PEMS.Application.News.Commands.ApproveNews;

public sealed record ApproveNewsCommand : IRequest<ApproveNewsResponse>
{
    public ulong NewsId { get; init; }
    public string Action { get; init; } = string.Empty; // "APPROVE" | "REJECT"
    public string? Reason { get; init; }
    public int RowVersion { get; init; }
    /// <summary>Null = leave IsFeatured unchanged; otherwise set it (checkbox next to Duyệt/Từ chối).</summary>
    public bool? IsFeatured { get; init; }
    /// <summary>Null = leave IsPinned unchanged; otherwise set it (checkbox next to Duyệt/Từ chối).</summary>
    public bool? IsPinned { get; init; }
}
