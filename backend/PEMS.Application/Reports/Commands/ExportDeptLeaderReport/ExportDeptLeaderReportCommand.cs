using MediatR;
using System;

namespace PEMS.Application.Reports.Commands.ExportDeptLeaderReport;

public class ExportDeptLeaderReportCommand : IRequest<ExportDeptLeaderReportResult>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? LogisticsStatus { get; set; }
    public string? ItemType { get; set; }
    public string? AssignedUserId { get; set; }
    public string? DueStatus { get; set; }
    public string? HandoverStatus { get; set; }
    public string? FeedbackRating { get; set; }
    public string? ExportFormat { get; set; }
    public string[]? ReportSections { get; set; }
}

public class ExportDeptLeaderReportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
