using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetVisitInvitationDetail;

public class VisitInvitationDetailDto
{
    public ulong ParticipantId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }

    public string RequestCode { get; set; }
    public string DelegationName { get; set; }
    public string OrganizationName { get; set; }
    public string CampusName { get; set; }
    public string VisitScope { get; set; }

    public DateTime? PlannedStartAt { get; set; }
    public DateTime? PlannedEndAt { get; set; }

    public string ParticipantRole { get; set; }
    public string InvitationStatus { get; set; }

    public string VisitRequestStatus { get; set; }
    public string CampusVisitStatus { get; set; }

    public string InvitedByName { get; set; }
    public DateTime? InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public string? Note { get; set; }
    public string? DeclineReason { get; set; }

    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }

    public List<string> AllowedActions { get; set; } = new();
}
