using System;
using MediatR;

namespace PEMS.Application.Reports.Queries.GetHoReportOverview;

/// <summary>
/// Head Office system-wide report. All filters are optional; null/empty/"ALL" means no filter.
/// Dates are interpreted as Vietnam local dates (UTC+7) and only used when Preset = CUSTOM.
/// </summary>
public sealed class GetHoReportOverviewQuery : IRequest<HoReportOverviewDto>
{
    /// <summary>THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM. Default THIS_YEAR.</summary>
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ulong? CampusId { get; set; }
    /// <summary>ALL | SINGLE_CAMPUS | MULTI_CAMPUS.</summary>
    public string? VisitScope { get; set; }
    /// <summary>ALL | PENDING_APPROVAL | APPROVED | REJECTED | CANCELLED.</summary>
    public string? RequestStatus { get; set; }
    /// <summary>ALL | WAITING_REQUEST_APPROVAL | ... | CLOSED | CANCELLED.</summary>
    public string? CampusInstanceStatus { get; set; }
    public string? VisitType { get; set; }
}
