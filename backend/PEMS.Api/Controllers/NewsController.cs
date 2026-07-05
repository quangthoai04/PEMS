using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        // Get news list for a specific visit instance (used by visit management page)
        [HttpGet("visit-instances/{visitInstanceId}")]
        public async Task<IActionResult> GetVisitInstanceNews(ulong visitInstanceId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetVisitInstanceNewsQuery(visitInstanceId), cancellationToken));

        // UC-Create News: POST /api/news/visit-instances/{visitInstanceId}
        // visitInstanceId comes from URL — set by visit management page when navigating to create form
        [HttpPost("visit-instances/{visitInstanceId}")]
        [RoleAuthorize(EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> CreateNews(
            [FromRoute] ulong visitInstanceId,
            [FromBody] CreateNewsBody body,
            CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.News.Commands.CreateNews.CreateNewsCommand
            {
                VisitInstanceId = visitInstanceId,
                CoverFileId     = body.CoverFileId,
                Title           = body.Title ?? string.Empty,
                Summary         = body.Summary ?? string.Empty,
                ContentSections = body.ContentSections
                    ?? Array.Empty<PEMS.Application.News.Commands.CreateNews.CreateNewsContentSectionDto>()
            };
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.Success) return Conflict(result);
            return StatusCode(201, result);
        }

        // UC Upload News Cover Image
        [HttpPost("cover-upload")]
        [RoleAuthorize(EffectiveRole.Staff, EffectiveRole.Student)]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadNewsCoverImage(
            [FromForm] IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Tệp tải lên rỗng hoặc không hợp lệ." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(
                new PEMS.Application.News.Commands.UploadNewsCoverImage.UploadNewsCoverImageCommand(
                    ms.ToArray(), file.FileName, file.ContentType),
                cancellationToken);

            return Ok(result);
        }

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

        // UC View News Details
        [HttpGet("{newsId}")]
        [RoleAuthorize(EffectiveRole.Ho, EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> GetNewsDetails(ulong newsId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.News.Queries.ViewNewsDetails.ViewNewsDetailsQuery(newsId),
                cancellationToken);
            return Ok(result);
        }

        // UC Review News: PATCH /api/news/{newsId}/review (approve or reject)
        [HttpPatch("{newsId}/review")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> ReviewNews(ulong newsId, [FromBody] ReviewNewsBody body, CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.News.Commands.ApproveNews.ApproveNewsCommand
            {
                NewsId     = newsId,
                Action     = body.Action,
                Reason     = body.Reason,
                RowVersion = body.RowVersion
            };
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // UC Change Visibility: PATCH /api/news/{newsId}/visibility
        [HttpPatch("{newsId}/visibility")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> ChangeNewsVisibility(ulong newsId, [FromBody] ChangeVisibilityBody body, CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.News.Commands.ManageNewsVisibility.ManageNewsVisibilityCommand
            {
                NewsId       = newsId,
                TargetStatus = body.TargetStatus,
                RowVersion   = body.RowVersion
            };
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // UC Edit News: PUT /api/news/{newsId}
        [HttpPut("{newsId}")]
        [RoleAuthorize(EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> EditNews(
            ulong newsId,
            [FromBody] EditNewsBody body,
            CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.News.Commands.EditNews.EditNewsCommand
            {
                NewsId          = newsId,
                RowVersion      = body.RowVersion,
                CoverFileId     = body.CoverFileId,
                Title           = body.Title ?? string.Empty,
                Summary         = body.Summary,
                ContentSections = body.ContentSections
                    ?? Array.Empty<PEMS.Application.News.Commands.EditNews.EditNewsContentSectionDto>()
            };
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.Success) return Conflict(result);
            return Ok(result);
        }
    }

    public sealed record CreateNewsBody(
        ulong? CoverFileId,
        string? Title,
        string? Summary,
        IReadOnlyList<PEMS.Application.News.Commands.CreateNews.CreateNewsContentSectionDto>? ContentSections);
    public sealed record ReviewNewsBody(string Action, string? Reason, int RowVersion);
    public sealed record ChangeVisibilityBody(string TargetStatus, int RowVersion);
    public sealed record EditNewsBody(
        int    RowVersion,
        ulong? CoverFileId,
        string? Title,
        string? Summary,
        IReadOnlyList<PEMS.Application.News.Commands.EditNews.EditNewsContentSectionDto>? ContentSections);
}
