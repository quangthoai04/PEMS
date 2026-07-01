using System;
using System.Collections.Generic;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;

/// <summary>
/// Payload for the Contribution Page (spec §5.3 / §7 / §10.3): the security-checked permission
/// flags, a read-only summary of the visit (request + agenda + participants + scoped logistics),
/// and the workspace section status (minutes / media / news). The backend is the single source of
/// truth — the frontend renders sections purely from these booleans and every mutating endpoint
/// re-validates server-side.
/// </summary>
public sealed class ContributionPageDto
{
    public ContributionPermissionDto Permissions { get; set; } = default!;
    public VisitContributionSummaryDto Summary { get; set; } = default!;
    public ContributionWorkspaceStatusDto Workspace { get; set; } = default!;
}

public sealed class ContributionPermissionDto
{
    /// <summary>Always true here — the handler throws 403/404 when the caller is not allowed in.</summary>
    public bool CanViewContributionPage { get; set; }

    /// <summary>HOST | IC_SUPPORT | DEPARTMENT_RELATED | STUDENT_RELATED. Display/telemetry only — never an auth input.</summary>
    public string Relation { get; set; } = default!;
    public string? ParticipantRole { get; set; }
    public string? ParticipantStatus { get; set; }

    public bool CanViewRequestSummary { get; set; }
    public bool CanViewAgendaSummary { get; set; }
    public bool CanViewParticipantSummary { get; set; }

    public bool CanViewLogisticsSummary { get; set; }
    public bool CanViewRelatedLogisticsOnly { get; set; }
    public bool CanViewFullLogisticsSummary { get; set; }

    public bool CanViewMinutes { get; set; }
    public bool CanEditMinutes { get; set; }

    public bool CanViewMedia { get; set; }
    public bool CanUploadMedia { get; set; }

    public bool CanViewNews { get; set; }
    public bool CanCreateNews { get; set; }
    public bool CanEditNews { get; set; }

    /// <summary>True when the instance is CLOSED/CANCELLED — the whole workspace is view-only.</summary>
    public bool IsReadOnly { get; set; }
}

/// <summary>
/// Read-only summary shown on the Contribution Page. Reuses the existing process-detail DTO types
/// (<see cref="VisitProcessRequestSummaryDto"/>, <see cref="AgendaItemDto"/>,
/// <see cref="VisitParticipantListItemDto"/>) so the frontend can share read-only renderers.
/// </summary>
public sealed class VisitContributionSummaryDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = default!;
    public string RequestStatus { get; set; } = default!;
    public string InstanceStatus { get; set; } = default!;
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string? CampusName { get; set; }
    public ulong? HostUserId { get; set; }
    public string? HostName { get; set; }
    public int GuestCount { get; set; }

    public VisitProcessRequestSummaryDto? Request { get; set; }
    public List<AgendaItemDto> Agenda { get; set; } = new();
    public List<VisitParticipantListItemDto> Participants { get; set; } = new();
    public List<ContributionLogisticsItemDto> Logistics { get; set; } = new();
}

/// <summary>Lightweight logistics row for the Contribution summary (scoped by permission).</summary>
public sealed class ContributionLogisticsItemDto
{
    public ulong LogisticsItemId { get; set; }
    public string? ItemType { get; set; }
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Priority { get; set; }
    public ulong? RequestedToDepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public ulong? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
}

/// <summary>
/// Phase 1 workspace status (minutes / media / news). The flags mirror the permission DTO so the
/// frontend can render section placeholders; the full editors/lists land in a later phase
/// (<see cref="ContributionSectionStatusDto.Placeholder"/> is true).
/// </summary>
public sealed class ContributionWorkspaceStatusDto
{
    public ContributionSectionStatusDto Minutes { get; set; } = new();
    public ContributionSectionStatusDto Media { get; set; } = new();
    public ContributionSectionStatusDto News { get; set; } = new();
}

public sealed class ContributionSectionStatusDto
{
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    /// <summary>Phase 1 marker — the full editor/list is not wired yet on this page.</summary>
    public bool Placeholder { get; set; } = true;
}
