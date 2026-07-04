using MediatR;
using System;

namespace PEMS.Application.Reports.Queries.GetStaffLeaderReportOverview;

public sealed class GetStaffLeaderReportOverviewQuery : IRequest<StaffLeaderReportOverviewDto>
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
}
