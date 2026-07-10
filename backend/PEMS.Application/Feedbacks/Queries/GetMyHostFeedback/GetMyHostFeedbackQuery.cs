using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Feedbacks.Queries.GetMyHostFeedback;

/// <summary>
/// Backs the "Host feedback về bạn" notification modal (action_type=OPEN_HOST_FEEDBACK_MODAL).
/// Always resolves the target to the CALLER's own user id server-side — never accepts a
/// targetUserId from the client, so a user can never see another user's feedback.
/// </summary>
public sealed record GetMyHostFeedbackQuery(ulong VisitInstanceId)
    : IRequest<GetMyHostFeedbackResponse>;

public sealed class GetMyHostFeedbackResponse
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
    public List<HostFeedbackItemDto> Feedbacks { get; set; } = new();
}

public sealed class HostFeedbackItemDto
{
    public ulong FeedbackId { get; set; }
    public string? HostName { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public string SubmittedAt { get; set; } = default!;
    public List<HostFeedbackRatingItemDto> RatingItems { get; set; } = new();
}

public sealed class HostFeedbackRatingItemDto
{
    public string CriterionCode { get; set; } = default!;
    public string CriterionLabel { get; set; } = default!;
    public byte Rating { get; set; }
}
