using System;
using System.Collections.Generic;
using PEMS.Application.Delegations.Queries.GetVisitProcessDetail;
using PEMS.Application.Delegations.Queries.GetVisitInstanceContribution;
using PEMS.Application.Delegations.Queries.GetVisitInstanceParticipants;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSummary;

public sealed class ProcessSummaryPageDto
{
    public ProcessSummaryPermissionDto Permissions { get; set; } = default!;
    
    public VisitProcessRequestSummaryDto? RequestSummary { get; set; }
    public List<AgendaItemDto> AgendaSummary { get; set; } = new();
    public List<VisitParticipantListItemDto> ParticipantSummary { get; set; } = new();
    public List<ContributionLogisticsItemDto> LogisticsSummary { get; set; } = new();
    
    public ContributionSectionStatusDto MinutesSummary { get; set; } = new();
    public ContributionSectionStatusDto MediaSummary { get; set; } = new();
    public ContributionSectionStatusDto NewsSummary { get; set; } = new();
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
