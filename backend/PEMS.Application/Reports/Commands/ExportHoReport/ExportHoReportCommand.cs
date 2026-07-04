using System;
using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Reports.Commands.ExportHoReport;

/// <summary>
/// Exports the HO overview report with the exact same filters the dashboard is using.
/// </summary>
public sealed class ExportHoReportCommand : IRequest<ExportHoReportResult>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ulong? CampusId { get; set; }
    public string? VisitScope { get; set; }
    public string? RequestStatus { get; set; }
    public string? CampusInstanceStatus { get; set; }
    public string? VisitType { get; set; }

    /// <summary>PDF | EXCEL | CSV. Default EXCEL.</summary>
    public string? ExportFormat { get; set; }

    /// <summary>Subset of report sections; empty = all. Values:
    /// EXECUTIVE_SUMMARY, APPROVAL_OVERVIEW, CAMPUS_PERFORMANCE,
    /// LIFECYCLE_CLOSE_READINESS, FEEDBACK_QUALITY, CONTENT_EMAIL_EFFECTIVENESS.</summary>
    public List<string> ReportSections { get; set; } = new();
}

public sealed class ExportHoReportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = null!;
    public string FileName { get; set; } = null!;
}
