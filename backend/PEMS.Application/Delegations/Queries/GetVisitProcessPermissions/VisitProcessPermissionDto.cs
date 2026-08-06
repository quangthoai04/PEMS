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

    /// <summary>
    /// ASSIGNED → BEFORE_VISIT: the current Host may press "Bắt đầu chuẩn bị" and open the setup
    /// window. True only for the current Host of a live campus that is still ASSIGNED. This and
    /// <see cref="CanEditBeforeVisit"/> are never both true — that is the whole point of the state.
    /// </summary>
    public bool CanStartPreparation { get; set; }

    // Operational stage transitions (Host only, live instance):
    //   CanStartVisit    : BEFORE_VISIT → DURING_VISIT (finish preparation)
    //   CanCompleteVisit : DURING_VISIT → AFTER_VISIT  (finish the visit)
    //   CanCloseVisit    : AFTER_VISIT  → CLOSED       (close the delegation)
    public bool CanStartVisit { get; set; }
    public bool CanCompleteVisit { get; set; }
    public bool CanCloseVisit { get; set; }

    /// <summary>
    /// Whether the signed-in user may send the "Gửi cập nhật chuẩn bị" email for this instance.
    ///
    /// <para>
    /// Only the instance's CURRENT host, and only while the preparation tab is still open — the same
    /// window in which they may change what the mail would describe. A Staff Leader or HO who can read
    /// this page (and download the very same report) deliberately does NOT get it: writing to the guest
    /// as the delegation's host is not the same act as reading its preparation, and delegating it needs
    /// a rule of its own rather than falling out of view access.
    /// </para>
    /// <para>
    /// It is a separate flag rather than a reuse of <see cref="CanEditBeforeVisit"/> even though the two
    /// currently coincide: the frontend renders the send button from this one, so tightening or relaxing
    /// either must be a deliberate edit here, not a side effect on the other.
    /// </para>
    /// </summary>
    public bool CanSendSetupProgressEmail { get; set; }
}
