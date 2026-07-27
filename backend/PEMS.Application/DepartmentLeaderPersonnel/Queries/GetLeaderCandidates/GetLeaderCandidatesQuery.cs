using System;
using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetLeaderCandidates;

/// <summary>
/// Spec §16 — accounts eligible to become the department's next head. Served by its own endpoint on
/// purpose: the paged personnel list is filtered and paginated for display, so using it as the
/// successor source would silently hide eligible members that happen to be on another page.
/// </summary>
public sealed record GetLeaderCandidatesQuery : IRequest<GetLeaderCandidatesResponse>;

public sealed class LeaderCandidateDto
{
    public required ulong UserId { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class GetLeaderCandidatesResponse
{
    public IReadOnlyList<LeaderCandidateDto> Items { get; init; } = Array.Empty<LeaderCandidateDto>();

    /// <summary>The department's seated head at read time, so the modal can label the outgoing leader.</summary>
    public ulong? CurrentLeaderUserId { get; init; }
    public string? CurrentLeaderName { get; init; }
}
