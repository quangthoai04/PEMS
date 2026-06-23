using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitationDetail;

public class VisitInvitationDetailDto
{
    public ulong ParticipantId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }

    public string RequestCode { get; set; } = null!;
    public string DelegationName { get; set; } = null!;
    public string OrganizationName { get; set; } = null!;
    public string CampusName { get; set; } = null!;
    public string VisitScope { get; set; } = null!;

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }

    public string ParticipantRole { get; set; } = null!;
    public string InvitationStatus { get; set; } = null!;

    public string VisitRequestStatus { get; set; } = null!;
    public string CampusVisitStatus { get; set; } = null!;

    public string InvitedByName { get; set; } = null!;
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public string? Note { get; set; }
    public string? DeclineReason { get; set; }

    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }

    public List<string> AllowedActions { get; set; } = new();
}
