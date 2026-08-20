using System.Collections.Generic;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;

namespace PEMS.Application.Delegations.Queries.ResolveNotificationVisitTarget;

/// <summary>
/// The exact, current-state answer to "what is this notification actually about, and what may THIS
/// caller do with it right now". Every field is scoped to the exact
/// <see cref="ResolveNotificationVisitTargetQuery.VisitInstanceId"/> the notification named (or to
/// the request itself when it named none) — never to whichever relation happens to rank highest on
/// an aggregated list row.
/// </summary>
public sealed class NotificationVisitTargetDto
{
    /// <summary>False when the request (or the named instance) no longer exists at all.</summary>
    public bool Exists { get; set; }

    /// <summary>
    /// False when it exists but the caller currently holds no relation to it — a declined invitation,
    /// a transferred-away host role, a campus outside a Staff Leader's own campus, etc.
    /// </summary>
    public bool HasAccess { get; set; }

    public ulong VisitRequestId { get; set; }
    public ulong? VisitInstanceId { get; set; }
    public ulong? CampusId { get; set; }
    public string? CampusName { get; set; }

    public string? RequestStatus { get; set; }
    public string? CampusStatus { get; set; }
    public string? VisitScope { get; set; }
    public string? RequestCode { get; set; }
    public string? DelegationName { get; set; }

    public bool CanViewRequestDetail { get; set; }

    /// <summary>
    /// Every relation the caller genuinely holds AT THE RESOLVED SCOPE (the exact instance, or
    /// REQUEST scope when no instance was named) — never a merged/aggregated row's single winner.
    /// Same shape <c>ViewGuestDelegationList</c> already uses, so the frontend keeps one vocabulary.
    /// </summary>
    public List<VisitRelationContextDto> RelationContexts { get; set; } = new();

    public ulong? ParticipantId { get; set; }
    public string? ParticipantStatus { get; set; }
}
