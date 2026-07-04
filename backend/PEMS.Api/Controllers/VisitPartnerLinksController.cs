using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Partners.Commands.CreatePartnerFromGuest;
using PEMS.Application.Partners.VisitLinks.Commands.CreateOrUpdateVisitGuestPartnerLink;
using PEMS.Application.Partners.VisitLinks.Commands.RejectVisitGuestPartnerSuggestion;
using PEMS.Application.Partners.VisitLinks.Queries.GetVisitGuestPartnerLinks;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Links between visit guests / minute participants and partner profiles
    /// (docs/PARTNER_canh/01 §5.5). Access is checked per visit instance in the handlers.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/visit-instances/{visitInstanceId}/partner-links")]
    public class VisitPartnerLinksController : ControllerBase
    {
        private readonly IMediator _mediator;
        public VisitPartnerLinksController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetLinks(ulong visitInstanceId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetVisitGuestPartnerLinksQuery(visitInstanceId), cancellationToken));

        [HttpPost]
        public async Task<IActionResult> CreateLink(ulong visitInstanceId,
            [FromBody] CreateOrUpdateVisitGuestPartnerLinkCommand command, CancellationToken cancellationToken)
        {
            command.VisitInstanceId = visitInstanceId;
            command.LinkId = null;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPut("{linkId}")]
        public async Task<IActionResult> UpdateLink(ulong visitInstanceId, ulong linkId,
            [FromBody] CreateOrUpdateVisitGuestPartnerLinkCommand command, CancellationToken cancellationToken)
        {
            command.VisitInstanceId = visitInstanceId;
            command.LinkId = linkId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        [HttpPost("{linkId}/reject-suggestion")]
        public async Task<IActionResult> RejectSuggestion(ulong visitInstanceId, ulong linkId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new RejectVisitGuestPartnerSuggestionCommand(visitInstanceId, linkId), cancellationToken));

        /// <summary>Create partner from guest snapshot + link in one call (prefill flow).</summary>
        [HttpPost("create-partner")]
        public async Task<IActionResult> CreatePartnerFromGuest(ulong visitInstanceId,
            [FromBody] CreatePartnerFromGuestCommand command, CancellationToken cancellationToken)
        {
            command.VisitInstanceId = visitInstanceId;
            return Ok(await _mediator.Send(command, cancellationToken));
        }
    }
}
