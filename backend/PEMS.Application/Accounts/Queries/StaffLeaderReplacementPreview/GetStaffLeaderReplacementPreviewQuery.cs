using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Accounts.Queries.StaffLeaderReplacementPreview;

/// <summary>
/// Replace Staff Leader pre-check (HO only). Given a campus, returns the current Staff Leader, the
/// eligible IC-Staff replacement candidates, and whether a replace can run (or why not). Drives
/// the Replace Staff Leader modal. The authoritative checks re-run in the replace command.
/// See REPLACE_STAFF_LEADER spec §14.1.
/// </summary>
public sealed class GetStaffLeaderReplacementPreviewQuery : IRequest<StaffLeaderReplacementPreviewDto>
{
    public ulong CampusId { get; init; }
}

public sealed class StaffLeaderReplacementPreviewDto
{
    public ulong CampusId { get; init; }
    public string? CampusName { get; init; }
    public string? CampusStatus { get; init; }
    public ulong? IcDepartmentId { get; init; }
    public string? IcDepartmentName { get; init; }

    public ReplacementLeaderDto? CurrentLeader { get; init; }
    public IReadOnlyList<ReplacementCandidateDto> EligibleCandidates { get; init; } = new List<ReplacementCandidateDto>();

    public bool CanReplace { get; init; }
    public string? BlockingReason { get; init; }
    public string Message { get; init; } = default!;
}

public sealed class ReplacementLeaderDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public string? SubRole { get; init; }
}

public sealed class ReplacementCandidateDto
{
    public ulong UserId { get; init; }
    public string FullName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public string? SubRole { get; init; }
}
