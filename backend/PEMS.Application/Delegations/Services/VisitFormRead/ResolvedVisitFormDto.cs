namespace PEMS.Application.Delegations.Services.VisitFormRead;

/// <summary>
/// Per-campus form read model returned by <see cref="IVisitFormReadService"/>. Every campus carries its
/// own content, resolved from that campus's detail row. It only
/// ever contains the campus instances the caller is authorized to see — hidden campuses never appear,
/// and their count/detail never leaks. Sensitive fields (raw tokens, pending-snapshot JSON, full
/// amendment JSON, audit IP/UA) are intentionally excluded.
/// </summary>
public sealed class ResolvedVisitFormDto
{
    public long VisitRequestId { get; init; }
    public string RequestCode { get; init; } = "";
    /// <summary>Request-level optimistic-concurrency token — the edit/resubmit v2 payload echoes it back as
    /// <c>ExpectedRequestRowVersion</c> so a stale editor gets a stable 409 instead of clobbering a concurrent change.</summary>
    public int RowVersion { get; init; }
    public bool HasMixedCampusDetails { get; init; }
    public string VisitScope { get; init; } = "";
    public string RequestStatus { get; init; } = "";
    public string CreatedSource { get; init; } = "";
    public DateTime SubmittedAt { get; init; }
    public long? PartnerId { get; init; }

    // ── Cancellation outcome (UC-136) ──
    // Populated only when the REQUEST itself was cancelled. The detail screen has to explain how a
    // request ended, and "CANCELLED" on its own leaves the reader hunting through the timeline for who
    // did it and why. Request-level cancellation is not campus-scoped, so there is nothing to hide here:
    // a caller who can read the request can read how it ended.
    public ulong? CancelledByUserId { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }

    public ResolvedRegistrantDto Registrant { get; init; } = new();

    /// <summary>
    /// How far the request is through the confirmation gate, counted over the campuses the caller may
    /// see. There is no request-level contact to report any more — each campus has its own, and the
    /// only request-level fact about them is how many have answered.
    /// </summary>
    public ResolvedConfirmationSummaryDto ConfirmationSummary { get; init; } = new();

    /// <summary>
    /// The verdict on the WHOLE request. NULL for any caller who does not see every campus, and that
    /// null is the point: a Staff Leader scoped to one rejected campus was being shown "rejected at
    /// every campus" because the only collection the page had was their own slice of it, and a slice
    /// where everything is rejected looks exactly like a request where everything is rejected.
    /// Counted here, over every campus, so the claim is only ever made by somebody who can see them.
    /// </summary>
    public ResolvedRequestOutcomeDto? RequestOutcome { get; init; }

    /// <summary>Only the campus instances the caller may view, ordered by planned start.</summary>
    public List<ResolvedCampusVisitDto> CampusVisits { get; init; } = new();

    /// <summary>
    /// Backend-derived viewer capabilities (never trusted from the client). PR-3 exposes read-scope
    /// facts only; mutation actions (edit / amend / transfer / cancel) are added by the write PRs and
    /// are always re-authorized in their command handlers.
    /// </summary>
    public ResolvedViewerContextDto Viewer { get; init; } = new();
}

public sealed class ResolvedRegistrantDto
{
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string Nationality { get; init; } = "";
}

/// <summary>
/// Confirmation-gate progress across the visible campuses (plan §5.2). Counts only — never an address
/// and never a token, so it is safe for anybody who may read the request at all.
/// </summary>
public sealed class ResolvedConfirmationSummaryDto
{
    /// <summary>Visible campuses that are not cancelled — the denominator of the gate.</summary>
    public int Total { get; init; }
    public int Confirmed { get; init; }
    /// <summary>Campuses whose contact has neither confirmed nor refused yet.</summary>
    public int Pending { get; init; }
    public int Declined { get; init; }
    public int Expired { get; init; }
    /// <summary>True while the whole request is held at the gate (no Staff Leader may see it).</summary>
    public bool GateOpen { get; init; }
}

/// <summary>
/// Request-wide outcome, counted over EVERY campus of the request. Sent only to a caller with
/// full-request scope (registrant, request owner, HO); everyone else gets null and must describe
/// what they can see instead.
/// </summary>
public sealed class ResolvedRequestOutcomeDto
{
    /// <summary>
    /// ALL_CANCELLED — the request itself was cancelled · ALL_REJECTED — every campus refused ·
    /// ALL_WAITING — every campus still deciding · MIXED — campuses ended differently ·
    /// IN_PROGRESS — nothing refused, at least one campus under way · NO_CAMPUS.
    /// </summary>
    public string Code { get; init; } = "";
    public int Total { get; init; }
    public int Accepted { get; init; }
    public int InProgress { get; init; }
    public int Waiting { get; init; }
    public int Rejected { get; init; }
    public int Cancelled { get; init; }
    public int Closed { get; init; }
}

public sealed class ResolvedViewerContextDto
{
    /// <summary>HOST / STAFF_LEADER / HO / VISITOR_OWNER / REGISTRANT / IC_SUPPORT / DEPT_SUPPORT / STUDENT / NONE.</summary>
    public string Relation { get; init; } = "NONE";
    /// <summary>True when the caller sees every campus of the request (registrant, primary contact, HO).</summary>
    public bool CanViewAllCampuses { get; init; }
    /// <summary>True for HO — monitoring only, no business action.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>
    /// The ENABLED subset of <see cref="Capabilities"/>, plus the contact-identity actions and the READ
    /// capabilities that have no verdict to carry — <c>VIEW</c>, and <c>VIEW_CHANGE_HISTORY</c> when
    /// this viewer may read the UC-32 timeline. Kept as a flat list because most call sites only ask
    /// "may I?"; the mutation entries are DERIVED from the capabilities rather than computed
    /// separately, so the two can never disagree.
    /// </summary>
    public List<string> AllowedActions { get; init; } = new();
    /// <summary>
    /// Request-scoped mutation capabilities WITH their verdict — including the ones that are refused.
    /// A refused capability carries why and until when, which is what lets the UI show a disabled
    /// button with a real reason instead of silently hiding the action and leaving the user to guess.
    /// </summary>
    public List<VisitActionCapabilityDto> Capabilities { get; init; } = new();
}

/// <summary>
/// One action, one verdict. The backend is the authority: the frontend renders from this and never
/// re-derives permission from status, role or relation, and every command handler re-checks the same
/// policy inside its transaction.
/// </summary>
public sealed class VisitActionCapabilityDto
{
    /// <summary>EDIT_PENDING_REQUEST / RESUBMIT_REJECTED_REQUEST / SUBMIT_SAFE_EDIT / SUBMIT_AMENDMENT / APPROVE_AMENDMENT / TRANSFER_HOST.</summary>
    public string Code { get; init; } = "";
    /// <summary>REQUEST (applies to the whole request) or INSTANCE (applies to one campus).</summary>
    public string Scope { get; init; } = "";
    /// <summary>Set for INSTANCE scope — which campus this verdict is about.</summary>
    public long? VisitInstanceId { get; init; }
    public bool Enabled { get; init; }
    /// <summary>Stable code (VISIT_MUTATION_CUTOFF_REACHED / …) — match on this, never on the message.</summary>
    public string? DisabledReasonCode { get; init; }
    public string? DisabledReason { get; init; }
    /// <summary>The moment the window closes. Present whether or not it has passed.</summary>
    public DateTime? CutoffAt { get; init; }
    /// <summary>Start of the campus this verdict was measured against — pairs with CutoffAt in the UI.</summary>
    public DateTime? PlannedStartAt { get; init; }
    /// <summary>Campus the verdict was measured against (the governing one for a request-level action).</summary>
    public string? CampusName { get; init; }
    public int RequiredLeadHours { get; init; }
}

public sealed class ResolvedCampusVisitDto
{
    public long VisitInstanceId { get; init; }
    public long CampusId { get; init; }
    public string CampusCode { get; init; } = "";
    public string CampusName { get; init; } = "";
    public DateTime PlannedStartAt { get; init; }
    public DateTime PlannedEndAt { get; init; }
    public string Timezone { get; init; } = "Asia/Ho_Chi_Minh";

    public string InstanceStatus { get; init; } = "";
    public long? CurrentHostUserId { get; init; }
    public string? CurrentHostName { get; init; }

    /// <summary>
    /// The three people of this campus are three separate objects on purpose (plan §1.1): the
    /// registrant (request-level), the guest-side operational contact, and the FPTU reception host.
    /// They can be three different people, and collapsing any two of them has repeatedly produced
    /// screens that show the wrong person's phone number beside the wrong label.
    /// Null while nobody has been assigned — never "the proposed host, close enough".
    /// </summary>
    public ResolvedCurrentHostDto? CurrentHost { get; init; }

    /// <summary>The intended host while the gate is shut, or the record of one that fell through.</summary>
    public ResolvedProposedHostDto? ProposedHost { get; init; }

    /// <summary>
    /// What the CALLER may do about this campus's reception host, decided by the backend. The
    /// frontend renders the create/edit controls from this rather than re-deriving them from a role.
    /// </summary>
    public ResolvedHostSelectionCapabilitiesDto HostSelection { get; init; } = new();
    public long? DecidedByUserId { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? DecisionActorRole { get; init; }
    public string? DecisionNote { get; init; }

    // ── Per-campus cancellation (UC-136) ──
    // A campus can be cancelled on its own without the whole request being cancelled, so this is
    // separate from the request-level block above. It is projected from the instance the caller is
    // already authorized to see, so it carries no cross-campus information.
    public ulong? CancelledByUserId { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationActorType { get; init; }
    public string? CancellationSource { get; init; }
    public string? CancellationReason { get; init; }

    // This campus's form content, from its own visit_instance_form_details row.
    public string DelegationName { get; init; } = "";
    public string VisitType { get; init; } = "";
    public string? VisitTypeOther { get; init; }
    public string Purpose { get; init; } = "";
    public string? WorkingContent { get; init; }
    public List<ResolvedMemberDto> Visitors { get; init; } = new();
    public List<ResolvedMemberDto> SupportMembers { get; init; } = new();
    public ResolvedOperationalContactDto OperationalContact { get; init; } = new();
    public string WorkingLanguage { get; init; } = "";
    public string? TransportationNote { get; init; }
    public string MediaConsentStatus { get; init; } = "";

    /// <summary>"Ghi chú gửi FPTU" — this campus's one general remark, independent of media consent.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Where THIS campus stands in the confirmation workflow. Per campus because the workflow is:
    /// one campus can be confirmed and running while its sibling is still waiting for an answer.
    /// </summary>
    public ResolvedCampusContactStateDto ContactState { get; init; } = new();

    public uint FormRevision { get; init; }
    public uint ApprovalRevision { get; init; }
    public int RowVersion { get; init; }

    /// <summary>Present only when the actor may see it; carries no PII/full-diff JSON (summary only).</summary>
    public ResolvedActiveAmendmentDto? ActiveAmendment { get; init; }

    /// <summary>
    /// Backend-derived mutation actions the caller may take on THIS instance (per-campus scoped) — e.g.
    /// SUBMIT_AMENDMENT / APPROVE_AMENDMENT / REJECT_AMENDMENT / WITHDRAW_AMENDMENT. The frontend gates
    /// per-instance UI on this list; command handlers re-authorize. Empty for read-only viewers.
    /// Derived from <see cref="Capabilities"/> (plus the amendment decision actions), never computed twice.
    /// </summary>
    public List<string> AllowedActions { get; init; } = new();

    /// <summary>
    /// This campus's mutation capabilities WITH their verdict, refused ones included. A sibling campus
    /// being under way says nothing about this one — every entry here was measured against THIS
    /// instance's own status and start time.
    /// </summary>
    public List<VisitActionCapabilityDto> Capabilities { get; init; } = new();

    /// <summary>
    /// True when a change this caller submits for THIS campus will be approved in the same call, because
    /// they are both the requester side and the campus's current Host.
    ///
    /// <para>
    /// It exists so the UI can say "Cập nhật" instead of "Gửi đề xuất thay đổi" and skip a review step
    /// that reviews nothing — not so the UI can apply anything itself. The backend still creates the
    /// amendment, still validates it and still records the decision; this flag only changes a label.
    /// The client must never infer it from role or status: the previous version of this behaviour lived
    /// in the browser as "is the user a STAFF account", which was wrong for a staff registrant who was
    /// not the Host of anything.
    /// </para>
    /// </summary>
    public bool AmendmentSelfApproves { get; init; }

    /// <summary>
    /// True when this caller is the Staff Leader of THIS campus AND the request's registrant, and so may
    /// (a) file a schedule inside the 72-hour registration floor after confirming, and (b) approve the
    /// campus in the same call as an edit. Both halves are needed and neither is enough: leading the
    /// campus without having filed the request does not open the edit at all, and filing the request
    /// while leading a DIFFERENT campus opens the edit but grants neither privilege — that actor is a
    /// requester here. Lets the client warn EARLY and offer the right buttons; the backend re-decides
    /// both, so a client that ignores this cannot obtain either.
    ///
    /// <para>
    /// It says nothing about approving or rejecting the campus the ORDINARY way. Those are separate
    /// commands, still open to the campus's leader whoever filed the request, and the list screen
    /// decides them from its own APPROVE_AND_ASSIGN_HOST / CAMPUS_REJECT actions.
    /// </para>
    /// </summary>
    public bool CanOverrideScheduleLeadTime { get; init; }
}

public sealed class ResolvedMemberDto
{
    public long GuestMemberId { get; init; }
    public string MemberType { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    /// <summary>
    /// Which partner profile this member's organization was picked from, or null for free text.
    /// Returned so re-opening the edit form restores the CHOICE and not just the text (PART-01).
    /// </summary>
    public ulong? OrganizationPartnerId { get; init; }
    public string JobTitle { get; init; } = "";
    public string Nationality { get; init; } = "";
    public int DisplayOrder { get; init; }
}

/// <summary>
/// "Đầu mối đoàn khách phối hợp tại cơ sở" — the guest-side person who coordinates THIS campus with
/// FPTU. Per campus, always: two campuses of one request routinely have two different people, and
/// there is no request-level contact to fall back on.
///
/// <para>
/// This is NOT the reception host and must never be rendered under a host label. It is also not the
/// registrant: the person who filled the form may be a third party who never attends.
/// </para>
///
/// <para>
/// Every field can be empty while the invitation is still outstanding — the block is still worth
/// rendering, because the email and the confirmation status are exactly what the reader wants then.
/// </para>
/// </summary>
public sealed class ResolvedOperationalContactDto
{
    public string FullName { get; init; } = "";
    public string Organization { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";

    /// <summary>
    /// PENDING / CONFIRMED / DECLINED / EXPIRED / TRANSFER_PENDING, mirroring
    /// <see cref="ResolvedCampusVisitDto.ContactState"/> so a caller rendering only this object still
    /// knows whether to trust the name beside it.
    /// </summary>
    public string ConfirmationStatus { get; init; } = "PENDING";
    /// <summary>REGISTRANT_SELF_MATCH | EMAIL_CONFIRMATION | TRANSFER — null until confirmed.</summary>
    public string? ConfirmationSource { get; init; }
    public DateTime? ConfirmedAt { get; init; }

    /// <summary>
    /// Which member of THIS campus's delegation the contact IS — matches a
    /// <see cref="ResolvedMemberDto.GuestMemberId"/> in <see cref="ResolvedCampusVisitDto.Visitors"/>
    /// (NP-03).
    ///
    /// <para>
    /// Null is a normal answer, and means one of two things the reader does not have to tell apart:
    /// the contact is not travelling with the delegation, or the request predates this link. Either
    /// way the five snapshot fields above are complete on their own.
    /// </para>
    /// <para>
    /// It is a plain relation, NOT an authorization fact: who may act as the contact is decided by
    /// <c>visit_request_campuses.operational_contact_user_id</c> and nothing else.
    /// </para>
    /// </summary>
    public long? GuestMemberId { get; init; }
}

/// <summary>
/// "Host dự kiến" — somebody an internal creator named as the intended reception host BEFORE the
/// confirmation gate opened. Present only while it still matters: once the proposal is activated the
/// person appears as <see cref="ResolvedCampusVisitDto.CurrentHost"/> instead, and the UI must not
/// show both at once.
/// </summary>
public sealed class ResolvedProposedHostDto
{
    public long? UserId { get; init; }
    public string FullName { get; init; } = "";
    /// <summary>Campus or department, whichever identifies them to a reader of this screen.</summary>
    public string OrganizationOrDepartment { get; init; } = "";
    /// <summary>SELF | SELECTED | WAIT_FOR_LATER.</summary>
    public string SelectionMode { get; init; } = "WAIT_FOR_LATER";
    /// <summary>PENDING | ACTIVATED | NEEDS_RESELECTION.</summary>
    public string? ProposalStatus { get; init; }
    public DateTime? ProposedAt { get; init; }
}

/// <summary>
/// "Người phụ trách tiếp đón" — the OFFICIAL reception host, set only after the gate opened. Null
/// means nobody has been assigned yet, which is a different fact from "a host was proposed".
/// </summary>
public sealed class ResolvedCurrentHostDto
{
    public long UserId { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string DepartmentName { get; init; } = "";
}

/// <summary>
/// One campus's operational-contact confirmation state. Addresses are MASKED even for the registrant:
/// the API says whether an invitation is outstanding, not who exactly it went to — the full address is
/// something the registrant typed, not something the system reads back out.
/// </summary>
public sealed class ResolvedCampusContactStateDto
{
    /// <summary>An account actually holds this campus (operational_contact_user_id is set).</summary>
    public bool Confirmed { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    /// <summary>REGISTRANT_SELF_MATCH | EMAIL_CONFIRMATION | TRANSFER.</summary>
    public string? ConfirmationSource { get; init; }
    /// <summary>True when the actor is the confirmed contact of THIS campus.</summary>
    public bool IsCurrentUser { get; init; }

    /// <summary>INITIAL_CONFIRMATION | TRANSFER, when an invitation is outstanding for this campus.</summary>
    public string? PendingChangeKind { get; init; }
    public string? PendingEmailMasked { get; init; }
    public DateTime? PendingExpiresAt { get; init; }
    public uint PendingResendCount { get; init; }
}

/// <summary>
/// Backend verdict on what the caller may do about this campus's reception host. Mirrors §3.4's
/// capability names. A capability is a rendering hint, never a token: every handler re-authorizes.
/// </summary>
public sealed class ResolvedHostSelectionCapabilitiesDto
{
    public bool CanProposeSelfAsHost { get; init; }
    public bool CanProposeOtherHost { get; init; }
    public bool CanWaitForLaterAssignment { get; init; }
    public bool CanUpdateProposedHost { get; init; }
}

public sealed class ResolvedActiveAmendmentDto
{
    public long AmendmentId { get; init; }
    public uint AmendmentNo { get; init; }
    public string Status { get; init; } = "";
    public DateTime RequestedAt { get; init; }
    public int ChangedFieldCount { get; init; }
}
