using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.Commands.CreateAuthenticatedVisitRequest;
using PEMS.Application.Delegations.Commands.InitiateVisitRequest;
using PEMS.Application.Delegations.Commands.RecoverVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitRequest;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitRequest;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequest;
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
    [ProducesResponseType(typeof(InitiateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Initiate(
        [FromBody] InitiateVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
    [ProducesResponseType(typeof(VerifyAndCreateVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyAndCreateVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// RESEND — Supersedes the old challenge and returns a NEW <c>sessionToken</c> with a
    /// fresh code. Subject to per-email hourly issue quotas + min resend interval. A
    /// challenge that already requires human verification cannot be resent (428).
    /// </summary>
    [HttpPost("resend-otp")]
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
    /// as the public flow. Per-campus processing modes (SELF_HOST / ASSIGN_HOST) are only
    /// honoured on the caller's own campus and only for Staff/Staff Leader; the handler
    /// revalidates role, campus scope and host candidate from the DB.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CreateAuthenticatedVisitRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAuthenticated(
        [FromBody] CreateAuthenticatedVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Host candidates for the authenticated create form's ASSIGN_HOST mode (Staff Leader
    /// only, own campus implied — no campus parameter, other campuses can't be probed).
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
    public async Task<IActionResult> GetEditableDetail(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEditableVisitRequestDetailQuery(visitRequestId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PR-3 reference read path — returns the fully per-campus resolved form via the central
    /// dual-read <c>IVisitFormReadService</c> (v1 or v2, correctly scoped to the caller). Gated by
    /// the <c>PerCampusFormV2</c> feature flag: 404 when the flag is OFF. v1 endpoints are unchanged.
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
    /// Per-campus form v2 PUBLIC (OTP-gated) create — the unauthenticated sibling of <see cref="CreateFormV2"/>.
    /// Verifies the OTP challenge (bound to the registrant email + submissionId), then creates the v2 request.
    /// Gated by BOTH flags: write OFF makes this 404 (the v1 public verify flow is unchanged). Retries of the
    /// same submission intent replay idempotently.
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
    /// Visitor edits a still-fully-pending request (every campus WAITING_REQUEST_APPROVAL,
    /// ≥ 24h before the earliest start). Campus list may change; status stays PENDING_APPROVAL.
    /// </summary>
    [HttpPut("{visitRequestId}/pending-edit")]
    [Authorize]
    public async Task<IActionResult> UpdatePending(
        ulong visitRequestId,
        [FromBody] UpdatePendingVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { VisitRequestId = visitRequestId }, cancellationToken);
        return Ok(result);
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
    /// Visitor edits &amp; resubmits a fully-rejected request. The campus set must stay the
    /// same; old decisions are snapshotted to audit before being cleared.
    /// </summary>
    [HttpPost("{visitRequestId}/resubmit")]
    [Authorize]
    public async Task<IActionResult> Resubmit(
        ulong visitRequestId,
        [FromBody] ResubmitRejectedVisitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { VisitRequestId = visitRequestId }, cancellationToken);
        return Ok(result);
    }

    // ── Per-campus v2 primary-contact INITIAL_CLAIM (plan §16.4) ─────────────────────────────
    // The generic /api/public/email-actions handler REJECTS the claim context: possession of the
    // email link alone never applies a claim. The landing page is anonymous + masked-only; accept
    // and decline require a logged-in session whose email matches the invitation.

    /// <summary>Anonymous masked landing summary for a contact-claim link.</summary>
    [HttpGet("/api/public/visit-contact-claims/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContactClaimInfo(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactClaim.GetVisitContactClaimInfoQuery(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited contact (logged in with the matching Google account) ACCEPTS the claim:
    /// visitor_user_id is linked and the contact becomes ACTIVE in one transaction.</summary>
    [HttpPost("/api/v2/visit-contact-claims/{token}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptContactClaim(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactClaim.AcceptVisitContactClaimCommand(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited contact DECLINES the claim. The request is not cancelled.</summary>
    [HttpPost("/api/v2/visit-contact-claims/{token}/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineContactClaim(
        string token,
        [FromBody] DeclineContactClaimBody? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactClaim.DeclineVisitContactClaimCommand(
                token, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

    public sealed record DeclineContactClaimBody(string? Reason);

    /// <summary>Registrant re-sends the pending contact invitation (old links die, 72h restarts).</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/contact-claim/resend")]
    [Authorize]
    public async Task<IActionResult> ResendContactClaim(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactClaim.ResendVisitContactClaimCommand(visitRequestId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Registrant replaces the still-unclaimed pending contact (typo fix): supersedes the old
    /// invitation and either links the registrant (same email) or invites the new email.</summary>
    [HttpPut("/api/v2/visit-requests/{visitRequestId}/contact-claim")]
    [Authorize]
    public async Task<IActionResult> ReplacePendingContact(
        ulong visitRequestId,
        [FromBody] ReplacePendingContactBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactClaim.ReplacePendingVisitContactCommand(
                visitRequestId, body.FullName, body.Organization, body.Phone, body.Email),
            cancellationToken);
        return Ok(result);
    }

    public sealed record ReplacePendingContactBody(string FullName, string Organization, string Phone, string Email);

    // ── Per-campus v2 primary-contact TRANSFER, 24h (plan §16.4/§4.4, D-4) ───────────────────
    // The current ACTIVE owner keeps every right until the invited person logs in with the matching
    // Google account and explicitly accepts. The generic anonymous email-action handler rejects
    // the transfer context; the anonymous landing below is masked-only and mutation-free.

    /// <summary>Registrant or current ACTIVE contact proposes handing the contact role to a new email.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/contact-transfer")]
    [Authorize]
    public async Task<IActionResult> InitiateContactTransfer(
        ulong visitRequestId,
        [FromBody] InitiateContactTransferBody body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.InitiateVisitContactTransferCommand(
                visitRequestId, body.FullName, body.Organization, body.Phone, body.Email, body.Reason),
            cancellationToken);
        return Ok(result);
    }

    public sealed record InitiateContactTransferBody(
        string FullName, string Organization, string Phone, string Email, string? Reason);

    /// <summary>Owner-side state of the pending transfer (masked email only).</summary>
    [HttpGet("/api/v2/visit-requests/{visitRequestId}/contact-transfer")]
    [Authorize]
    public async Task<IActionResult> GetActiveContactTransfer(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.GetActiveVisitContactTransferQuery(visitRequestId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Re-sends the pending transfer invitation (old links die, 24h restarts).</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/contact-transfer/resend")]
    [Authorize]
    public async Task<IActionResult> ResendContactTransfer(ulong visitRequestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.ResendVisitContactTransferCommand(visitRequestId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Cancels the pending transfer; the current owner stays ACTIVE.</summary>
    [HttpPost("/api/v2/visit-requests/{visitRequestId}/contact-transfer/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelContactTransfer(
        ulong visitRequestId,
        [FromBody] CancelContactTransferBody? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.CancelVisitContactTransferCommand(
                visitRequestId, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

    public sealed record CancelContactTransferBody(string? Reason);

    /// <summary>Anonymous masked landing summary for a contact-transfer link.</summary>
    [HttpGet("/api/public/visit-contact-transfers/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContactTransferInfo(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.GetVisitContactTransferInfoQuery(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited person (logged in with the matching Google account) ACCEPTS the transfer:
    /// visitor_user_id + the contact snapshot swap in one transaction; the old account stays ACTIVE.</summary>
    [HttpPost("/api/v2/visit-contact-transfers/{token}/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptContactTransfer(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.AcceptVisitContactTransferCommand(token),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>The invited person DECLINES the transfer. The current owner keeps everything.</summary>
    [HttpPost("/api/v2/visit-contact-transfers/{token}/decline")]
    [Authorize]
    public async Task<IActionResult> DeclineContactTransfer(
        string token,
        [FromBody] DeclineContactClaimBody? body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new PEMS.Application.Delegations.Commands.VisitContactTransfer.DeclineVisitContactTransferCommand(
                token, body?.Reason),
            cancellationToken);
        return Ok(result);
    }

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
}
