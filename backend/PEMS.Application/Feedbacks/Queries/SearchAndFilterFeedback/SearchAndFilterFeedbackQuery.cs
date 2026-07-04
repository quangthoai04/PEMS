using MediatR;
using PEMS.Application.Common.Models;

namespace PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback;

public class SearchAndFilterFeedbackQuery : IRequest<PaginatedResult<FeedbackListItem>>
{
    public string? Q { get; set; }
    public string? RatingLevel { get; set; }
    public string? FeedbackType { get; set; }
    public string? SubmitterRole { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ulong? VisitRequestId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class FeedbackListItem
{
    public ulong FeedbackId { get; set; }
    public ulong VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public string VisitTitle { get; set; } = string.Empty;
    public string FeedbackType { get; set; } = string.Empty;
    public ulong? SubmittedByUserId { get; set; }
    public string SubmitterRole { get; set; } = string.Empty;
    public string SubmitterContext { get; set; } = string.Empty;
    public string SubmitterNameSnapshot { get; set; } = string.Empty;
    public ulong? TargetUserId { get; set; }
    public string TargetRole { get; set; } = string.Empty;
    public string TargetContext { get; set; } = string.Empty;
    public string TargetNameSnapshot { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? CommentPreview { get; set; }
    public DateTime SubmittedAt { get; set; }
}