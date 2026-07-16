using System;
using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportV2;

/// <summary>
/// Báo cáo phòng ban của Department Leader (format 3 phần như Staff Leader):
/// (1) bộ lọc thời gian chung, (2) báo cáo nhiệm vụ (thư mời + đơn yêu cầu),
/// (3) báo cáo nhân sự (Dept Leader + Dept Staff). Phần "xuất hóa đơn" dùng
/// endpoint riêng (GetDeptLeaderInvoiceItemsV2 / SendDeptLeaderInvoiceToStaffLeader).
/// </summary>
public sealed class GetDeptLeaderReportV2Query : IRequest<DeptLeaderReportV2Dto>
{
    /// <summary>THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM.</summary>
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class DeptLeaderReportV2Dto
{
    public DateTime GeneratedAt { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Preset { get; set; } = "THIS_YEAR";
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public DeptLeaderV2Tasks Tasks { get; set; } = new();
    public DeptLeaderV2Personnel Personnel { get; set; } = new();
}

// ── Phần 2: báo cáo nhiệm vụ (thư mời + đơn yêu cầu) ─────────────────────────
public sealed class DeptLeaderV2Tasks
{
    public int TotalTasks { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public int NotCompleted { get; set; }
    public int FeedbackCount { get; set; }
    public int FeedbackTotalStars { get; set; }
    public double? FeedbackAverage { get; set; }
    /// <summary>YEAR | MONTH | WEEK | DAY | HOUR — quy tắc chọn giống Staff Leader.</summary>
    public string TrendGranularity { get; set; } = "MONTH";
    public List<DeptLeaderV2TrendPoint> Trend { get; set; } = new();
}

public sealed class DeptLeaderV2TrendPoint
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int Completed { get; set; }
}

// ── Phần 3: nhân sự (Dept Leader + Dept Staff) ───────────────────────────────
public sealed class DeptLeaderV2Personnel
{
    public int TotalStaff { get; set; }
    public double? AverageFeedback { get; set; }
    public List<DeptLeaderV2PersonnelRow> Rows { get; set; } = new();
}

public sealed class DeptLeaderV2PersonnelRow
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    /// <summary>DEPT_LEADER | DEPT_STAFF.</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Số thư mời/đơn yêu cầu người này đã chấp nhận.</summary>
    public int TaskCount { get; set; }
    public double TotalHours { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
    public int DeclinedCount { get; set; }
}

/// <summary>Guard + khoảng thời gian dùng chung cho các endpoint report v2 của Department Leader.</summary>
public static class DeptLeaderReportV2Guard
{
    public static ulong RequireDepartmentLeader(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(currentUser.RoleCode, "DEPARTMENT", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(currentUser.SubRole, "LEADER", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo phòng ban.");
        return currentUser.DepartmentId
            ?? throw new ForbiddenException("Tài khoản chưa được gán phòng ban.");
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

    /// <summary>Độ chi tiết trục thời gian theo độ dài kỳ lọc (giống Staff Leader/HO):
    /// ≥3 năm → YEAR; &gt;3 tháng → MONTH; ≤3 tháng → WEEK; ≤2 tuần → DAY; trong 1 ngày → HOUR.</summary>
    public static string ResolveGranularity(DateTime fromVn, DateTime toVnExclusive)
    {
        var days = (toVnExclusive - fromVn).TotalDays;
        return days >= 3 * 365 ? "YEAR"
            : days > 92 ? "MONTH"
            : days > 14 ? "WEEK"
            : days > 1 ? "DAY"
            : "HOUR";
    }

    /// <summary>Sinh danh sách bucket [start, end) + nhãn hiển thị cho kỳ lọc.</summary>
    public static List<(DateTime Start, DateTime End, string Label)> BuildBuckets(
        DateTime fromVn, DateTime toVnExclusive, string granularity)
    {
        var buckets = new List<(DateTime, DateTime, string)>();
        var cursor = granularity switch
        {
            "YEAR" => new DateTime(fromVn.Year, 1, 1),
            "MONTH" => new DateTime(fromVn.Year, fromVn.Month, 1),
            _ => fromVn,
        };
        while (cursor < toVnExclusive)
        {
            var next = granularity switch
            {
                "YEAR" => cursor.AddYears(1),
                "MONTH" => cursor.AddMonths(1),
                "WEEK" => cursor.AddDays(7),
                "DAY" => cursor.AddDays(1),
                _ => cursor.AddHours(1),
            };
            var label = granularity switch
            {
                "YEAR" => cursor.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "MONTH" => $"T{cursor.Month}/{cursor.Year}",
                "WEEK" => cursor.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture),
                "DAY" => cursor.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture),
                _ => cursor.ToString("HH:00", System.Globalization.CultureInfo.InvariantCulture),
            };
            buckets.Add((cursor, next, label));
            cursor = next;
        }
        return buckets;
    }
}
