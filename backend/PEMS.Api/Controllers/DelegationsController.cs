using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;
using PEMS.Application.Delegations.Commands.CancelVisitRequest;
using PEMS.Application.Delegations.Commands.ProcessVisitRequest;
using PEMS.Application.Delegations.Commands.RejectVisitRequest;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Queries.GetHostCandidates;
using PEMS.Application.Delegations.Queries.ViewMyVisitInvitations;
using PEMS.Domain.Constants;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DelegationsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DelegationsController(IMediator mediator) => _mediator = mediator;

        // UC-17 Submit Visit Request is the public, OTP-verified flow in VisitRequestsController:
        //   POST /api/visit-requests/initiate | /verify | /resend-otp
        // The former DelegationsController.submitvisitrequest scaffold (NotImplementedException)
        // was removed — it was unused and conflicted with the real UC-17.

        // ── UC-18 HO approve / reject a MULTI_CAMPUS request ──────────────────
        // Approve: request → APPROVED and every campus instance is auto-assigned to its IC head.
        [HttpPost("{visitRequestId}/ho-approve")]

        public async Task<IActionResult> HoApprove(ulong visitRequestId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ApproveCrossCampusRequestCommand(visitRequestId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{visitRequestId}/ho-reject")]

        public async Task<IActionResult> HoReject(ulong visitRequestId, [FromBody] RejectVisitRequestBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RejectVisitRequestCommand(visitRequestId, body.Reason), cancellationToken);
            return Ok(result);
        }

        // ── Submitted visit-request form snapshot (read-only, shared) ────────
        // The "what the guest submitted" detail, reused by the pre-approval review, the
        // approved/waiting-host detail and the rejected detail screens. Role/scope/status
        // visibility is enforced in the handler (HO → MULTI_CAMPUS; Staff Leader → own-campus
        // SINGLE_CAMPUS any status, or own-campus MULTI_CAMPUS only after HO approval; Visitor →
        // own request). Never mutates anything; decisions use the ho-approve / assign-host /
        // reject endpoints above.
        [HttpGet("visit-requests/{visitRequestId}/submitted-form-detail")]
        public async Task<IActionResult> GetSubmittedVisitRequestFormDetail(ulong visitRequestId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Delegations.Queries.GetSubmittedVisitRequestFormDetail.GetSubmittedVisitRequestFormDetailQuery(visitRequestId),
                cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewguestdelegationdetails")]
        public async Task<IActionResult> ViewGuestDelegationDetails([FromQuery] PEMS.Application.Delegations.Queries.ViewGuestDelegationDetails.ViewGuestDelegationDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewguestdelegationlist")]

        public async Task<IActionResult> ViewGuestDelegationList([FromQuery] PEMS.Application.Delegations.Queries.ViewGuestDelegationList.ViewGuestDelegationListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchdelegations")]
        public async Task<IActionResult> SearchDelegations([FromQuery] PEMS.Application.Delegations.Queries.SearchDelegations.SearchDelegationsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // ── UC-22 Staff Leader: list host candidates, approve+assign host (single) /
        //    transfer host (multi), and reject own-campus single requests ─────────
        [HttpGet("campuses/{visitInstanceId}/host-candidates")]

        public async Task<IActionResult> GetHostCandidates(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetHostCandidatesQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/assign-host")]

        public async Task<IActionResult> AssignHost(ulong visitRequestId, ulong visitInstanceId, [FromBody] AssignHostBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ProcessVisitRequestCommand(visitRequestId, visitInstanceId, body.HostUserId), cancellationToken);
            return Ok(result);
        }

        [HttpPost("{visitRequestId}/campus-reject")]

        public async Task<IActionResult> CampusReject(ulong visitRequestId, [FromBody] RejectVisitRequestBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RejectVisitRequestCommand(visitRequestId, body.Reason), cancellationToken);
            return Ok(result);
        }

        [HttpPost("createguestdelegation")]
        public async Task<IActionResult> CreateGuestDelegation([FromBody] PEMS.Application.Delegations.Commands.CreateGuestDelegation.CreateGuestDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateguestdelegation")]
        public async Task<IActionResult> UpdateGuestDelegation([FromBody] PEMS.Application.Delegations.Commands.UpdateGuestDelegation.UpdateGuestDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("preparevisitlogistics")]
        public async Task<IActionResult> PrepareVisitLogistics([FromBody] PEMS.Application.Delegations.Commands.PrepareVisitLogistics.PrepareVisitLogisticsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatevisitlogistics")]
        public async Task<IActionResult> UpdateVisitLogistics([FromBody] PEMS.Application.Delegations.Commands.UpdateVisitLogistics.UpdateVisitLogisticsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirmparticipation")]
        public async Task<IActionResult> ConfirmParticipation([FromBody] PEMS.Application.Delegations.Commands.ConfirmParticipation.ConfirmParticipationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ── UC-27 Confirm Participation: an invitee's own participation invitations ─────
        // The pending invitations to respond to (and optionally the responded history).
        [HttpGet("my-invitations")]

        public async Task<IActionResult> GetMyInvitations([FromQuery] bool includeResponded, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new ViewMyVisitInvitationsQuery { IncludeResponded = includeResponded }, cancellationToken);
            return Ok(result);
        }

        // A single invitation for the invitation-detail screen (ownership enforced server-side).
        [HttpGet("invitations/{participantId}")]

        public async Task<IActionResult> GetInvitation(ulong participantId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInvitationByIdQuery(participantId), cancellationToken);
            return Ok(result);
        }

        // Accept / decline an invitation (decline requires a reason). Accepting makes the row
        // appear in the "Đơn mời tham dự" tab; this endpoint is NOT on that tab.
        [HttpPost("participants/{participantId}/respond")]

        public async Task<IActionResult> RespondInvitation(ulong participantId, [FromBody] RespondInvitationBody body, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RespondVisitParticipantInvitationCommand(participantId, body.Accept, body.DeclineReason), cancellationToken);
            return Ok(result);
        }

        [HttpPost("approveresourcerequest")]
        public async Task<IActionResult> ApproveResourceRequest([FromBody] PEMS.Application.Delegations.Commands.ApproveResourceRequest.ApproveResourceRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("proposeresourcemodification")]
        public async Task<IActionResult> ProposeResourceModification([FromBody] PEMS.Application.Delegations.Commands.ProposeResourceModification.ProposeResourceModificationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("confirmthechangeproposal")]
        public async Task<IActionResult> ConfirmTheChangeProposal([FromBody] PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal.ConfirmTheChangeProposalCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createmeetingminutes")]
        public async Task<IActionResult> CreateMeetingMinutes([FromBody] PEMS.Application.Delegations.Commands.CreateMeetingMinutes.CreateMeetingMinutesCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editmeetingminutes")]
        public async Task<IActionResult> EditMeetingMinutes([FromBody] PEMS.Application.Delegations.Commands.EditMeetingMinutes.EditMeetingMinutesCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewmeetingminutesdetails")]
        public async Task<IActionResult> ViewMeetingMinutesDetails([FromQuery] PEMS.Application.Delegations.Queries.ViewMeetingMinutesDetails.ViewMeetingMinutesDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("uploadattacheddocuments")]
        public async Task<IActionResult> UploadAttachedDocuments([FromBody] PEMS.Application.Delegations.Commands.UploadAttachedDocuments.UploadAttachedDocumentsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("submitdelegationfeedback")]
        public async Task<IActionResult> SubmitDelegationFeedback([FromBody] PEMS.Application.Delegations.Commands.SubmitDelegationFeedback.SubmitDelegationFeedbackCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("scanbusinesscard")]
        public async Task<IActionResult> ScanBusinessCard([FromBody] PEMS.Application.Delegations.Commands.ScanBusinessCard.ScanBusinessCardCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createpartnerprofile")]
        public async Task<IActionResult> CreatePartnerProfile([FromBody] PEMS.Application.Delegations.Commands.CreatePartnerProfile.CreatePartnerProfileCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("uploadvisitphotos")]
        public async Task<IActionResult> UploadVisitPhotos([FromBody] PEMS.Application.Delegations.Commands.UploadVisitPhotos.UploadVisitPhotosCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("tagfacesonphotos")]
        public async Task<IActionResult> TagFacesonPhotos([FromBody] PEMS.Application.Delegations.Commands.TagFacesonPhotos.TagFacesonPhotosCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createnewsarticle")]
        public async Task<IActionResult> CreateNewsArticle([FromBody] PEMS.Application.Delegations.Commands.CreateNewsArticle.CreateNewsArticleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("closedelegation")]
        public async Task<IActionResult> CloseDelegation([FromBody] PEMS.Application.Delegations.Commands.CloseDelegation.CloseDelegationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // ── UC-136 Cancel Visit Request (post-approval only) ──────────────────
        // Cancel the whole approved request (Visitor self-cancel, or Staff Leader / HO).
        [HttpPost("{visitRequestId}/cancel")]

        public async Task<IActionResult> CancelVisitRequest(
            ulong visitRequestId,
            [FromBody] CancelVisitRequestBody body,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CancelVisitRequestCommand(visitRequestId, null, body.CancellationReason), cancellationToken);
            return Ok(result);
        }

        // Cancel a single campus instance (current Host, after external confirmation from the guest).
        [HttpPost("{visitRequestId}/campuses/{visitInstanceId}/cancel")]

        public async Task<IActionResult> CancelVisitRequestCampus(
            ulong visitRequestId,
            ulong visitInstanceId,
            [FromBody] CancelVisitRequestBody body,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CancelVisitRequestCommand(visitRequestId, visitInstanceId, body.CancellationReason), cancellationToken);
            return Ok(result);
        }
    }

    /// <summary>Request body for the UC-136 cancel endpoints.</summary>
    public sealed record CancelVisitRequestBody(string CancellationReason);

    /// <summary>Request body for the UC-18/UC-22 reject endpoints (reason is mandatory).</summary>
    public sealed record RejectVisitRequestBody(string Reason);

    /// <summary>Request body for the UC-22 approve-and-assign / transfer-host endpoint.</summary>
    public sealed record AssignHostBody(ulong HostUserId);

    /// <summary>Request body for the UC-27 respond-to-invitation endpoint (decline requires a reason).</summary>
    public sealed record RespondInvitationBody(bool Accept, string? DeclineReason);
}
