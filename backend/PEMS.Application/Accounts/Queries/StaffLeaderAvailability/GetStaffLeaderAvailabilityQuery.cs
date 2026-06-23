using MediatR;

namespace PEMS.Application.Accounts.Queries.StaffLeaderAvailability;

/// <summary>
/// UC-96 pre-check (HO only). Given a campus, reports whether HO can create a new Staff Leader
/// (Trưởng phòng IC) for it and, if not, why (existing leader + status, or a data inconsistency).
/// The create modal calls this when HO picks a campus so it can disable the form and show the
/// right warning. The authoritative check still runs server-side in CreateAccount.
/// </summary>
public sealed class GetStaffLeaderAvailabilityQuery : IRequest<StaffLeaderAvailabilityDto>
{
    public ulong CampusId { get; init; }
}

/// <summary>Result of the Staff Leader availability check. Mirrors the spec §11.1 response shape.</summary>
public sealed class StaffLeaderAvailabilityDto
{
    public ulong CampusId { get; init; }
    public string? CampusName { get; init; }
    public bool CanCreateStaffLeader { get; init; }
    public ulong? IcDepartmentId { get; init; }
    public string? IcDepartmentName { get; init; }
    public ExistingLeaderDto? ExistingLeader { get; init; }

    /// <summary>Machine-readable reason the create is blocked, or null when allowed.</summary>
    public string? BlockingReason { get; init; }

    /// <summary>Safe, user-facing Vietnamese explanation (always set).</summary>
    public string Message { get; init; } = default!;
}

public sealed class ExistingLeaderDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Status { get; init; } = default!;
}
