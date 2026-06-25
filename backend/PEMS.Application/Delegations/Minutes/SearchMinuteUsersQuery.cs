using MediatR;

namespace PEMS.Application.Delegations.Minutes;

/// <summary>
/// Search-as-you-type over real system <c>users</c> so the Host can add a participant who has an
/// account but was not auto-filled (Phần 1.4 / Phần 8). Scoped: the caller must be able to edit the
/// minutes of <paramref name="VisitInstanceId"/>, so this is not an open user directory.
/// </summary>
public sealed record SearchMinuteUsersQuery(ulong VisitInstanceId, string? Query)
    : IRequest<List<MinuteUserSearchDto>>;

/// <summary>One picked-able system user for the manual-add dropdown.</summary>
public sealed class MinuteUserSearchDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Organization { get; set; }
}
