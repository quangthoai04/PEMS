using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Feedbacks.Commands.SubmitVisitFeedback;

/// <summary>
/// Batch submit of the feedback screen. Each item must map to a REAL business target of the
/// visit instance (see FeedbackRules) — rating 1..5 required, comment optional.
/// Duplicate feedback per (submitter, instance, target) is rejected with 409.
/// </summary>
public sealed record SubmitVisitFeedbackCommand(
    ulong VisitInstanceId,
    List<SubmitVisitFeedbackItem> Items) : IRequest<SubmitVisitFeedbackResponse>;

public sealed class SubmitVisitFeedbackItem
{
    public string FeedbackType { get; set; } = default!;   // VISITOR_OVERALL | HOST_PARTICIPANT | HOST_LOGISTICS
    public string TargetType { get; set; } = default!;
    public ulong? TargetUserId { get; set; }
    public ulong? TargetParticipantId { get; set; }
    public ulong? TargetGuestMemberId { get; set; }
    public ulong? TargetLogisticsItemId { get; set; }
    public ulong? TargetHandoverId { get; set; }
    public ulong? TargetDepartmentId { get; set; }
    public byte Rating { get; set; }                       // 1..5 required
    public string? Comment { get; set; }                   // optional; trimmed server-side
}

public sealed class SubmitVisitFeedbackResponse
{
    public int SavedCount { get; set; }
    public bool AlreadySubmittedAllRequired { get; set; }
    public string Message { get; set; } = default!;
}
