using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetHoReportOverview;

/// <summary>
/// Aggregated Head Office report. Request-level metrics (approval funnel, trend) are
/// counted by visit_requests.submitted_at; campus-instance metrics (lifecycle, campus
/// performance) are counted by visit_request_campuses.planned_start_at. Operational
/// "needs action now" blocks (pending multi-campus requests, close readiness, attention
/// items) reflect CURRENT state and intentionally ignore the time filter.
/// </summary>
public sealed class HoReportOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public HoReportFilterSummaryDto FilterSummary { get; set; } = new();
    public HoReportKpisDto Kpis { get; set; } = new();
    public List<HoAttentionItemDto> AttentionItems { get; set; } = new();
    public List<HoMonthlyTrendDto> MonthlyTrend { get; set; } = new();
    public HoApprovalBreakdownDto ApprovalBreakdown { get; set; } = new();
    public List<HoCampusPerformanceDto> CampusPerformance { get; set; } = new();
    public List<HoLifecyclePipelineItemDto> LifecyclePipeline { get; set; } = new();
    public List<HoPendingMultiCampusRequestDto> MultiCampusPendingRequests { get; set; } = new();
    public int MultiCampusPendingTotal { get; set; }
    public List<HoCloseReadinessDto> CloseReadiness { get; set; } = new();
    public int CloseReadinessTotal { get; set; }
    public HoFeedbackSummaryDto FeedbackSummary { get; set; } = new();
    public HoContentEmailSummaryDto ContentAndEmailSummary { get; set; } = new();
    public HoPartnerSummaryDto PartnerSummary { get; set; } = new();
}

public sealed class HoReportFilterSummaryDto
{
    public string Preset { get; set; } = "THIS_YEAR";
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public ulong? CampusId { get; set; }
    public string CampusName { get; set; } = "Tất cả cơ sở";
    public string VisitScope { get; set; } = "ALL";
    public string RequestStatus { get; set; } = "ALL";
    public string CampusInstanceStatus { get; set; } = "ALL";
    public string VisitType { get; set; } = "ALL";
    public ulong? GeneratedByUserId { get; set; }
    public string? GeneratedByName { get; set; }
}

public sealed class HoReportKpisDto
{
    /// <summary>Requests submitted in the selected period (after filters).</summary>
    public int TotalRequests { get; set; }
    /// <summary>Multi-campus requests CURRENTLY having at least one campus instance waiting for
    /// its Staff Leader's decision (HO monitor-only; state-based, not time-filtered).</summary>
    public int MultiCampusPending { get; set; }
    public int PendingRequests { get; set; }
    public int ApprovedRequests { get; set; }
    /// <summary>Requests with at least one approved campus and at least one pending/rejected campus.</summary>
    public int PartiallyApprovedRequests { get; set; }
    public int RejectedRequests { get; set; }
    public int CancelledRequests { get; set; }
    /// <summary>Campus instances in the period whose status is not CLOSED/CANCELLED.</summary>
    public int ActiveCampusInstances { get; set; }
    public int ClosedCampusInstances { get; set; }
    /// <summary>Instances past planned_end_at but still in an operational status (not closed).</summary>
    public int OverdueCloseInstances { get; set; }
    public double? AverageDecisionHours { get; set; }
    public double? AverageFeedbackRating { get; set; }
    public int TotalGuests { get; set; }
}

public sealed class HoAttentionItemDto
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public int Count { get; set; }
    /// <summary>INFO | WARNING | DANGER | SUCCESS.</summary>
    public string Severity { get; set; } = "INFO";
    public string Description { get; set; } = string.Empty;
    /// <summary>Anchor id of the section the UI should scroll to.</summary>
    public string TargetSection { get; set; } = string.Empty;
}

public sealed class HoMonthlyTrendDto
{
    /// <summary>yyyy-MM (Vietnam local time).</summary>
    public string Month { get; set; } = null!;
    public string MonthLabel { get; set; } = null!;
    public int TotalRequests { get; set; }
    public int SingleCampusRequests { get; set; }
    public int MultiCampusRequests { get; set; }
    public int Approved { get; set; }
    public int PartiallyApproved { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
    public int TotalGuests { get; set; }
}

public sealed class HoApprovalBreakdownDto
{
    public int Approved { get; set; }
    public int PartiallyApproved { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public int Cancelled { get; set; }
    /// <summary>Percent 0-100 over all requests in the period.</summary>
    public double ApprovalRate { get; set; }
    public double RejectionRate { get; set; }
    public double? AverageDecisionHours { get; set; }
}

public sealed class HoCampusPerformanceDto
{
    public ulong CampusId { get; set; }
    public string CampusCode { get; set; } = null!;
    public string CampusName { get; set; } = null!;
    public int TotalInstances { get; set; }
    public int WaitingRequestApproval { get; set; }
    /// <summary>Campus instances the campus Staff Leader rejected (campus-independent approval).</summary>
    public int Rejected { get; set; }
    public int Assigned { get; set; }
    public int BeforeVisit { get; set; }
    public int DuringVisit { get; set; }
    public int AfterVisit { get; set; }
    public int Closed { get; set; }
    public int Cancelled { get; set; }
    public double? AverageFeedbackRating { get; set; }
    public int OverdueCloseCount { get; set; }
    public int GuestCount { get; set; }
}

public sealed class HoLifecyclePipelineItemDto
{
    public string Status { get; set; } = null!;
    public string LabelVi { get; set; } = null!;
    public int Count { get; set; }
    /// <summary>Percent 0-100 over all instances in the period.</summary>
    public double Percentage { get; set; }
}

public sealed class HoPendingMultiCampusRequestDto
{
    public ulong RequestId { get; set; }
    public string RequestCode { get; set; } = null!;
    public string DelegationName { get; set; } = null!;
    public string OrganizationName { get; set; } = null!;
    public DateTime SubmittedAt { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public int RequestedCampusCount { get; set; }
    public int GuestCount { get; set; }
    public double WaitingHours { get; set; }
    public string Status { get; set; } = null!;
}

public sealed class HoCloseReadinessDto
{
    public ulong VisitInstanceId { get; set; }
    public ulong RequestId { get; set; }
    public string RequestCode { get; set; } = null!;
    public string DelegationName { get; set; } = null!;
    public string CampusName { get; set; } = null!;
    public DateTime PlannedEndAt { get; set; }
    public string? HostName { get; set; }
    public int LogisticsOpenCount { get; set; }
    public int MissingHandoverSignatureCount { get; set; }
    public int OpenActionItemCount { get; set; }
    public bool HasMinutes { get; set; }
    public bool HasPublishedNews { get; set; }
    public bool NewsNotRequired { get; set; }
    public int FeedbackCount { get; set; }
    public bool CanClose { get; set; }
    /// <summary>Blockers per the real close rule (CompleteVisitStage §10):
    /// PLANNED_END_NOT_REACHED | LOGISTICS_OPEN | HANDOVER_SIGNATURE_MISSING | ACTION_ITEMS_OPEN | NEWS_MISSING.</summary>
    public List<string> Blockers { get; set; } = new();
}

public sealed class HoRatedVisitDto
{
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = null!;
    public string CampusName { get; set; } = null!;
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
    public DateTime? PlannedStartAt { get; set; }
}

public sealed class HoCampusRatingDto
{
    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = null!;
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public sealed class HoFeedbackSummaryDto
{
    public double? AverageRating { get; set; }
    public int TotalFeedbacks { get; set; }
    /// <summary>Feedbacks with rating &lt;= 2 in the period.</summary>
    public int LowFeedbackCount { get; set; }
    public List<HoRatedVisitDto> TopRatedVisits { get; set; } = new();
    public List<HoRatedVisitDto> LowRatedVisits { get; set; } = new();
    public List<HoCampusRatingDto> RatingByCampus { get; set; } = new();
}

public sealed class HoPartnerSummaryDto
{
    /// <summary>Approved partners (current state; scoped by campus filter via owner_campus_id).</summary>
    public int TotalPartners { get; set; }
    /// <summary>Approved + cooperation_status ACTIVE (current state).</summary>
    public int ActivePartners { get; set; }
    /// <summary>Profile PENDING_APPROVAL (current state).</summary>
    public int PendingApprovalPartners { get; set; }
    /// <summary>Partners created within the report period.</summary>
    public int NewPartnersInPeriod { get; set; }
    /// <summary>Campus instances in the period linked to at least one partner (direct or guest link).</summary>
    public int VisitsWithPartner { get; set; }
    public List<HoPartnerTypeCountDto> PartnersByType { get; set; } = new();
    public List<HoPartnerCampusCountDto> PartnersByCampus { get; set; } = new();
    public List<HoTopPartnerDto> TopPartners { get; set; } = new();
}

public sealed class HoPartnerTypeCountDto
{
    public string PartnerType { get; set; } = null!;
    public int Count { get; set; }
}

public sealed class HoPartnerCampusCountDto
{
    public ulong CampusId { get; set; }
    public string CampusName { get; set; } = null!;
    public int ApprovedCount { get; set; }
    public int PendingCount { get; set; }
    public int NewInPeriod { get; set; }
}

public sealed class HoTopPartnerDto
{
    public ulong PartnerId { get; set; }
    public string Name { get; set; } = null!;
    public string PartnerType { get; set; } = null!;
    public string? Country { get; set; }
    public string OwnerCampusName { get; set; } = null!;
    public string CooperationStatus { get; set; } = null!;
    /// <summary>Distinct campus instances in the period tied to the partner (direct or via guest links).</summary>
    public int VisitCount { get; set; }
    /// <summary>Confirmed guest-member links in the period.</summary>
    public int LinkedGuestCount { get; set; }
}

public sealed class HoContentEmailSummaryDto
{
    public int PublishedNewsCount { get; set; }
    public int PendingNewsCount { get; set; }
    /// <summary>Instances past DURING_VISIT that neither have a published news nor the news-not-required flag.</summary>
    public int InstancesMissingNewsCount { get; set; }
    public int EmailSentCount { get; set; }
    public int EmailFailedCount { get; set; }
    /// <summary>Percent 0-100 of SENT over SENT+FAILED attempts in the period.</summary>
    public double? EmailDeliveredRate { get; set; }
    public int ActionTokenRespondedCount { get; set; }
    public int ActionTokenExpiredCount { get; set; }
    public int ActionTokenPendingCount { get; set; }
}
