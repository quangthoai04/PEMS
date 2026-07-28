using System;
using System.Collections.Generic;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;

public class DeptLeaderReportOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public DeptLeaderFilterSummary FilterSummary { get; set; } = new();
    public DeptLeaderKpis Kpis { get; set; } = new();
    public List<DeptLeaderAttentionItem> AttentionItems { get; set; } = new();
    public List<DeptLeaderTaskPipelineItem> TaskStatusPipeline { get; set; } = new();
    public List<DeptLeaderWorkTypeItem> WorkTypeDistribution { get; set; } = new();
    public List<DeptLeaderMonthlyTrend> MonthlyTrend { get; set; } = new();
    public List<DeptLeaderStaffPerformance> StaffPerformance { get; set; } = new();
    public List<DeptLeaderPendingTask> PendingTasks { get; set; } = new();
    public int PendingTasksTotal { get; set; }
    public List<DeptLeaderProposalChange> ProposalChanges { get; set; } = new();
    public List<DeptLeaderHandoverItem> HandoverSummary { get; set; } = new();
    public int HandoverTotal { get; set; }
    public List<DeptLeaderIncidentItem> IncidentSummary { get; set; } = new();
    public DeptLeaderFeedbackSummary FeedbackSummary { get; set; } = new();
}

public class DeptLeaderFilterSummary
{
    public string Preset { get; set; } = "THIS_MONTH";
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public string LogisticsStatus { get; set; } = "ALL";
    public string ItemType { get; set; } = "ALL";
    public string AssignedUserId { get; set; } = "ALL";
    public string? AssignedUserName { get; set; }
    public string DueStatus { get; set; } = "ALL";
    public string HandoverStatus { get; set; } = "ALL";
    public string FeedbackRating { get; set; } = "ALL";
    public string DepartmentName { get; set; } = string.Empty;
    public string CampusName { get; set; } = string.Empty;
    public string? GeneratedByName { get; set; }
}

public class DeptLeaderKpis
{
    public int NewRequests { get; set; }
    public int WaitingAssignment { get; set; }
    public int WaitingStaffResponse { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Declined { get; set; }
    public int Overdue { get; set; }
    public int MissingHandoverSignature { get; set; }
    public double? AverageResponseHours { get; set; }
    public double? AverageFeedbackRating { get; set; }
}

public class DeptLeaderAttentionItem
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>DANGER | WARNING | INFO | SUCCESS.</summary>
    public string Severity { get; set; } = "INFO";
    public string Description { get; set; } = string.Empty;
    /// <summary>Tab/section id the "Xem" button should jump to on the UI.</summary>
    public string TargetSection { get; set; } = string.Empty;
}

public class DeptLeaderTaskPipelineItem
{
    public string Status { get; set; } = string.Empty;
    public string LabelVi { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class DeptLeaderWorkTypeItem
{
    public string ItemType { get; set; } = string.Empty;
    public string LabelVi { get; set; } = string.Empty;
    public int Count { get; set; }
    public int QuantityTotal { get; set; }
    public double Percentage { get; set; }
}

public class DeptLeaderMonthlyTrend
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
}

public class DeptLeaderStaffPerformance
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int PendingResponseCount { get; set; }
    public int AcceptedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int DeclinedCount { get; set; }
    public int OverdueCount { get; set; }
    public double CompletionRate { get; set; }
    public double? AverageResponseHours { get; set; }
}

public class DeptLeaderPendingTask
{
    public ulong LogisticsItemId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public string? AssignedToName { get; set; }
    public double WaitingHours { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string? DetailUrl { get; set; }
}

public class DeptLeaderProposalChange
{
    public ulong LogisticsItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ProposedByName { get; set; } = string.Empty;
    public int? ProposedQuantity { get; set; }
    public DateTime? ProposedUsageStartAt { get; set; }
    public DateTime? ProposedUsageEndAt { get; set; }
    public string? ProposalNote { get; set; }
    public string ProposalStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DeptLeaderHandoverItem
{
    public ulong LogisticsItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string VisitCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    /// <summary>BORROW | RETURN.</summary>
    public string HandoverType { get; set; } = string.Empty;
    public bool BorrowerSigned { get; set; }
    public bool ProviderSigned { get; set; }
    public string? ItemCondition { get; set; }
    public string? ConditionNote { get; set; }
    public ulong? AttachmentFileId { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
}

public class DeptLeaderIncidentItem
{
    public string ItemType { get; set; } = string.Empty;
    public string ItemTypeLabelVi { get; set; } = string.Empty;
    /// <summary>Representative item name (most recent item with an issue in this group).</summary>
    public string ItemName { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int DamagedCount { get; set; }
    public int MissingCount { get; set; }
    public int NeedActionCount { get; set; }
    public string? LatestNote { get; set; }
}

public class DeptLeaderFeedbackByType
{
    public string ItemType { get; set; } = string.Empty;
    public string LabelVi { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int FeedbackCount { get; set; }
}

public class DeptLeaderFeedbackEntry
{
    public ulong FeedbackId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string DelegationName { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class DeptLeaderFeedbackSummary
{
    public double? AverageRating { get; set; }
    public int TotalFeedbacks { get; set; }
    public int LowFeedbackCount { get; set; }
    public List<DeptLeaderFeedbackByType> FeedbackByItemType { get; set; } = new();
    public List<DeptLeaderFeedbackEntry> LowRatedItems { get; set; } = new();
    public List<DeptLeaderFeedbackEntry> RecentFeedbacks { get; set; } = new();
}

/// <summary>Shared Vietnamese labels for the Department Leader report + invoice.</summary>
public static class DeptLeaderReportLabels
{
    public static string ItemTypeLabelVi(string? itemType) => itemType?.Trim().ToUpperInvariant() switch
    {
        "ROOM" => "Phòng / địa điểm",
        "TRANSPORT" => "Phương tiện / xe",
        "MEAL" => "Trà nước / đồ ăn",
        "EQUIPMENT" => "Thiết bị",
        "BANNER" => "Banner / ấn phẩm",
        "LED" => "LED / màn hình",
        "DEPARTMENT" => "Phòng ban (chung)",
        _ => "Khác",
    };

    public static string StatusLabelVi(string? status) => status?.Trim().ToUpperInvariant() switch
    {
        "REQUESTED" => "Yêu cầu mới",
        "CHANGE_PROPOSED" => "Đề xuất thay đổi",
        "ASSIGNED" => "Chờ nhân sự phản hồi",
        "ACCEPTED" => "Đã nhận",
        "IN_PROGRESS" => "Đang xử lý",
        "DONE" => "Hoàn thành",
        "REJECTED" => "Phòng ban từ chối",
        "DECLINED" => "Nhân sự từ chối",
        "CANCELLED" => "Đã hủy",
        _ => status ?? "—",
    };
}
