using MediatR;
using System;

namespace PEMS.Application.Reports.Commands.ExportStaffLeaderReport;

public class ExportStaffLeaderReportCommand : IRequest<ExportStaffLeaderReportResult>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? VisitStatus { get; set; }
    public string? RequestStatus { get; set; }
    public string? HostUserId { get; set; }
    public string? DepartmentId { get; set; }
    public string? LogisticsStatus { get; set; }
    public string? FeedbackRating { get; set; }
    public string? ExportFormat { get; set; }
    public string[]? ReportSections { get; set; }
}

public class ExportStaffLeaderReportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
