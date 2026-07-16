using System;
using MediatR;

namespace PEMS.Application.Reports.Commands.ExportHoReportV2;

/// <summary>
/// Xuất báo cáo hệ thống của HO (PDF / Excel / CSV) theo khoảng thời gian đang lọc;
/// chọn phần OVERVIEW (tổng quan + campus) / PARTNERS (đối tác) hoặc cả hai.
/// </summary>
public sealed class ExportHoReportV2Command : IRequest<ExportHoReportV2Result>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>PDF | EXCEL | CSV.</summary>
    public string? ExportFormat { get; set; }
    /// <summary>OVERVIEW | PARTNERS — rỗng = cả hai.</summary>
    public string[]? Sections { get; set; }
}

public sealed class ExportHoReportV2Result
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
    public string FileName { get; set; } = string.Empty;
}
