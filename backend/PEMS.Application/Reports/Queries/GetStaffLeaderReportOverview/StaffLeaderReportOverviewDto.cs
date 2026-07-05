using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;

public class StaffLeaderReportOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public StaffLeaderFilterSummary FilterSummary { get; set; } = new();
    public StaffLeaderKpis Kpis { get; set; } = new();
    public List<StaffLeaderAttentionItem> AttentionItems { get; set; } = new();
    public List<StaffLeaderLifecyclePipelineItem> CampusLifecyclePipeline { get; set; } = new();
    public List<StaffLeaderMonthlyTrend> MonthlyTrend { get; set; } = new();
    public List<StaffLeaderHostWorkload> HostWorkload { get; set; } = new();
    public List<StaffLeaderLogisticsByDepartment> LogisticsByDepartment { get; set; } = new();
    public List<StaffLeaderPendingActionRequest> PendingActionRequests { get; set; } = new();
    public int PendingActionTotal { get; set; }
    public List<StaffLeaderCloseReadiness> CloseReadiness { get; set; } = new();
    public int CloseReadinessTotal { get; set; }
    public StaffLeaderFeedbackSummary FeedbackSummary { get; set; } = new();
    public StaffLeaderPartnerSummary PartnerSummary { get; set; } = new();
}

public class StaffLeaderFilterSummary
{
    public string Preset { get; set; } = "THIS_YEAR";
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string VisitStatus { get; set; } = "ALL";
    public string RequestStatus { get; set; } = "ALL";
    public string HostUserId { get; set; } = "ALL";
    public string? HostName { get; set; }
    public string DepartmentId { get; set; } = "ALL";
    public string? DepartmentName { get; set; }
    public string LogisticsStatus { get; set; } = "ALL";
    public string FeedbackRating { get; set; } = "ALL";
    public string CampusName { get; set; } = string.Empty;
    public string? GeneratedByName { get; set; }
}

public class StaffLeaderKpis
{
    public int PendingSingleCampusApproval { get; set; }
    public int WaitingHostAssignment { get; set; }
    public int AssignedVisits { get; set; }
    public int BeforeVisit { get; set; }
    public int DuringVisit { get; set; }
    public int AfterVisit { get; set; }
    public int ClosedVisits { get; set; }
    public int OverdueOrNotClosed { get; set; }
    public double? AverageFeedbackRating { get; set; }
    public int TotalGuests { get; set; }
}

public class StaffLeaderAttentionItem
{
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Severity { get; set; } = "INFO";
    public string TargetSection { get; set; } = string.Empty;
}

public class StaffLeaderLifecyclePipelineItem
{
    public string Status { get; set; } = string.Empty;
    public string LabelVi { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class StaffLeaderMonthlyTrend
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int TotalInstances { get; set; }
    public int ClosedInstances { get; set; }
    public int CancelledInstances { get; set; }
    public int ActiveInstances { get; set; }
}

public class StaffLeaderHostWorkload
{
    public ulong HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int Upcoming7Days { get; set; }
    public int BeforeVisitCount { get; set; }
    public int DuringVisitCount { get; set; }
    public int AfterVisitCount { get; set; }
    public double? AverageFeedbackRating { get; set; }
}

public class StaffLeaderLogisticsByDepartment
{
    public ulong DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int Requested { get; set; }
    public int Accepted { get; set; }
    public int InProgress { get; set; }
    public int Done { get; set; }
    public int Rejected { get; set; }
    public int OverdueCount { get; set; }
}

public class StaffLeaderPendingActionRequest
{
    /// <summary>APPROVAL | ASSIGN_HOST.</summary>
    public string Type { get; set; } = string.Empty;
    public ulong RequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string VisitType { get; set; } = string.Empty;
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public int GuestCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public double WaitingHours { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
}

public class StaffLeaderCloseReadiness
{
    public ulong VisitInstanceId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public int LogisticsOpenCount { get; set; }
    public int MissingHandoverSignatureCount { get; set; }
    public int OpenActionItemCount { get; set; }
    public bool HasMinutes { get; set; }
    public bool HasPublishedNews { get; set; }
    public bool NewsNotRequired { get; set; }
    public int FeedbackCount { get; set; }
    public bool CanClose { get; set; }
    public List<string> Blockers { get; set; } = new();
}

public class StaffLeaderFeedbackSummary
{
    public double? AverageRating { get; set; }
    public int TotalFeedbacks { get; set; }
    public int LowFeedbackCount { get; set; }
    public List<StaffLeaderFeedbackEntry> LowFeedbacks { get; set; } = new();
    public List<StaffLeaderFeedbackEntry> GoodFeedbacks { get; set; } = new();
    public List<StaffLeaderRatingByHost> RatingByHost { get; set; } = new();
}

public class StaffLeaderFeedbackEntry
{
    public ulong FeedbackId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? PlannedStartAt { get; set; }
}

public class StaffLeaderRatingByHost
{
    public ulong HostUserId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public class StaffLeaderPartnerSummary
{
    /// <summary>Approved partners owned by this campus (current state).</summary>
    public int TotalPartners { get; set; }
    /// <summary>Approved + cooperation_status ACTIVE (current state).</summary>
    public int ActivePartners { get; set; }
    /// <summary>Profile PENDING_APPROVAL — Staff Leader duyệt hồ sơ partner campus mình.</summary>
    public int PendingApprovalPartners { get; set; }
    /// <summary>Partners created within the report period.</summary>
    public int NewPartnersInPeriod { get; set; }
    /// <summary>Campus instances in the period linked to at least one partner (direct or guest link).</summary>
    public int VisitsWithPartner { get; set; }
    public List<StaffLeaderPartnerTypeCount> PartnersByType { get; set; } = new();
    public List<StaffLeaderTopPartner> TopPartners { get; set; } = new();
}

public class StaffLeaderPartnerTypeCount
{
    public string PartnerType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StaffLeaderTopPartner
{
    public ulong PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string CooperationStatus { get; set; } = string.Empty;
    public string ProfileStatus { get; set; } = string.Empty;
    /// <summary>Distinct campus instances in the period tied to the partner (direct or via guest links).</summary>
    public int VisitCount { get; set; }
    /// <summary>Confirmed guest-member links in the period.</summary>
    public int LinkedGuestCount { get; set; }
}
