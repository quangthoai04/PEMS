namespace PEMS.Domain.Constants;

/// <summary>
/// Stable action codes emitted by the per-campus v2 read model (<c>viewer.allowedActions</c> at the
/// request level, and <c>campusVisit.allowedActions</c> per instance). The frontend renders mutation UI
/// ONLY when the matching code is present — it never derives permission from role/relation/status.
/// Every command handler still re-authorizes independently; these codes only decide what the UI offers.
/// </summary>
public static class VisitFormActions
{
    /// <summary>Read access (always present for an authorized viewer).</summary>
    public const string View = "VIEW";

    // ── Request-level ────────────────────────────────────────────────────────
    /// <summary>Registrant/ACTIVE contact may edit a fully-pending request (≥24h before earliest start).</summary>
    public const string EditPendingRequest = "EDIT_PENDING_REQUEST";
    /// <summary>Registrant/ACTIVE contact may edit &amp; resubmit a fully-rejected request.</summary>
    public const string ResubmitRejectedRequest = "RESUBMIT_REJECTED_REQUEST";
    /// <summary>Registrant/ACTIVE contact may apply a safe/privacy edit (v2, request not cancelled).</summary>
    public const string SubmitSafeEdit = "SUBMIT_SAFE_EDIT";

    // ── Per-campus instance ──────────────────────────────────────────────────

    // ── Operational-contact workflow. INSTANCE scope, always ─────────────────
    // These mirror the guards in the operational-contact handlers exactly. They used to be
    // request-level, which is what let one answer decide campuses its owner was never invited to;
    // now every one of them names the campus it acts on, and holding one campus grants nothing on a
    // sibling. The frontend decided from `viewer.relation` alone before, so it offered buttons the
    // backend would refuse (a resend past its cap, a transfer inside the lead time, a second change
    // while one is pending).
    /// <summary>Registrant may re-send THIS campus's outstanding invitation (cap 5, with cooldown).</summary>
    public const string ResendOperationalContactConfirmation = "RESEND_OPERATIONAL_CONTACT_CONFIRMATION";
    /// <summary>Registrant may correct THIS campus's contact outright while the campus is undecided.</summary>
    public const string ReplaceOperationalContact = "REPLACE_OPERATIONAL_CONTACT";
    /// <summary>Registrant / this campus's confirmed contact may hand the campus to someone else after its decision.</summary>
    public const string InitiateOperationalContactTransfer = "INITIATE_OPERATIONAL_CONTACT_TRANSFER";
    /// <summary>Registrant / this campus's confirmed contact may close the outstanding invitation.</summary>
    public const string CancelOperationalContactChange = "CANCEL_OPERATIONAL_CONTACT_CHANGE";

    /// <summary>Requester side may propose an amendment for a BEFORE_VISIT instance ≥24h out with no pending amendment.</summary>
    public const string SubmitAmendment = "SUBMIT_AMENDMENT";
    /// <summary>Current campus Staff Leader may approve the instance's pending amendment.</summary>
    public const string ApproveAmendment = "APPROVE_AMENDMENT";
    /// <summary>Current campus Staff Leader may reject the instance's pending amendment.</summary>
    public const string RejectAmendment = "REJECT_AMENDMENT";
    /// <summary>Requester side may withdraw the instance's pending amendment.</summary>
    public const string WithdrawAmendment = "WITHDRAW_AMENDMENT";
    /// <summary>
    /// Current campus Staff Leader may hand this instance's Host role to a different eligible user.
    /// Distinct from the approve-and-assign path: that one gives a campus its FIRST Host as part of the
    /// approval decision and refuses to run twice, so it can never express "the Host has changed".
    /// </summary>
    public const string TransferHost = "TRANSFER_HOST";
}

/// <summary>Capability scopes — whether a verdict is about the whole request or one campus.</summary>
public static class VisitActionScopes
{
    public const string Request = "REQUEST";
    public const string Instance = "INSTANCE";
}

/// <summary>
/// audit_logs.action values for visit events that have no revision row of their own. The history read
/// model maps from THESE (immutable, written once by the command) rather than from message text.
/// </summary>
public static class VisitAuditActions
{
    /// <summary>One campus's Host role was handed to a different user after approval.</summary>
    public const string HostTransferred = "HOST_TRANSFERRED";
    /// <summary>audit_logs.source_type for the handover — groups it apart from form revisions.</summary>
    public const string HostTransferSourceType = "HOST_TRANSFER";
}
