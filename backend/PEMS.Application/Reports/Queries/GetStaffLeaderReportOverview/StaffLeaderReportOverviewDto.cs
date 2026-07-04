using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;

public class StaffLeaderReportOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public StaffLeaderFilterSummary FilterSummary { get; set; }
    public StaffLeaderKpis Kpis { get; set; }
    public List<StaffLeaderAttentionItem> AttentionItems { get; set; }
    public List<StaffLeaderLifecyclePipelineItem> CampusLifecyclePipeline { get; set; }
    public List<StaffLeaderMonthlyTrend> MonthlyTrend { get; set; }
    public List<StaffLeaderHostWorkload> HostWorkload { get; set; }
    public List<StaffLeaderLogisticsByDepartment> LogisticsByDepartment { get; set; }
    public List<StaffLeaderPendingActionRequest> PendingActionRequests { get; set; }
    public List<StaffLeaderCloseReadiness> CloseReadiness { get; set; }
    public StaffLeaderFeedbackSummary FeedbackSummary { get; set; }
    public StaffLeaderNewsMediaSummary NewsMediaSummary { get; set; }
}

public class StaffLeaderFilterSummary
{
    public string Preset { get; set; }
    public string FromDate { get; set; }
    public string ToDate { get; set; }
    public string VisitStatus { get; set; }
    public string RequestStatus { get; set; }
    public string HostUserId { get; set; }
    public string DepartmentId { get; set; }
    public string LogisticsStatus { get; set; }
    public string FeedbackRating { get; set; }
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
    public string Type { get; set; }
    public string Label { get; set; }
    public int Count { get; set; }
}

public class StaffLeaderLifecyclePipelineItem
{
    public string Status { get; set; }
    public string LabelVi { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class StaffLeaderMonthlyTrend
{
    public string Month { get; set; }
    public string MonthLabel { get; set; }
    public int TotalInstances { get; set; }
    public int ClosedInstances { get; set; }
    public int CancelledInstances { get; set; }
}

public class StaffLeaderHostWorkload
{
    public ulong HostUserId { get; set; }
    public string HostName { get; set; }
    public int AssignedCount { get; set; }
    public int Upcoming7Days { get; set; }
    public int BeforeVisitCount { get; set; }
    public int DuringVisitCount { get; set; }
    public int AfterVisitCount { get; set; }
    public double? AverageFeedbackRating { get; set; }
    public int ConflictCount { get; set; }
}

public class StaffLeaderLogisticsByDepartment
{
    public ulong DepartmentId { get; set; }
    public string DepartmentName { get; set; }
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
    public string Type { get; set; }
    public ulong RequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string RequestCode { get; set; }
    public string DelegationName { get; set; }
    public string OrganizationName { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }
    public int GuestCount { get; set; }
    public string Status { get; set; }
    public int WaitingHours { get; set; }
    public string ActionLabel { get; set; }
}

public class StaffLeaderCloseReadiness
{
    public ulong VisitInstanceId { get; set; }
    public string RequestCode { get; set; }
    public string DelegationName { get; set; }
    public string HostName { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public int LogisticsOpenCount { get; set; }
    public int MissingHandoverSignatureCount { get; set; }
    public int OpenActionItemCount { get; set; }
    public bool HasMinutes { get; set; }
    public bool HasPublishedNews { get; set; }
    public bool NewsNotRequired { get; set; }
    public int FeedbackCount { get; set; }
    public bool CanClose { get; set; }
    public List<string> Blockers { get; set; }
}

public class StaffLeaderFeedbackSummary
{
    public double? AverageRating { get; set; }
    public int TotalFeedbacks { get; set; }
    public int LowFeedbackCount { get; set; }
    public List<StaffLeaderRatedVisit> TopRatedVisits { get; set; }
    public List<StaffLeaderRatedVisit> LowRatedVisits { get; set; }
    public List<StaffLeaderRatingByHost> RatingByHost { get; set; }
}

public class StaffLeaderRatedVisit
{
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; }
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
    public DateTime? PlannedStartAt { get; set; }
}

public class StaffLeaderRatingByHost
{
    public ulong HostUserId { get; set; }
    public string HostName { get; set; }
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public class StaffLeaderNewsMediaSummary
{
    public int PublishedNewsCount { get; set; }
    public int PendingNewsCount { get; set; }
    public int MissingNewsCount { get; set; }
    public int NewsNotRequiredCount { get; set; }
    public int MediaUploadedCount { get; set; }
}
