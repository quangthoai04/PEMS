namespace PEMS.Shared;

// Maps visit_request_campuses.decision_actor_role ENUM.
// STAFF_LEADER — standard campus review, or the activation of a proposal the Leader authorized.
// STAFF        — a regular IC Staff who registered the request, named THEMSELF as that campus's
//                proposed host, and had that proposal activated when the confirmation gate opened
//                (decision_source PREAUTHORIZED_HOST_ACTIVATION). It is the only case where a
//                non-Leader appears as the decider, and the DB ties it to the proposal columns.
public static class DecisionActorRole
{
    public const string StaffLeader = "STAFF_LEADER";
    public const string Staff       = "STAFF";
}

// Maps visit_request_campuses.decision_source ENUM.
public static class DecisionSources
{
    /// <summary>The campus Staff Leader approved a pending campus and named the host in that action.</summary>
    public const string StandardCampusReview = "STANDARD_CAMPUS_REVIEW";

    /// <summary>
    /// A host proposal recorded BEFORE the confirmation gate opened was revalidated and activated at
    /// the moment it opened. Replaces the old INTERNAL_SELF_HOST / INTERNAL_LEADER_ASSIGN sources,
    /// which named a current host at submit time — i.e. before anybody had confirmed they would run
    /// the visit, which is the one thing the gate exists to prevent.
    /// </summary>
    public const string PreauthorizedHostActivation = "PREAUTHORIZED_HOST_ACTIVATION";
}

/// <summary>
/// Maps visit_request_campuses.host_selection_mode — the reception-host ARRANGEMENT chosen by an
/// internal creator, never a lifecycle status. A campus sits at one of these from create until the
/// gate opens; only then does a proposal become a real assignment.
/// </summary>
public static class HostSelectionModes
{
    /// <summary>The person filling the form will host this campus themself.</summary>
    public const string Self = "SELF";
    /// <summary>A Staff Leader named a specific IC Staff of the same campus.</summary>
    public const string Selected = "SELECTED";
    /// <summary>Nobody named yet — the campus Staff Leader assigns after the gate opens. The default.</summary>
    public const string WaitForLater = "WAIT_FOR_LATER";

    public static bool IsProposal(string? mode) => mode is Self or Selected;

    public static bool IsKnown(string? mode) => mode is Self or Selected or WaitForLater;
}

/// <summary>Maps visit_request_campuses.proposed_host_activation_status.</summary>
public static class ProposedHostActivationStatuses
{
    /// <summary>Saved and waiting for the confirmation gate to open.</summary>
    public const string Pending = "PENDING";
    /// <summary>Revalidated and turned into the campus's current host.</summary>
    public const string Activated = "ACTIVATED";
    /// <summary>
    /// The gate opened but the proposed person no longer qualified. The contact confirmation still
    /// succeeded — the proposal is kept for audit and the campus falls back to WAITING_REQUEST_APPROVAL
    /// so its Staff Leader picks somebody. Never auto-substituted.
    /// </summary>
    public const string NeedsReselection = "NEEDS_RESELECTION";
}
