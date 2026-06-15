using MediatR;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("submitvisitrequest")]
        public async Task<IActionResult> SubmitVisitRequest([FromBody] PEMS.Application.Delegations.Commands.SubmitVisitRequest.SubmitVisitRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("approvecrosscampusrequest")]
        public async Task<IActionResult> ApproveCrossCampusRequest([FromBody] PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest.ApproveCrossCampusRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
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

        [HttpPost("processvisitrequest")]
        public async Task<IActionResult> ProcessVisitRequest([FromBody] PEMS.Application.Delegations.Commands.ProcessVisitRequest.ProcessVisitRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
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

    }
}
