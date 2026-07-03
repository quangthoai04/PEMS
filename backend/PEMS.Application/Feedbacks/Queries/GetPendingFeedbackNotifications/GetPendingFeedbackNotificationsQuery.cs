using System.Collections.Generic;
using MediatR;

namespace PEMS.Application.Feedbacks.Queries.GetPendingFeedbackNotifications;

/// <summary>
/// Visits the current user should rate (bell reminder "Bạn hãy đánh giá đoàn" + the "Đánh giá"
/// action in the visit list). Computed dynamically — nothing is inserted into `notifications`,
/// so reminders can never duplicate and disappear by themselves once feedback is submitted.
/// Covers both roles: Visitor (their own requests) and Host (instances they are current host of),
/// for instances in DURING_VISIT / AFTER_VISIT / CLOSED.
/// </summary>
public sealed record GetPendingFeedbackNotificationsQuery()
    : IRequest<GetPendingFeedbackNotificationsResponse>;

public sealed class GetPendingFeedbackNotificationsResponse
{
    /// <summary>Every rate-eligible instance of the user with its submitted state (for list badges).</summary>
    public List<PendingFeedbackItemDto> Items { get; set; } = new();
    /// <summary>Number of items with AlreadySubmitted == false (bell badge).</summary>
    public int PendingCount { get; set; }
}

public sealed class PendingFeedbackItemDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string ActorType { get; set; } = default!;      // VISITOR | HOST
    public string DelegationName { get; set; } = default!;
    public string? CampusName { get; set; }
    public string InstanceStatus { get; set; } = default!; // DURING_VISIT | AFTER_VISIT | CLOSED
    public string? PlannedStartAt { get; set; }
    public bool AlreadySubmitted { get; set; }
}
