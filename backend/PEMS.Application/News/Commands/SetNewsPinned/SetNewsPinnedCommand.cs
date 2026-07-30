using MediatR;

namespace PEMS.Application.News.Commands.SetNewsPinned;

public sealed record SetNewsPinnedCommand : IRequest<SetNewsPinnedResponse>
{
    public ulong NewsId { get; init; }
    public bool IsPinned { get; init; }
    public int RowVersion { get; init; }
}
