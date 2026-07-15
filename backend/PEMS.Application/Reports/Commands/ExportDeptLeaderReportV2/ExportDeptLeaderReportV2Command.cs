using System;
using MediatR;

namespace PEMS.Application.Reports.Commands.ExportDeptLeaderReportV2;

/// <summary>
/// Xuất báo cáo phòng ban của Department Leader (PDF / Excel / CSV) theo khoảng thời
/// gian đang lọc; chọn phần TASKS (nhiệm vụ) / PERSONNEL (nhân sự) hoặc cả hai.
/// </summary>
public sealed class ExportDeptLeaderReportV2Command : IRequest<ExportDeptLeaderReportV2Result>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    /// <summary>PDF | EXCEL | CSV.</summary>
    public string? ExportFormat { get; set; }
    /// <summary>TASKS | PERSONNEL — rỗng = cả hai.</summary>
    public string[]? Sections { get; set; }
}

public sealed class ExportDeptLeaderReportV2Result
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/pdf";
    public string FileName { get; set; } = string.Empty;
}
