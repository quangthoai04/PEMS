using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingMinutesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public MeetingMinutesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewminuteslist")]
        public async Task<IActionResult> ViewMinutesList([FromQuery] PEMS.Application.MeetingMinutes.Queries.ViewMinutesList.ViewMinutesListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchandfilterminutes")]
        public async Task<IActionResult> SearchAndFilterMinutes([FromQuery] PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes.SearchAndFilterMinutesQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
