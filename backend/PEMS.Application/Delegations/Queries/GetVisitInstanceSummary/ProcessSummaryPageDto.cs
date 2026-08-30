using System;
using System.Collections.Generic;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;
using PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSummary;

public sealed class ProcessSummaryPageDto
{
    public ProcessSummaryPermissionDto Permissions { get; set; } = default!;

    /// <summary>
    /// The request this instance belongs to. Needed by the frontend to call the request-scoped
    /// history/timeline endpoint (GET /v2/visit-requests/{visitRequestId}/history) — this page is
    /// keyed by visitInstanceId alone, which is not enough to reach that endpoint on its own.
    /// </summary>
    public ulong VisitRequestId { get; set; }

    public VisitProcessRequestSummaryDto? RequestSummary { get; set; }
    public List<AgendaItemDto> AgendaSummary { get; set; } = new();
    public List<VisitParticipantListItemDto> ParticipantSummary { get; set; } = new();
    public List<ContributionLogisticsItemDto> LogisticsSummary { get; set; } = new();

    /// <summary>
    /// Real workspace state (spec §5.3/§7), reusing the same DTOs the Contribution Page uses so the
    /// two screens can never disagree about whether a visit instance has minutes/media/news. This
    /// page is read-only end to end, so every edit/upload/create-capability flag on these DTOs is
    /// always forced false here regardless of what the underlying business rule would grant a
    /// Contribution-page caller — see GetVisitInstanceSummaryQueryHandler.
    /// </summary>
    public MinutesContributionDto? MinutesSummary { get; set; }
    public MediaContributionDto? MediaSummary { get; set; }
    public NewsContributionDto? NewsSummary { get; set; }
}

public sealed class ProcessSummaryPermissionDto
{
    public bool CanViewSummaryPage { get; set; }
    public string Relation { get; set; } = default!;
    public bool CanViewRequestSummary { get; set; }
    public bool CanViewAgendaSummary { get; set; }
    public bool CanViewParticipantSummary { get; set; }
    public bool CanViewLogisticsSummary { get; set; }
    public bool CanViewMinutesSummary { get; set; }
    public bool CanViewMediaSummary { get; set; }
    public bool CanViewNewsSummary { get; set; }
    public bool CanViewFeedbackSummary { get; set; }
    public bool CanViewTimeline { get; set; }
    public bool IsReadOnly { get; set; } = true;
    public string InstanceStatus { get; set; } = default!;
    public string? CampusName { get; set; }
    public string DelegationName { get; set; } = default!;
    public string? HostName { get; set; }
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
}
