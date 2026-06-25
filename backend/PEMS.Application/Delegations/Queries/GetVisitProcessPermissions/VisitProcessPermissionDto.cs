namespace PEMS.Application.Delegations.Queries.GetVisitProcessPermissions;

/// <summary>
/// Permission flags for the visit-process detail page. All edit/create flags already account for
/// status (CLOSED/CANCELLED ⇒ read-only) and host assignment, so the frontend only needs to honor
/// the booleans. <see cref="Relation"/> is for display/telemetry only — never an auth input.
/// </summary>
public sealed class VisitProcessPermissionDto
{
    public ulong VisitInstanceId { get; set; }
    public ulong VisitRequestId { get; set; }

    /// <summary>Request decision status (PENDING_APPROVAL/APPROVED/REJECTED/CANCELLED).</summary>
    public string RequestStatus { get; set; } = default!;
    /// <summary>Campus instance status (WAITING_HOST_ASSIGNMENT/ASSIGNED/.../CLOSED/CANCELLED).</summary>
    public string InstanceStatus { get; set; } = default!;

    /// <summary>HOST | STAFF_LEADER | HO | VISITOR_OWNER | IC_SUPPORT | DEPT_SUPPORT | STUDENT | NONE.</summary>
    public string Relation { get; set; } = "NONE";
    public bool HostAssigned { get; set; }

    public bool CanViewOriginalRequest { get; set; }
    public bool CanViewOverview { get; set; }

    public bool CanViewBeforeVisit { get; set; }
    public bool CanEditBeforeVisit { get; set; }

    public bool CanViewDuringVisit { get; set; }
    public bool CanEditDuringVisit { get; set; }

    public bool CanViewAfterVisit { get; set; }
    public bool CanEditAfterVisit { get; set; }

    public bool CanAssignHost { get; set; }

    public bool CanViewMinutes { get; set; }
    public bool CanCreateMinutes { get; set; }
    public bool CanEditMinutes { get; set; }

    public bool CanViewNews { get; set; }
    public bool CanCreateNews { get; set; }

    // Operational stage transitions (Host only, live instance):
    //   CanStartVisit    : ASSIGNED/BEFORE_VISIT → DURING_VISIT (finish preparation)
    //   CanCompleteVisit : DURING_VISIT          → AFTER_VISIT  (finish the visit)
    //   CanCloseVisit    : AFTER_VISIT           → CLOSED       (close the delegation)
    public bool CanStartVisit { get; set; }
    public bool CanCompleteVisit { get; set; }
    public bool CanCloseVisit { get; set; }
}
