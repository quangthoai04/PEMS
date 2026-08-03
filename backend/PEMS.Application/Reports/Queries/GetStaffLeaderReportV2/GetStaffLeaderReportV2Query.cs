using System;
using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportV2;

/// <summary>
/// Báo cáo campus của Staff Leader (bản 3 phần): (1) đoàn tiếp khách + tiến độ hợp tác đối tác,
/// (2) nhân sự IC + student, (3) các phòng ban khác. Bộ lọc duy nhất là khoảng thời gian
/// (dùng chung cho cả 3 phần).
/// </summary>
public sealed class GetStaffLeaderReportV2Query : IRequest<StaffLeaderReportV2Dto>
{
    /// <summary>THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM.</summary>
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class StaffLeaderReportV2Dto
{
    public DateTime GeneratedAt { get; set; }
    public string CampusName { get; set; } = string.Empty;
    public string Preset { get; set; } = "THIS_YEAR";
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public StaffLeaderV2Visits Visits { get; set; } = new();
    public StaffLeaderV2Partners Partners { get; set; } = new();
    public StaffLeaderV2Personnel Personnel { get; set; } = new();
    public StaffLeaderV2Departments Departments { get; set; } = new();
    public StaffLeaderV2Expenses Expenses { get; set; } = new();
}

// ── Phần 1: đoàn tiếp khách ─────────────────────────────────────────────────
public sealed class StaffLeaderV2Visits
{
    public int TotalVisits { get; set; }
    public int TotalGuests { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
    public int NotCompleted { get; set; }
    public int FeedbackCount { get; set; }
    public int FeedbackTotalStars { get; set; }
    public double? FeedbackAverage { get; set; }
    public int TotalPartners { get; set; }
    /// <summary>Độ chi tiết trục thời gian của Trend, chọn theo độ dài kỳ lọc:
    /// YEAR (kỳ ≥ 3 năm) | MONTH (&gt; 3 tháng) | WEEK (≤ 3 tháng) | DAY (≤ 2 tuần) | HOUR (trong 1 ngày).</summary>
    public string TrendGranularity { get; set; } = "MONTH";
    public List<StaffLeaderV2VisitTrendPoint> VisitTrend { get; set; } = new();
    public List<StaffLeaderV2PartnerTrendPoint> PartnerTrend { get; set; } = new();
}

public sealed class StaffLeaderV2VisitTrendPoint
{
    public string Month { get; set; } = string.Empty;      // key mốc thời gian (đầu bucket)
    public string MonthLabel { get; set; } = string.Empty; // nhãn hiển thị: 2026 | T7/2026 | 15/07 | 09:00
    public int TotalVisits { get; set; }
    public int CompletedVisits { get; set; }
    public int TotalGuests { get; set; }
}

public sealed class StaffLeaderV2PartnerTrendPoint
{
    public string Month { get; set; } = string.Empty;      // key mốc thời gian (đầu bucket)
    public string MonthLabel { get; set; } = string.Empty; // nhãn hiển thị: 2026 | T7/2026 | 15/07 | 09:00
    public int VisitsWithPartner { get; set; }
    public int NewPartners { get; set; }
    public int CumulativePartners { get; set; }
}

// ── Phần đối tác ─────────────────────────────────────────────────────────────
public sealed class StaffLeaderV2Partners
{
    public int TotalPartners { get; set; }
    public int NewPartnersInPeriod { get; set; }
    public int ActivePartners { get; set; }
    public int VisitsWithPartnerCount { get; set; }
    public double PartnerVisitRatio { get; set; }
    public string TrendGranularity { get; set; } = "MONTH";
    public List<StaffLeaderV2PartnerTrendPoint> Trend { get; set; } = new();
    public List<StaffLeaderV2PartnerTypeStat> PartnersByType { get; set; } = new();
    public List<StaffLeaderV2PartnerStatusStat> PartnersByStatus { get; set; } = new();
    public List<StaffLeaderV2TopPartnerRow> TopPartners { get; set; } = new();
}

public sealed class StaffLeaderV2PartnerTypeStat
{
    public string PartnerType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int VisitCount { get; set; }
}

public sealed class StaffLeaderV2PartnerStatusStat
{
    public string Status { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class StaffLeaderV2TopPartnerRow
{
    public ulong PartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string CooperationStatus { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public int GuestCount { get; set; }
    public double? FeedbackAverage { get; set; }
}

// ── Phần 2: nhân sự IC + student ────────────────────────────────────────────
public sealed class StaffLeaderV2Personnel
{
    public int TotalStaff { get; set; }
    public int TotalStudents { get; set; }
    public double? AverageFeedback { get; set; }
    public List<StaffLeaderV2PersonnelRow> Rows { get; set; } = new();
}

public sealed class StaffLeaderV2PersonnelRow
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>STAFF_LEADER | STAFF | STUDENT.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Staff: số đoàn là host; student: số đoàn đã tham gia.</summary>
    public int VisitCount { get; set; }
    public double TotalHours { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
    public int DeclinedCount { get; set; }
}

// ── Phần 3: các phòng ban khác ──────────────────────────────────────────────
public sealed class StaffLeaderV2Departments
{
    public int TotalDepartments { get; set; }
    public int CompletedTotal { get; set; }
    public int RejectedTotal { get; set; }
    public double? AverageFeedback { get; set; }
    public List<StaffLeaderV2DepartmentRow> Rows { get; set; } = new();
}

public sealed class StaffLeaderV2DepartmentRow
{
    public ulong DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Tổng đơn yêu cầu hậu cần + thư mời tham gia gửi tới phòng ban.</summary>
    public int TotalRequests { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
}

// ── Phần 4: Thống kê chi phí ────────────────────────────────────────────────
public sealed class StaffLeaderV2Expenses
{
    public decimal TotalAmount { get; set; }
    public decimal TotalGeneral { get; set; }
    public decimal TotalLogistics { get; set; }
    public List<StaffLeaderV2ExpenseRow> Rows { get; set; } = new();
}

public sealed class StaffLeaderV2ExpenseRow
{
    public ulong VisitInstanceId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string DelegationName { get; set; } = string.Empty;
    public DateTime? VisitDate { get; set; }
    public decimal GeneralExpense { get; set; }
    public decimal LogisticsExpense { get; set; }
    public decimal TotalExpense { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Guard + khoảng thời gian dùng chung cho các endpoint report v2 của Staff Leader.</summary>
public static class StaffLeaderReportV2Guard
{
    public static ulong RequireStaffLeaderCampus(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(currentUser.RoleCode, "STAFF", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo vận hành campus.");
        return currentUser.PrimaryCampusId
            ?? throw new ForbiddenException("Tài khoản chưa được gán campus chính.");
    }

    public static string NormalizePreset(string? preset)
    {
        var p = preset?.Trim().ToUpperInvariant();
        return p is "THIS_MONTH" or "THIS_QUARTER" or "THIS_YEAR" or "CUSTOM" ? p : "THIS_YEAR";
    }

    /// <summary>Returns [from, toExclusive) theo giờ Việt Nam.</summary>
    public static (DateTime FromVn, DateTime ToVnExclusive) ResolvePeriodVn(
        string preset, DateTime? fromDate, DateTime? toDate, DateTime nowVn)
    {
        switch (preset)
        {
            case "THIS_MONTH":
                var monthStart = new DateTime(nowVn.Year, nowVn.Month, 1);
                return (monthStart, monthStart.AddMonths(1));
            case "THIS_QUARTER":
                var quarterStartMonth = ((nowVn.Month - 1) / 3) * 3 + 1;
                var quarterStart = new DateTime(nowVn.Year, quarterStartMonth, 1);
                return (quarterStart, quarterStart.AddMonths(3));
            case "CUSTOM":
                var from = (fromDate ?? new DateTime(nowVn.Year, 1, 1)).Date;
                var to = (toDate ?? nowVn).Date.AddDays(1);
                if (to <= from) to = from.AddDays(1);
                return (from, to);
            default: // THIS_YEAR
                return (new DateTime(nowVn.Year, 1, 1), new DateTime(nowVn.Year + 1, 1, 1));
        }
    }
}
