using System;
using MediatR;

namespace PEMS.Application.Reports.Commands.ExportStaffLeaderReportV2;

/// <summary>
/// Xuất báo cáo campus 3 phần của Staff Leader (PDF / Excel / CSV) đúng theo khoảng
/// thời gian đang lọc; có thể chọn xuất phần 1/2/3 hoặc tất cả.
/// </summary>
public sealed class ExportStaffLeaderReportV2Command : IRequest<ExportStaffLeaderReportV2Result>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>PDF | EXCEL | CSV.</summary>
    public string? ExportFormat { get; set; }
    /// <summary>VISITS | PERSONNEL | DEPARTMENTS — rỗng = xuất cả 3 phần.</summary>
    public string[]? Sections { get; set; }
}

public sealed class ExportStaffLeaderReportV2Result
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
    public string FileName { get; set; } = string.Empty;
}
