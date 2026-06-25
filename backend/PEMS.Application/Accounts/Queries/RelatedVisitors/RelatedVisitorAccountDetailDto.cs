using System.Collections.Generic;

namespace PEMS.Application.Accounts.Queries.RelatedVisitors;

/// <summary>
/// Read-only detail of a Visitor account shown from the Staff Leader "Related Visitor Accounts"
/// tab, plus the visible related requests that tie the Visitor to the caller's campus. No
/// management capability is ever offered (BR-04); no sensitive columns are exposed (BR-05).
/// </summary>
public sealed class RelatedVisitorAccountDetailDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? Phone { get; init; }
    public string? Nationality { get; init; }
    public PEMS.Domain.Enums.Gender? Gender { get; init; }
    public string Status { get; init; } = default!;
    public string? CreatedVia { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    public IReadOnlyList<RelatedVisitorRequestDto> RelatedRequests { get; init; } =
        new List<RelatedVisitorRequestDto>();

    public bool CanManageStatus { get; init; }
    public bool CanUpdateRole { get; init; }
    public bool CanResetPassword { get; init; }
}

/// <summary>One visit request/campus instance through which the Visitor is related to the campus.</summary>
public sealed class RelatedVisitorRequestDto
{
    public ulong VisitRequestId { get; init; }
    public ulong VisitInstanceId { get; init; }
    public string RequestCode { get; init; } = default!;
    public string DelegationName { get; init; } = default!;
    public string VisitScope { get; init; } = default!;
    public string RequestStatus { get; init; } = default!;
    public string CampusInstanceStatus { get; init; } = default!;
    public DateTime PlannedStartAt { get; init; }
    public DateTime PlannedEndAt { get; init; }
}
