using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary;

public class ViewFeedbackSummaryQuery : IRequest<PaginatedResult<FeedbackVisitSummaryItem>>
{
    public string? Q { get; set; }
    public string? RatingLevel { get; set; }
    public string? SubmitterRole { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class FeedbackVisitSummaryItem
{
    public ulong VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string VisitTitle { get; set; } = string.Empty;
    public string? CampusName { get; set; }
    public int TotalFeedbacks { get; set; }
    public double AverageRating { get; set; }
    public DateTime? LatestSubmittedAt { get; set; }
    public string? LatestSubmitterName { get; set; }
    public int LowRatingCount { get; set; }
}