using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;

/// <summary>
/// A single visit-participation invitation addressed to the signed-in user (UC-27).
/// Used by both the pending-invitations list and the invitation-detail screen.
/// </summary>
public sealed class VisitInvitationDto
{
    public ulong ParticipantId { get; set; }
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }

    public string? RequestCode { get; set; }
    public string? DelegationName { get; set; }
    public string? OrganizationName { get; set; }

    public ulong CampusId { get; set; }
    public string? CampusName { get; set; }

    /// <summary>IC_SUPPORT | DEPT_SUPPORT | STUDENT (never IC_HOST — host is not an invitation).</summary>
    public string ParticipantRole { get; set; } = default!;
    /// <summary>INVITED | ACCEPTED | DECLINED.</summary>
    public string Status { get; set; } = default!;

    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }

    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }

    public ulong? InvitedByUserId { get; set; }
    public string? InvitedByName { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Free-text note; for a DECLINED invitation this holds the decline reason.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Actions available on the invitation-detail screen. ACCEPT_INVITATION / DECLINE_INVITATION
    /// are present only while the invitation is still INVITED; otherwise only VIEW_DETAIL.
    /// </summary>
    public List<string> AllowedActions { get; set; } = new();
}
