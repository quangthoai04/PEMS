using System;
using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetPersonnelStatusImpact;

/// <summary>
/// Spec §14 — what would happen if this member's status changed, evaluated WITHOUT changing anything.
/// The toggle in the UI opens this preview first; nothing is written until the operator confirms.
/// </summary>
public sealed record GetPersonnelStatusImpactQuery(ulong UserId, string TargetStatus)
    : IRequest<GetPersonnelStatusImpactResponse>;

public sealed class StatusImpactBlockerDto
{
    public required string Code { get; init; }
    public int Count { get; init; }
    public required string Message { get; init; }
}

public sealed class StatusImpactWarningDto
{
    public required string Code { get; init; }
    public int Count { get; init; }
    public required string Message { get; init; }
}

public sealed class GetPersonnelStatusImpactResponse
{
    public required ulong UserId { get; init; }
    public required string CurrentStatus { get; init; }
    public required string TargetStatus { get; init; }

    /// <summary>False whenever <see cref="Blockers"/> is non-empty — the command applies the same rule.</summary>
    public bool CanChangeStatus { get; init; }

    /// <summary>Sessions that a disable would revoke immediately.</summary>
    public int ActiveSessionCount { get; init; }

    public IReadOnlyList<StatusImpactBlockerDto> Blockers { get; init; } = Array.Empty<StatusImpactBlockerDto>();
    public IReadOnlyList<StatusImpactWarningDto> Warnings { get; init; } = Array.Empty<StatusImpactWarningDto>();
}
