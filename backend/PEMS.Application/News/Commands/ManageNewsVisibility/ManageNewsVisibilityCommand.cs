using MediatR;

namespace PEMS.Application.News.Commands.ManageNewsVisibility;

public sealed record ManageNewsVisibilityCommand : IRequest<ManageNewsVisibilityResponse>
{
    public ulong NewsId { get; init; }
    public string TargetStatus { get; init; } = string.Empty; // "HIDDEN" | "PUBLISHED"
    public int RowVersion { get; init; }
}
