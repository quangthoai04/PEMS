using System.Collections.Generic;
using MediatR;
using PEMS.Application.Feedbacks.Common;

namespace PEMS.Application.Feedbacks.Queries.GetVisitFeedbackTargets;

/// <summary>
/// Feedback screen data: which real business targets the current user may rate on this
/// campus visit instance. Visitor → one overall target; Host → 4 compact groups
/// (người tạo đoàn, đoàn khách, setup, detail setup). Never emits a fake target.
/// </summary>
public sealed record GetVisitFeedbackTargetsQuery(ulong VisitInstanceId)
    : IRequest<GetVisitFeedbackTargetsResponse>;

public sealed class GetVisitFeedbackTargetsResponse
{
    public bool CanSubmit { get; set; }
    public bool AlreadySubmittedAllRequired { get; set; }
    public string ActorType { get; set; } = default!;      // VISITOR | HOST
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }

    // Compact header summary
    public string DelegationName { get; set; } = default!;
    public string? CampusName { get; set; }
    public string InstanceStatus { get; set; } = default!;
    public string? PlannedStartAt { get; set; }            // "yyyy-MM-ddTHH:mm:ss" wall-clock
    public string? PlannedEndAt { get; set; }

    public List<FeedbackGroupDto> Groups { get; set; } = new();
    public List<ExistingFeedbackDto> ExistingFeedbacks { get; set; } = new();
    public string? SubmitHintMessage { get; set; }

    /// <summary>Stable code for <see cref="SubmitHintMessage"/> — one of NOT_ELIGIBLE / ALL_SUBMITTED /
    /// VISITOR_HINT / HOST_HINT. SubmitHintMessage stays Vietnamese-only (unchanged); the frontend
    /// uses this code to render an independent EN string for a Visitor reading in English.</summary>
    public string? SubmitHintKey { get; set; }
}

public sealed class ExistingFeedbackDto
{
    public ulong FeedbackId { get; set; }
    public string FeedbackType { get; set; } = default!;
    public string TargetType { get; set; } = default!;
    public string TargetName { get; set; } = default!;
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public string SubmittedAt { get; set; } = default!;
}
