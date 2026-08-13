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
    /// <summary>Registrant may edit a request no campus has decided yet, up to the mutation cutoff
    /// (<see cref="PEMS.Domain.Policies.VisitMutationPolicy.RequiredLeadHours"/> before the earliest
    /// start). Both pre-decision campus stages qualify, not only WAITING_REQUEST_APPROVAL.</summary>
    public const string EditPendingRequest = "EDIT_PENDING_REQUEST";
    /// <summary>Registrant/ACTIVE contact may edit &amp; resubmit a fully-rejected request.</summary>
    public const string ResubmitRejectedRequest = "RESUBMIT_REJECTED_REQUEST";

    /// <summary>
    /// Send ONE rejected campus back for review, leaving its siblings alone.
    ///
    /// <para>
    /// Distinct from <see cref="ResubmitRejectedRequest"/>, which needs EVERY campus rejected and
    /// revives the whole request. This one is offered on a campus that was refused beside one that was
    /// approved — and is offered to the person running that campus, not only to the registrant.
    /// </para>
    /// </summary>
    public const string ResubmitRejectedInstance = "RESUBMIT_REJECTED_INSTANCE";

    /// <summary>
    /// Edit ONE campus that is still waiting for its decision. INSTANCE scope.
    ///
    /// <para>
    /// The per-campus counterpart of <see cref="EditPendingRequest"/>, and the one that works on a MIXED
    /// request. Whole-request editing needs EVERY campus still waiting, so on a request with one campus
    /// approved it disappears — and until this code existed the campus that was still waiting had no
    /// action at all while its refused sibling had resubmit and its approved sibling had amendments.
    /// </para>
    /// <para>
    /// Granted to the registrant and to the operational contact of THAT campus. A Staff Leader gets it
    /// only when the campus is theirs AND they filed the request themselves — and then may additionally
    /// file a schedule inside the 72-hour floor and approve in the same action. A leader deciding
    /// somebody else's request is not offered this at all; their approve and reject are separate
    /// commands and are unaffected.
    /// </para>
    /// </summary>
    public const string EditPendingCampus = "EDIT_PENDING_CAMPUS";

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
    /// <summary>
    /// Registrant / this campus's current contact may correct the contact's DETAILS — name,
    /// organization, job title, phone. Never the address, which is what the two codes below are for.
    ///
    /// <para>
    /// Its window is much wider than theirs on purpose: nothing about authority moves, so an approved
    /// campus starting tomorrow still qualifies. Only a cancelled or rejected campus does not.
    /// </para>
    /// </summary>
    public const string UpdateOperationalContactProfile = "UPDATE_OPERATIONAL_CONTACT_PROFILE";
    /// <summary>Registrant may re-send THIS campus's outstanding invitation (cap 5, with cooldown).</summary>
    public const string ResendOperationalContactConfirmation = "RESEND_OPERATIONAL_CONTACT_CONFIRMATION";
    /// <summary>
    /// Registrant may open a BRAND NEW invitation for the address this campus already names, when the
    /// previous one ended unanswered (cancelled / declined / expired) and there is nothing left to
    /// resend. Distinct from <see cref="ResendOperationalContactConfirmation"/>, which reissues a token
    /// on an invitation that is still PENDING — the two never appear together, because a campus either
    /// has a live invitation or it does not.
    /// </summary>
    public const string ReinviteOperationalContactConfirmation = "REINVITE_OPERATIONAL_CONTACT_CONFIRMATION";
    /// <summary>Registrant may correct THIS campus's contact outright while the campus is undecided.</summary>
    public const string ReplaceOperationalContact = "REPLACE_OPERATIONAL_CONTACT";
    /// <summary>Registrant / this campus's confirmed contact may hand the campus to someone else after its decision.</summary>
    public const string InitiateOperationalContactTransfer = "INITIATE_OPERATIONAL_CONTACT_TRANSFER";
    /// <summary>Registrant / this campus's confirmed contact may close the outstanding invitation.</summary>
    public const string CancelOperationalContactChange = "CANCEL_OPERATIONAL_CONTACT_CHANGE";

    /// <summary>Requester side may propose an amendment for a decided, not-yet-started instance still
    /// inside the mutation cutoff and with no pending amendment.</summary>
    public const string SubmitAmendment = "SUBMIT_AMENDMENT";
    /// <summary>The instance's CURRENT Host may approve its pending amendment.</summary>
    public const string ApproveAmendment = "APPROVE_AMENDMENT";
    /// <summary>The instance's CURRENT Host may reject its pending amendment.</summary>
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

    /// <summary>
    /// A campus's Staff Leader deliberately filed a schedule inside the 72-hour registration floor.
    ///
    /// <para>
    /// Written as its own row rather than folded into the edit's field diff, because the question it
    /// answers is different: the diff says the date moved, this says somebody with the authority to do
    /// so accepted less notice than the rule asks for, and named themselves doing it. Its reason string
    /// carries the required lead time and both starts, so the row stands on its own.
    /// </para>
    /// </summary>
    public const string LeadTimeOverride = "LEAD_TIME_OVERRIDE";
    /// <summary>audit_logs.source_type for the override, so the rows can be counted on their own.</summary>
    public const string LeadTimeOverrideSourceType = "LEAD_TIME_OVERRIDE";
}
