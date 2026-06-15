using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaqsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FaqsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewlistfaq")]
        public async Task<IActionResult> ViewListFAQ([FromQuery] PEMS.Application.Faqs.Queries.ViewListFAQ.ViewListFAQQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createfaq")]
        public async Task<IActionResult> CreateFAQ([FromBody] PEMS.Application.Faqs.Commands.CreateFAQ.CreateFAQCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatefaq")]
        public async Task<IActionResult> UpdateFAQ([FromBody] PEMS.Application.Faqs.Commands.UpdateFAQ.UpdateFAQCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("changefaqvisibility")]
        public async Task<IActionResult> ChangeFAQVisibility([FromBody] PEMS.Application.Faqs.Commands.ChangeFAQVisibility.ChangeFAQVisibilityCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchfaq")]
        public async Task<IActionResult> SearchFAQ([FromQuery] PEMS.Application.Faqs.Queries.SearchFAQ.SearchFAQQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
