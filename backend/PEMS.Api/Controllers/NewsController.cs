using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.News;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/news")]
    public sealed class NewsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public NewsController(IMediator mediator) => _mediator = mediator;

        // ── Tin tức gắn với 1 campus instance (Phase 4) — nhiều bài / instance ──
        // List posts of an instance (Visitor sees only published).
        [HttpGet("visit-instances/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitInstanceNews(ulong visitInstanceId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetVisitInstanceNewsQuery(visitInstanceId), cancellationToken));

        // Create a post (Host / accepted IC-Staff / Student) → PENDING_REVIEW.
        [HttpPost("visit-instances/{visitInstanceId}")]
        public async Task<IActionResult> CreateVisitInstanceNews(ulong visitInstanceId, [FromBody] CreateVisitInstanceNewsBody body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new CreateVisitInstanceNewsCommand(visitInstanceId, body.Title, body.Summary, body.Body), cancellationToken));

        // Edit a not-yet-published post (author or Host) → resubmits for review.
        [HttpPut("visit-instance-news/{newsId}")]
        public async Task<IActionResult> UpdateVisitInstanceNews(ulong newsId, [FromBody] UpdateVisitInstanceNewsBody body, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new UpdateVisitInstanceNewsCommand(newsId, body.Title, body.Summary, body.Body, body.RowVersion), cancellationToken));

        // Re-submit a post for review (e.g. after rejection).
        [HttpPost("visit-instance-news/{newsId}/submit-review")]
        public async Task<IActionResult> SubmitVisitInstanceNews(ulong newsId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SubmitVisitInstanceNewsCommand(newsId), cancellationToken));

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

        // Create News support: get eligible closed visit instances
        [HttpGet("eligible-visit-instances")]
        [RoleAuthorize(EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> GetEligibleVisitInstances(
            [FromQuery] bool includeAlreadyHasNews = false,
            CancellationToken cancellationToken = default)
        {
            var q = new PEMS.Application.News.Queries.GetEligibleVisitInstancesForNews
                .GetEligibleVisitInstancesForNewsQuery
            {
                IncludeAlreadyHasNews = includeAlreadyHasNews
            };
            var result = await _mediator.Send(q, cancellationToken);
            return Ok(result);
        }

        // UC-Create News: POST /api/news
        [HttpPost]
        [RoleAuthorize(EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> CreateNews(
            [FromBody] PEMS.Application.News.Commands.CreateNews.CreateNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.Success) return Conflict(result);
            return StatusCode(201, result);
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

    public sealed record CreateVisitInstanceNewsBody(string Title, string? Summary, string? Body);
    public sealed record UpdateVisitInstanceNewsBody(string Title, string? Summary, string? Body, int RowVersion);
}
