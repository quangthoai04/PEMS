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

        [HttpGet]
        public async Task<IActionResult> GetFeedbacks([FromQuery] PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback.SearchAndFilterFeedbackQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("visit-summary")]
        public async Task<IActionResult> GetVisitSummary([FromQuery] PEMS.Application.Feedbacks.Queries.ViewFeedbackSummary.ViewFeedbackSummaryQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFeedbackDetail(ulong id)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }
        
        [HttpGet("visit-summary/{visitRequestId}")]
        public async Task<IActionResult> GetVisitSummaryDetail(ulong visitRequestId)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }
        
        [HttpGet("visit-summary/{visitRequestId}/instances/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitInstanceSummaryDetail(ulong visitRequestId, ulong visitInstanceId)
        {
            // Dummy implementation for now to prevent 404
            return Ok(new { });
        }

    }
}
