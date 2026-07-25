using MediatR;

namespace PEMS.Application.News.Commands.SetNewsFeatured;

/// <summary>
/// Standalone featured toggle for an already-reviewed article (Published/Hidden/Rejected) —
/// the checkbox next to Duyệt/Từ chối only sets IsFeatured at review time; this covers
/// un-featuring (or featuring) a post afterwards from its detail page.
/// </summary>
public sealed record SetNewsFeaturedCommand : IRequest<SetNewsFeaturedResponse>
{
    public ulong NewsId { get; init; }
    public bool IsFeatured { get; init; }
    public int RowVersion { get; init; }
}
