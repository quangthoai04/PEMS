using MediatR;

namespace PEMS.Application.DepartmentLeaderPersonnel.Queries.GetMyDepartment;

/// <summary>
/// Spec §8 — the authenticated Department Leader's own department, with its personnel head-count
/// breakdown. Carries NO parameters on purpose: the department is resolved from the caller's verified
/// scope, so there is no id for a client to tamper with (spec §5.1).
/// </summary>
public sealed record GetMyDepartmentQuery : IRequest<GetMyDepartmentResponse>;

/// <summary>
/// Everything the department header card renders. Every value comes from the database — the screen must
/// never fall back to a name cached in local storage (spec §5.7).
/// </summary>
public sealed class GetMyDepartmentResponse
{
    public required ulong DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public required string DepartmentType { get; init; }
    public required string DepartmentStatus { get; init; }

    public required ulong CampusId { get; init; }
    public required string CampusName { get; init; }

    public ulong? CurrentLeaderUserId { get; init; }
    public string? CurrentLeaderName { get; init; }

    /// <summary>All DEPARTMENT accounts in this department, in any status.</summary>
    public int TotalPersonnelCount { get; init; }
    public int ActivePersonnelCount { get; init; }
    public int InactivePersonnelCount { get; init; }
    public int PendingEmailConfirmationCount { get; init; }
    public int LockedPersonnelCount { get; init; }
}
