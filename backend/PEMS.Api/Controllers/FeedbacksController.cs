using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbacksController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FeedbacksController(IMediator mediator) => _mediator = mediator;

        [HttpGet("searchandfilterfeedback")]
        public async Task<IActionResult> SearchAndFilterFeedback([FromQuery] PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback.SearchAndFilterFeedbackQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewfeedbacksummary")]
        public async Task<IActionResult> ViewFeedbackSummary([FromQuery] PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary.ViewFeedbackSummaryQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
