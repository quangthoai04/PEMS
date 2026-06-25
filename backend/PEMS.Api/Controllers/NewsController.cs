using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/news")]
    public sealed class NewsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public NewsController(IMediator mediator) => _mediator = mediator;

        // UC-88: View News List
        [HttpGet]
        [RoleAuthorize(EffectiveRole.Ho, EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> GetNewsList(
            [FromQuery] PEMS.Application.News.Queries.ViewNewsList.ViewNewsListQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("approvenews")]
        public async Task<IActionResult> ApproveNews(
            [FromBody] PEMS.Application.News.Commands.ApproveNews.ApproveNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("publishnews")]
        public async Task<IActionResult> PublishNews(
            [FromBody] PEMS.Application.News.Commands.PublishNews.PublishNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewnewsdetails")]
        public async Task<IActionResult> ViewNewsDetails(
            [FromQuery] PEMS.Application.News.Queries.ViewNewsDetails.ViewNewsDetailsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("addmultilingualnews")]
        public async Task<IActionResult> AddMultilingualNews(
            [FromBody] PEMS.Application.News.Commands.AddMultilingualNews.AddMultilingualNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("managenewsvisibility")]
        public async Task<IActionResult> ManageNewsVisibility(
            [FromBody] PEMS.Application.News.Commands.ManageNewsVisibility.ManageNewsVisibilityCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("editnews")]
        public async Task<IActionResult> EditNews(
            [FromBody] PEMS.Application.News.Commands.EditNews.EditNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}
