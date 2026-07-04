using MediatR;
using System;

namespace PEMS.Application.Reports.Queries.GetDeptLeaderReportOverview;

public sealed class GetDeptLeaderReportOverviewQuery : IRequest<DeptLeaderReportOverviewDto>
{
    public string? Preset { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? LogisticsStatus { get; set; }
    public string? ItemType { get; set; }
    public string? Priority { get; set; }
    public string? AssignedUserId { get; set; }
    public string? DueStatus { get; set; }
    public string? HandoverStatus { get; set; }
    public string? FeedbackRating { get; set; }
}
