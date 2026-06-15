using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PartnersController : ControllerBase
    {
        private readonly IMediator _mediator;
        public PartnersController(IMediator mediator) => _mediator = mediator;

        [HttpPost("processpartnercreationrequest")]
        public async Task<IActionResult> ProcessPartnerCreationRequest([FromBody] PEMS.Application.Partners.Commands.ProcessPartnerCreationRequest.ProcessPartnerCreationRequestCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editpartnerinformation")]
        public async Task<IActionResult> EditPartnerInformation([FromBody] PEMS.Application.Partners.Commands.EditPartnerInformation.EditPartnerInformationCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewpartnerlists")]
        public async Task<IActionResult> ViewPartnerLists([FromQuery] PEMS.Application.Partners.Queries.ViewPartnerLists.ViewPartnerListsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchpartners")]
        public async Task<IActionResult> SearchPartners([FromQuery] PEMS.Application.Partners.Queries.SearchPartners.SearchPartnersQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewpartnerdetails")]
        public async Task<IActionResult> ViewPartnerDetails([FromQuery] PEMS.Application.Partners.Queries.ViewPartnerDetails.ViewPartnerDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
