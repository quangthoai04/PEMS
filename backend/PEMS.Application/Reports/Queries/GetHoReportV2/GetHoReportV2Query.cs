using System;
using System.Collections.Generic;
using MediatR;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Reports.Queries.GetHoReportV2;

/// <summary>
/// Báo cáo hệ thống của Head Office (format 3 phần như Staff Leader):
/// (1) bộ lọc thời gian chung, (2) tổng quan toàn hệ thống + tiến trình tiếp khách
/// theo từng campus + bảng campus, (3) xu hướng đối tác + bảng đối tác theo lượt thăm.
/// </summary>
public sealed class GetHoReportV2Query : IRequest<HoReportV2Dto>
{
    /// <summary>THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM.</summary>
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class HoReportV2Dto
{
    public DateTime GeneratedAt { get; set; }
    public string Preset { get; set; } = "THIS_YEAR";
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public HoV2Overview Overview { get; set; } = new();
    public HoV2Partners Partners { get; set; } = new();
}

// ── Phần 2: tổng quan toàn hệ thống ─────────────────────────────────────────
public sealed class HoV2Overview
{
    public int CampusCount { get; set; }
    public int TotalVisits { get; set; }
    public int TotalGuests { get; set; }
    public int TotalPartners { get; set; }
    public int MultiCampusRequests { get; set; }
    public int SingleCampusRequests { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Rejected { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
    /// <summary>YEAR | MONTH | WEEK | DAY | HOUR — quy tắc chọn giống báo cáo Staff Leader.</summary>
    public string TrendGranularity { get; set; } = "MONTH";
    /// <summary>Danh sách campus (động — không hard-code số lượng) làm series cho biểu đồ.</summary>
    public List<HoV2CampusInfo> Campuses { get; set; } = new();
    public List<HoV2TrendPoint> Trend { get; set; } = new();
    public List<HoV2CampusRow> CampusRows { get; set; } = new();
}

public sealed class HoV2CampusInfo
{
    public ulong CampusId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class HoV2TrendPoint
{
    public string Month { get; set; } = string.Empty;      // key đầu bucket
    public string MonthLabel { get; set; } = string.Empty; // 2026 | T7/2026 | 15/07 | 09:00
    /// <summary>Số đoàn theo campus trong bucket — key là campusId (string).</summary>
    public Dictionary<string, int> ByCampus { get; set; } = new();
}

public sealed class HoV2CampusRow
{
    public ulong CampusId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalVisits { get; set; }
    public int TotalPartners { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
}

// ── Phần 3: đối tác ─────────────────────────────────────────────────────────
public sealed class HoV2Partners
{
    public string TrendGranularity { get; set; } = "MONTH";
    public List<HoV2PartnerTrendPoint> Trend { get; set; } = new();
    public List<HoV2PartnerRow> Rows { get; set; } = new();
}

public sealed class HoV2PartnerTrendPoint
{
    public string Month { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public int VisitsWithPartner { get; set; }
    public int NewPartners { get; set; }
    public int CumulativePartners { get; set; }
}

public sealed class HoV2PartnerRow
{
    public ulong PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string? Country { get; set; }
    public int VisitCount { get; set; }
    public double? FeedbackAverage { get; set; }
    public int FeedbackCount { get; set; }
}

/// <summary>Guard + khoảng thời gian dùng chung cho các endpoint report v2 của HO.</summary>
public static class HoReportV2Guard
{
    public static void RequireHo(ICurrentUserService currentUser)
    {
        if (!currentUser.IsAuthenticated)
            throw new ForbiddenException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");
        if (!string.Equals(currentUser.RoleCode, "HO", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không có quyền xem báo cáo hệ thống.");
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

    /// <summary>Độ chi tiết trục thời gian theo độ dài kỳ lọc (giống Staff Leader):
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
