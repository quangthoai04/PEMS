using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Commands.VisitRequestOtp;
using PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Queries.GetCreateHostCandidates;
using PEMS.Application.Delegations.Queries.GetEditableVisitRequestDetail;
using PEMS.Application.Delegations.Queries.GetVisitRequestFormV2;

namespace PEMS.Api.Controllers;

/// <summary>
/// UC-17 — Public endpoint for submitting a visit request (no authentication required),
/// plus the authenticated Visitor edit / resubmit flow (ownership + role are enforced in
/// the handlers via ICurrentUserService — an anonymous caller gets 403).
/// </summary>
[ApiController]
[Route("api/visit-requests")]
public sealed class VisitRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VisitRequestsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// STEP 1 — Validates form data, stores a pending session and sends a 6-digit OTP
    /// to the registrant's email. Returns a <c>sessionToken</c> the client must pass
    /// to the verify endpoint.
    /// </summary>
    [HttpPost("initiate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult Initiate()
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    /// <summary>
    /// STEP 2 — Verifies the OTP challenge (identified by <c>sessionToken</c> and bound to
    /// <c>submissionId</c>), creates the VisitRequest, provisions the Visitor account and
    /// routes the request to the correct approval queue. Retries of the SAME submission
    /// intent are replayed idempotently (200 with the original request). A different submit
    /// intent with the same core content inside the duplicate window returns
    /// 409 DUPLICATE_VISIT_REQUEST.
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult Verify()
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    /// <summary>
    /// RESEND — Supersedes the old challenge and returns a NEW <c>sessionToken</c> with a
    /// fresh code. Subject to per-email hourly issue quotas + min resend interval. A
    /// challenge that already requires human verification cannot be resent (428).
    /// </summary>
    [HttpPost("resend-otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendVisitRequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// RECOVER — After the challenge was burned by too many wrong codes: validates the
    /// Turnstile token server-side, invalidates the old challenge and issues a brand-new
    /// one (attempts reset) for the same submission intent. CAPTCHA success never re-opens
    /// the old code.
    /// </summary>
    [HttpPost("otp/recover")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecoverOtp(
        [FromBody] RecoverVisitRequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// AUTHENTICATED create (Visitor / IC Staff / Staff Leader) — no OTP: the JWT session
    /// is the registrant identity. Same shared form validation + fingerprint idempotency
    /// as the public flow. RETIRED — answers 410; the per-campus create is
    /// <see cref="CreateFormV2"/>, where the reception-host arrangement is a PROPOSAL
    /// (SELF / SELECTED / WAIT_FOR_LATER) activated after the confirmation gate, not a
    /// processing mode applied at submit.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult CreateAuthenticated()
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    /// <summary>
    /// Host candidates for the create form's SELECTED arrangement (Staff Leader only, own campus
    /// implied — no campus parameter, other campuses can't be probed).
    /// Optional planned window drives non-blocking schedule-conflict warnings.
    /// </summary>
    [HttpGet("host-candidates")]
    [Authorize]
    public async Task<IActionResult> GetCreateHostCandidates(
        [FromQuery] DateTime? startAt,
        [FromQuery] DateTime? endAt,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCreateHostCandidatesQuery(startAt, endAt), cancellationToken);
        return Ok(result);
    }

    // ── Visitor edit / resubmit (SQL v10 resubmit_agenda_cancel24) ────────────────
    // All three endpoints are OWNER-only (Visitor): the handlers verify authentication,
    // role, ownership and the editable/resubmittable state — no OTP round-trip here.

    /// <summary>
    /// Loads the full form data of the caller's own request so the edit (pending) or
    /// resubmit (rejected) form can be prefilled.
    /// </summary>
    [HttpGet("{visitRequestId}/edit-detail")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult GetEditableDetail(ulong visitRequestId)
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    /// <summary>
    /// Reference read path — returns the fully per-campus resolved form via the central
    /// <c>IVisitFormReadService</c>, scoped to the caller. Gated by the <c>PerCampusFormV2</c>
    /// availability switch: 404 when it is off, which makes this endpoint unavailable rather than
    /// serving an older shape — there is no other form representation to serve.
    /// </summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}")]
    [Authorize]
    public async Task<IActionResult> GetFormV2(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVisitRequestFormV2Query(visitRequestId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 authenticated create. Gated by BOTH flags: the write flag OFF makes this 404
    /// (the v1 create flow is unchanged); write ON with read OFF is rejected. Idempotent by submissionId.
    /// </summary>
    [HttpPost("/api/v2/visit-requests")]
    [Authorize]
    public async Task<IActionResult> CreateFormV2(
        [FromBody] PEMS.Application.Common.DTOs.VisitRequestFormDataV2 form, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.CreateVisitRequestV2.CreateVisitRequestV2Command(form),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 PUBLIC initiate (STEP 1) — validates the FULL v2 form (same canonical rules as
    /// authenticated create-v2; NOT the v1 3-hour / mandatory-support rules), mints an OTP challenge, and
    /// BINDS the canonical v2 snapshot to the submit intent so <see cref="VerifyAndCreateFormV2"/> builds the
    /// request from exactly what was OTP-verified. No request is created here. Gated by BOTH flags: write OFF
    /// makes this 404 (the v1 initiate flow is unchanged).
    /// </summary>
    [HttpPost("/api/v2/visit-requests/initiate")]
    [AllowAnonymous]
    public async Task<IActionResult> InitiateFormV2(
        [FromBody] PEMS.Application.Delegations.Commands.InitiateVisitRequestV2.InitiateVisitRequestV2Command command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 PUBLIC (OTP-gated) create — the unauthenticated sibling of <see cref="CreateFormV2"/>.
    /// Verifies the OTP challenge (bound to the registrant email + submissionId), then creates the v2 request
    /// FROM THE SNAPSHOT BOUND AT INITIATE (never the verify-time form). Gated by BOTH flags: write OFF makes
    /// this 404 (the v1 public verify flow is unchanged). Retries of the same submission intent replay idempotently.
    /// </summary>
    [HttpPost("/api/v2/visit-requests/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyAndCreateFormV2(
        [FromBody] PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2.VerifyAndCreateVisitRequestV2Command command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// "Did my submission go through?" (plan §10) — resolves a submit intent to COMPLETED / PENDING /
    /// FAILED / NOT_FOUND. Exists because a connection dropped after the verify transaction commits looks,
    /// from the browser, exactly like one that never arrived: without this the visitor's only recourse was
    /// to submit again, which is how duplicate delegations are created.
    ///
    /// Anonymous by necessity (the public OTP flow has no session) and safe to be: the key is the caller's
    /// OWN client-minted submissionId, and the response carries only the request code and status — no
    /// registrant identity, no contact details, no form content. Read-only: it never verifies an OTP,
    /// never creates anything and never changes challenge state, so polling it costs the user nothing.
    /// </summary>
    [HttpGet("/api/v2/visit-requests/submissions/{submissionId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSubmissionResultV2(
        string submissionId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Queries.GetVisitSubmissionResult.GetVisitSubmissionResultQuery(submissionId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 PENDING EDIT — per-campus content/schedule/members (copy-on-write), add campus,
    /// remove pending campus; explicit optimistic concurrency (expected request + per-instance row versions →
    /// stable 409). Gated by BOTH flags like create-v2 (write OFF → 404). Editors: registrant or ACTIVE
    /// primary contact. Account-binding emails are immutable here (identity edit is a separate workflow).
    /// </summary>
    [HttpPut("/api/v2/visit-requests/{visitRequestId}/pending-edit")]
    [Authorize]
    public async Task<IActionResult> UpdatePendingFormV2(
        ulong visitRequestId,
        [FromBody] PEMS.Application.Common.DTOs.VisitRequestEditV2Dto edit,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.UpdatePendingVisitRequestV2.UpdatePendingVisitRequestV2Command(visitRequestId, edit),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 PENDING EDIT of ONE campus that is still waiting for its decision.
    ///
    /// <para>
    /// The endpoint above needs EVERY campus of the request to be waiting, because it rewrites data they
    /// share. This one asks only about the campus in the route, which is what makes a MIXED request
    /// workable: with HN approved, HCM waiting and DN refused, HCM is edited here, DN through
    /// <c>…/instances/{id}/resubmit</c>, and HN through safe-edit or an amendment — and none of the
    /// three touches the others.
    /// </para>
    /// <para>
    /// Open to the registrant and to the operational contact of THAT campus — a STAFF LEADER account
    /// among them, on a request they filed. The campus's OWN Staff Leader, when they are also the
    /// registrant, additionally may set a start inside the 72-hour registration floor (the first call
    /// answers 409 <c>LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED</c> and the client re-sends with
    /// <c>overrideLeadTimeConfirmed</c>) and may pass <c>approveAfterSave</c>, which saves and approves
    /// in ONE transaction. A Staff Leader who did not file the request is refused here (403) and decides
    /// the campus through the ordinary approve/reject endpoints instead.
    /// </para>
    /// </summary>
    [HttpPut("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/pending-edit")]
    [Authorize]
    public async Task<IActionResult> UpdatePendingInstanceFormV2(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] PEMS.Application.Common.DTOs.VisitInstancePendingEditDto body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2
                .UpdatePendingVisitInstanceV2Command(
                    visitRequestId, visitInstanceId, body.Content,
                    body.OverrideLeadTimeConfirmed, body.ApproveAfterSave),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Visitor edits a still-fully-pending request (every campus WAITING_REQUEST_APPROVAL,
    /// ≥ 24h before the earliest start). Campus list may change; status stays PENDING_APPROVAL.
    /// </summary>
    [HttpPut("{visitRequestId}/pending-edit")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult UpdatePending(ulong visitRequestId)
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    /// <summary>
    /// Per-campus form v2 RESUBMIT after full rejection — campus set fixed, every visitInstanceId kept, old
    /// decisions snapshotted to audit before being cleared, instances re-routed to the current Staff Leaders.
    /// Same two-flag gate, editor policy and optimistic-concurrency contract as pending-edit v2.
    /// </summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/resubmit")]
    [Authorize]
    public async Task<IActionResult> ResubmitFormV2(
        ulong visitRequestId,
        [FromBody] PEMS.Application.Common.DTOs.VisitRequestEditV2Dto edit,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequestV2.ResubmitRejectedVisitRequestV2Command(visitRequestId, edit),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Per-campus form v2 RESUBMIT of ONE rejected campus, leaving every sibling untouched.
    ///
    /// <para>
    /// Separate from the whole-request resubmit above because that one requires EVERY campus to be
    /// rejected and resets all of them. Open to the registrant and to the confirmed operational contact
    /// of THIS campus — the handler proves the campus belongs to the request and that the caller holds
    /// that campus, so naming a sibling is refused rather than quietly accepted.
    /// </para>
    /// </summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/resubmit")]
    [Authorize]
    public async Task<IActionResult> ResubmitInstanceFormV2(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] PEMS.Application.Common.DTOs.CampusVisitEditV2Dto content,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2
                .ResubmitRejectedVisitInstanceV2Command(visitRequestId, visitInstanceId, content),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Visitor edits &amp; resubmits a fully-rejected request. The campus set must stay the
    /// same; old decisions are snapshotted to audit before being cleared.
    /// </summary>
    [HttpPost("{visitRequestId}/resubmit")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult Resubmit(ulong visitRequestId)
    {
        return StatusCode(StatusCodes.Status410Gone, new { errorCode = "VISIT_FORM_V1_RETIRED", message = "Phiên bản biểu mẫu cũ không còn được hỗ trợ." });
    }

    // ── Per-campus operational-contact confirmation and transfer (plan §3.2, §3.3, §5.2) ─────
    // Every mutation names BOTH the request and the campus, and the handler proves the campus
    // belongs to that request before touching anything: there is no request-wide contact action.
    //
    // The generic /api/public/email-actions handler REJECTS these contexts on purpose: it is a
    // GET-executes-the-action door, and taking on (or refusing) a campus must never be answered by a
    // mail scanner following a link. These endpoints replace it with the safe shape — an anonymous,
    // masked, read-only landing GET, and a POST the reader triggers from that page.
    //
    // The POSTs come in two flavours for the same two answers:
    //   • /api/operational-contact-confirmations/{token}/{accept,decline} — signed in. The session's
    //     address must match the invitation.
    //   • /api/public/operational-contact-confirmations/{token}/{accept,decline} — NOT signed in.
    //     The invited person is usually an external guest with no PEMS account, and demanding one
    //     before they may answer is why invitations went unanswered and campuses sat behind the
    //     confirmation gate. The single-use, action-bound, address-bound token is the authorization.

    /// <summary>Anonymous masked landing summary for a confirmation link (either kind).</summary>
    [HttpGet("/api/public/operational-contact-confirmations/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOperationalContactConfirmationInfo(
        string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.GetOperationalContactConfirmationInfoQuery(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited person takes on ONE campus. Requires a session matching the invited address.</summary>
    [HttpPost("/api/operational-contact-confirmations/{token}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptOperationalContactConfirmation(
        string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.AcceptOperationalContactConfirmationCommand(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited person declines ONE campus. Same authentication bar as accepting.</summary>
    [HttpPost("/api/operational-contact-confirmations/{token}/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineOperationalContactConfirmation(
        string token,
        [FromBody] DeclineOperationalContactRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.DeclineOperationalContactConfirmationCommand(token, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The invited person takes on ONE campus WITHOUT signing in, from the confirmation page the
    /// email's "Xác nhận" button opened. POST only — a GET here would be executed by link prefetchers.
    /// The handler links the existing account for the invited address, or provisions the Visitor
    /// account a later Google sign-in with that address will resolve to.
    /// </summary>
    [HttpPost("/api/public/operational-contact-confirmations/{token}/accept")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicAcceptOperationalContactConfirmation(
        string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.PublicAcceptOperationalContactConfirmationCommand(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The invited person declines ONE campus without signing in. No account is provisioned: refusing
    /// a role is not a reason to acquire an account.
    /// </summary>
    [HttpPost("/api/public/operational-contact-confirmations/{token}/decline")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicDeclineOperationalContactConfirmation(
        string token,
        [FromBody] DeclineOperationalContactRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.PublicDeclineOperationalContactConfirmationCommand(token, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// "Lời mời đầu mối của tôi" — the outstanding invitations addressed to the signed-in account's
    /// own address, so an invitee who is already in PEMS can answer without going back to their inbox.
    /// A limited summary only: a pending invitee is not yet the contact of anything.
    /// </summary>
    [HttpGet("/api/v2/me/operational-contact-invitations")]
    [Authorize]
    public async Task<IActionResult> GetMyOperationalContactInvitations(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.GetMyOperationalContactInvitationsQuery(),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Signed-in invitee accepts one of their own invitations, by id rather than by link.</summary>
    [HttpPost("/api/v2/me/operational-contact-invitations/{identityChangeId}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptMyOperationalContactInvitation(
        ulong identityChangeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.AcceptOperationalContactConfirmationCommand(
                Token: null, ActingUserId: null, IdentityChangeId: identityChangeId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Signed-in invitee declines one of their own invitations, by id rather than by link.</summary>
    [HttpPost("/api/v2/me/operational-contact-invitations/{identityChangeId}/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineMyOperationalContactInvitation(
        ulong identityChangeId,
        [FromBody] DeclineOperationalContactRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.DeclineOperationalContactConfirmationCommand(
                Token: null, Reason: body?.Reason, ActingUserId: null, IdentityChangeId: identityChangeId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Current contact + in-flight invitation of ONE campus (masked).</summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact")]
    [Authorize]
    public async Task<IActionResult> GetOperationalContactState(
        ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.GetOperationalContactStateQuery(visitRequestId, visitInstanceId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Re-sends ONE campus's pending invitation. Old link dies first; token version bumps.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact-confirmation/resend")]
    [Authorize]
    public async Task<IActionResult> ResendOperationalContactConfirmation(
        ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.ResendOperationalContactConfirmationCommand(visitRequestId, visitInstanceId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Opens a NEW invitation for the address this campus already names, when the previous one ended
    /// unanswered (cancelled / declined / expired) so there is nothing left to resend. Refused when the
    /// campus already has a confirmed contact or an invitation still in flight — the latter is a
    /// resend, above.
    /// </summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact-confirmation/reinvite")]
    [Authorize]
    public async Task<IActionResult> ReinviteOperationalContactConfirmation(
        ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.ReinviteOperationalContactConfirmationCommand(visitRequestId, visitInstanceId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Saves ONE campus's operational contact. The SERVER decides what the save means, by comparing the
    /// submitted address with the stored one.
    ///
    /// <para>
    /// Same address → the person's details are corrected and nothing else happens: no invitation, no
    /// email, no change to who holds the campus. Different address → the canonical identity workflow
    /// runs, which is a replace while the campus is undecided (clears the relation, re-closes the global
    /// gate until answered) and a transfer once it has been decided (nothing moves until the invited
    /// person accepts).
    /// </para>
    /// <para>
    /// One endpoint on purpose. Two would ask the CLIENT to classify the edit, and a client that got it
    /// wrong would either send a confirmation email for a corrected phone number or change who runs a
    /// campus without one.
    /// </para>
    /// </summary>
    [HttpPut("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact")]
    [Authorize]
    public async Task<IActionResult> SaveOperationalContact(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] OperationalContactPayload body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.SaveOperationalContactCommand(
                visitRequestId, visitInstanceId,
                body.FullName, body.Organization, body.JobTitle, body.Phone, body.Email,
                body.Reason, body.ExpectedRowVersion),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Proposes handing ONE decided campus to a new address. Nothing moves until that person accepts.
    /// </summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact/transfer")]
    [Authorize]
    public async Task<IActionResult> InitiateOperationalContactTransfer(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] OperationalContactTransferPayload body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.InitiateOperationalContactTransferCommand(
                visitRequestId, visitInstanceId,
                body.FullName, body.Organization, body.JobTitle, body.Phone, body.Email, body.Reason),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Closes ONE campus's in-flight invitation without changing who holds the campus.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/operational-contact/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOperationalContactChange(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] DeclineOperationalContactRequest? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.OperationalContact.CancelOperationalContactChangeCommand(visitRequestId, visitInstanceId, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sets, changes or clears ONE campus's PROPOSED reception host ("Host dự kiến") while the
    /// request is still pre-decision.
    ///
    /// <para>
    /// Campus-scoped, and it never touches the current host: a campus that already has one refuses
    /// this call and points at the handover flow. Storing a proposal is not an assignment — it is
    /// activated, and revalidated, only when the confirmation gate opens.
    /// </para>
    /// </summary>
    [HttpPut("/api/v2/visit-requests/{visitRequestId}/campuses/{visitInstanceId}/proposed-host")]
    [Authorize]
    public async Task<IActionResult> UpdateProposedHost(
        ulong visitRequestId,
        ulong visitInstanceId,
        [FromBody] ProposedHostPayload body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.UpdateProposedHost.UpdateProposedHostCommand(
                visitRequestId, visitInstanceId,
                body.HostSelectionMode, body.ProposedHostUserId, body.RowVersion),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The reception-host arrangement of one campus. <c>hostSelectionMode</c> is
    /// SELF | SELECTED | WAIT_FOR_LATER; <c>proposedHostUserId</c> is required for SELECTED, ignored
    /// for SELF (resolved from the session) and must be absent for WAIT_FOR_LATER.
    /// </summary>
    public sealed record ProposedHostPayload(
        string HostSelectionMode, ulong? ProposedHostUserId, int RowVersion);

    /// <summary>Optional free-text reason for declining or cancelling an invitation.</summary>
    public sealed record DeclineOperationalContactRequest(string? Reason);

    /// <summary>
    /// The five contact fields as the user filled them in, for ONE campus. Organization is optional.
    ///
    /// <para>
    /// <c>Reason</c> is used only if the save turns out to be a transfer, and
    /// <c>ExpectedRowVersion</c> only if it turns out to be a metadata correction — the client sends
    /// what it has and the server decides which of the two, if either, applies. Both are optional so an
    /// older client keeps working.
    /// </para>
    /// </summary>
    public sealed record OperationalContactPayload(
        string FullName, string? Organization, string JobTitle, string? Phone, string Email,
        string? Reason = null, int? ExpectedRowVersion = null);

    /// <summary>A transfer proposal: the same details, plus why the campus is changing hands.</summary>
    public sealed record OperationalContactTransferPayload(
        string FullName, string? Organization, string JobTitle, string? Phone, string Email, string? Reason);

    // ── Per-campus v2 safe edit + amendments (plan §16.6, Phase E) ───────────────────────────
    // The backend classifier is the only authority: the safe endpoint fails closed on anything
    // approval-sensitive; approval-sensitive/structural changes of a DECIDED campus go through
    // per-campus amendments and the ACTIVE snapshot never moves before the Staff Leader approves.

    /// <summary>Applies safe/correction fields immediately (registrant/contact display data, notes,
    /// media consent — a consent WITHDRAWAL applies even &lt;24h with an URGENT notification).</summary>
    [HttpPatch("/api/v2/visit-requests/{visitRequestId}/safe-details")]
    [Authorize]
    public async Task<IActionResult> PatchSafeDetails(
        ulong visitRequestId,
        [FromBody] PEMS.Application.Common.DTOs.VisitRequestSafeEditDto patch,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.SubmitVisitSafeEditCommand(visitRequestId, patch),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Submits an approval-sensitive change proposal for ONE decided campus instance.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/amendments")]
    [Authorize]
    public async Task<IActionResult> SubmitAmendment(
        ulong visitRequestId, ulong visitInstanceId,
        [FromBody] PEMS.Application.Common.DTOs.VisitAmendmentProposalDto proposal,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.SubmitVisitAmendmentCommand(
                visitRequestId, visitInstanceId, proposal),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The instance's pending amendment (scoped; null when none).</summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/amendments/active")]
    [Authorize]
    public async Task<IActionResult> GetActiveAmendment(
        ulong visitRequestId, ulong visitInstanceId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.GetActiveVisitAmendmentQuery(
                visitRequestId, visitInstanceId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Requester withdraws their pending amendment; the active snapshot stays.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/instances/{visitInstanceId}/amendments/{amendmentId}/withdraw")]
    [Authorize]
    public async Task<IActionResult> WithdrawAmendment(
        ulong visitRequestId, ulong visitInstanceId, ulong amendmentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.WithdrawVisitAmendmentCommand(
                visitRequestId, visitInstanceId, amendmentId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Current campus Staff Leader APPROVES the amendment — the patch applies atomically,
    /// form+approval revisions bump, sibling campuses and approval statuses never reset.</summary>
    [HttpPost("/api/v2/visit-instances/{visitInstanceId}/amendments/{amendmentId}/approve")]
    [Authorize]
    public async Task<IActionResult> ApproveAmendment(
        ulong visitInstanceId, ulong amendmentId,
        [FromBody] AmendmentDecisionBody? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.ApproveVisitAmendmentCommand(
                visitInstanceId, amendmentId, body?.Note),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Current campus Staff Leader REJECTS the amendment (reason required); nothing changes.</summary>
    [HttpPost("/api/v2/visit-instances/{visitInstanceId}/amendments/{amendmentId}/reject")]
    [Authorize]
    public async Task<IActionResult> RejectAmendment(
        ulong visitInstanceId, ulong amendmentId,
        [FromBody] AmendmentDecisionBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.RejectVisitAmendmentCommand(
                visitInstanceId, amendmentId, body.Note ?? string.Empty),
            cancellationToken);
        return Ok(result);
    }

    public sealed record AmendmentDecisionBody(string? Note);

    /// <summary>
    /// Current campus Staff Leader hands this instance's Host role to a different eligible user, after
    /// the campus was approved. Deliberately NOT the approve-and-assign endpoint: that one gives a
    /// campus its first Host as part of the approval decision and refuses to run twice, so it cannot
    /// express a handover — no before/after, no notification to the outgoing Host, and it would have to
    /// re-open a settled approval to run at all.
    /// </summary>
    [HttpPost("/api/v2/visit-instances/{visitInstanceId}/host-transfer")]
    [Authorize]
    public async Task<IActionResult> TransferHost(
        ulong visitInstanceId,
        [FromBody] PEMS.Application.Delegations.Commands.TransferVisitHost.TransferVisitHostBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.TransferVisitHost.TransferVisitHostCommand(
                visitInstanceId, body.NewHostUserId, body.Reason, body.ExpectedRowVersion),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Scoped, masked business-history timeline of the request.</summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.GetVisitRequestHistoryQuery(visitRequestId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Clears the caller's unread-change badge for this request. Called when the DETAIL screen opens,
    /// never when a row appears in a list — a badge that clears itself on scroll spends the one signal
    /// telling the reader to look, before they have looked.
    /// </summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/changes/seen")]
    [Authorize]
    public async Task<IActionResult> MarkChangesSeen(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var marked = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.MarkVisitChangesSeenCommand(visitRequestId),
            cancellationToken);
        return Ok(new { markedCount = marked });
    }

    /// <summary>
    /// What actually changed in ONE timeline event: field before/after plus who joined or left the
    /// delegation. Same scoping as the timeline, and an event outside the caller's campuses answers
    /// 404 rather than 403 — a refusal would confirm the campus exists.
    /// </summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}/history/{eventId}")]
    [Authorize]
    public async Task<IActionResult> GetHistoryDetail(
        ulong visitRequestId, string eventId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitAmendments.GetVisitHistoryDetailQuery(
                visitRequestId, eventId),
            cancellationToken);
        return Ok(result);
    }
}
