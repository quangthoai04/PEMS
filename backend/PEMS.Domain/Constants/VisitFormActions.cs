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
    /// <summary>Requester side may propose an amendment for an ASSIGNED/BEFORE_VISIT instance ≥24h out with no pending amendment.</summary>
    public const string SubmitAmendment = "SUBMIT_AMENDMENT";
    /// <summary>Current campus Staff Leader may approve the instance's pending amendment.</summary>
    public const string ApproveAmendment = "APPROVE_AMENDMENT";
    /// <summary>Current campus Staff Leader may reject the instance's pending amendment.</summary>
    public const string RejectAmendment = "REJECT_AMENDMENT";
    /// <summary>Requester side may withdraw the instance's pending amendment.</summary>
    public const string WithdrawAmendment = "WITHDRAW_AMENDMENT";
}
