using MediatR;

namespace PEMS.Application.Accounts.Queries.HoCampusCheck;

/// <summary>
/// UC-96 pre-check (HO only). Given a campus, reports whether HO can create a new HO account for
/// it and, if not, why (existing HO + status, or inconsistent multi-HO data). The create modal
/// calls this when HO picks a campus so it can disable the form and show the right warning. The
/// authoritative check still runs server-side in CreateAccount.
/// </summary>
public sealed class GetHoCampusCheckQuery : IRequest<HoCampusCheckDto>
{
    public ulong CampusId { get; init; }
}

/// <summary>Result of the HO campus pre-check. Mirrors the spec §11.1 response shape.</summary>
public sealed class HoCampusCheckDto
{
    public ulong CampusId { get; init; }
    public string? CampusName { get; init; }
    public bool CanCreateHo { get; init; }
    public ExistingHoDto? ExistingHo { get; init; }

    /// <summary>Machine-readable reason the create is blocked, or null when allowed.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Safe, user-facing Vietnamese explanation (always set).</summary>
    public string Message { get; init; } = default!;
}

public sealed class ExistingHoDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Status { get; init; } = default!;
}
