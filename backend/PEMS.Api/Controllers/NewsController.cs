using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public NewsController(IMediator mediator) => _mediator = mediator;

        [HttpPost("approvenews")]
        public async Task<IActionResult> ApproveNews([FromBody] PEMS.Application.News.Commands.ApproveNews.ApproveNewsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("publishnews")]
        public async Task<IActionResult> PublishNews([FromBody] PEMS.Application.News.Commands.PublishNews.PublishNewsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewnewslist")]
        public async Task<IActionResult> ViewNewsList([FromQuery] PEMS.Application.News.Queries.ViewNewsList.ViewNewsListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewnewsdetails")]
        public async Task<IActionResult> ViewNewsDetails([FromQuery] PEMS.Application.News.Queries.ViewNewsDetails.ViewNewsDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("addmultilingualnews")]
        public async Task<IActionResult> AddMultilingualNews([FromBody] PEMS.Application.News.Commands.AddMultilingualNews.AddMultilingualNewsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("managenewsvisibility")]
        public async Task<IActionResult> ManageNewsVisibility([FromBody] PEMS.Application.News.Commands.ManageNewsVisibility.ManageNewsVisibilityCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editnews")]
        public async Task<IActionResult> EditNews([FromBody] PEMS.Application.News.Commands.EditNews.EditNewsCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}
