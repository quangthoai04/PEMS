using System;

namespace PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

public sealed class VisitRequestManagementItemDto
{
    public ulong VisitRequestId { get; init; }
    public ulong? VisitInstanceId { get; init; }
    public string? RequestCode { get; init; }
    public string? DelegationName { get; init; }
    public string? PartnerName { get; init; }

    public string RequestStatus { get; init; } = default!;
    public string? CampusStatus { get; init; }

    public string? VisitScope { get; init; }

    public ulong? CampusId { get; init; }
    public string? CampusName { get; init; }

    public ulong? CreatedByUserId { get; init; }
    public ulong? CurrentHostUserId { get; init; }
    public string? HostName { get; init; }
    
    public ulong? VisitorUserId { get; init; }
    public string? VisitorName { get; init; }

    public bool IsCurrentUserParticipant { get; init; }

    public DateTime? ExpectedStartAt { get; init; }
    public DateTime? ExpectedEndAt { get; init; }
    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }
    public int? ExpectedGuestCount { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
}