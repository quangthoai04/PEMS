using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Feedbacks.Queries.GetVisitorFeedback;

/// <summary>
/// Backs the "Visitor đã gửi đánh giá" notification (action_type=OPEN_VISITOR_FEEDBACK_MODAL).
/// Only the current Host of the instance or a Staff Leader of its campus may view — mirrors the
/// same audience SubmitVisitFeedbackCommandHandler notifies (CurrentHostUserId + CoordinatorUserId).
/// </summary>
public sealed record GetVisitorFeedbackQuery(ulong VisitInstanceId) : IRequest<GetVisitorFeedbackResponse>;

public sealed class GetVisitorFeedbackResponse
{
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }
    public string RequestCode { get; set; } = default!;
    public string DelegationName { get; set; } = default!;
    public string? OrganizationName { get; set; }
    public string? CampusName { get; set; }
    public string? HostName { get; set; }
    public string InstanceStatus { get; set; } = default!;
    public string? PlannedStartAt { get; set; }
    public string? PlannedEndAt { get; set; }
    public List<VisitorFeedbackItemDto> Feedbacks { get; set; } = new();
}

public sealed class VisitorFeedbackItemDto
{
    public ulong FeedbackId { get; set; }
    public string? VisitorName { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public string SubmittedAt { get; set; } = default!;
}
