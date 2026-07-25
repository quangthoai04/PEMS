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

        // UC Upload News Cover Image: POST /api/news/cover-upload
        // visitInstanceId (optional form field): when the post being composed is attached to a
        // đoàn, the image is routed into that đoàn's own Drive folder instead of the flat News
        // folder — same folder the Student visit-photo feature already uses for that instance.
        [HttpPost("cover-upload")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadNewsCoverImage(
            IFormFile file,
            [FromForm] ulong? visitInstanceId,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Tệp tải lên rỗng hoặc không hợp lệ." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(
                new PEMS.Application.News.Commands.UploadNewsCoverImage.UploadNewsCoverImageCommand(
                    ms.ToArray(), file.FileName, file.ContentType, visitInstanceId),
                cancellationToken);

            return Ok(result);
        }

        // Upload one section (inline) image: POST /api/news/section-file-upload
        // Same pipeline as cover-upload (Google Drive + files metadata); returns fileId
        // to reference from contentSections[].sectionFiles.
        [HttpPost("section-file-upload")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        [RequestSizeLimit(6 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadNewsSectionImage(
            IFormFile file,
            [FromForm] ulong? visitInstanceId,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Tệp tải lên rỗng hoặc không hợp lệ." });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(
                new PEMS.Application.News.Commands.UploadNewsCoverImage.UploadNewsCoverImageCommand(
                    ms.ToArray(), file.FileName, file.ContentType, visitInstanceId),
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

        // Create News support: get eligible closed visit instances
        [HttpGet("eligible-visit-instances")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
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
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> CreateNews(
            [FromBody] PEMS.Application.News.Commands.CreateNews.CreateNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.Success) return Conflict(result);
            return StatusCode(201, result);
        }

        // UC View News Details: GET /api/news/{newsId}?languageCode=en
        [HttpGet("{newsId}")]
        [RoleAuthorize(EffectiveRole.Ho, EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> GetNewsDetails(
            ulong newsId,
            [FromQuery] string? languageCode,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.News.Queries.ViewNewsDetails.ViewNewsDetailsQuery(newsId, languageCode),
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
                RowVersion = body.RowVersion,
                IsFeatured = body.IsFeatured
            };
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // UC Change Visibility: PATCH /api/news/{newsId}/visibility (hide or show)
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

        // Standalone featured toggle for an already-reviewed post (Published/Hidden/Rejected):
        // PATCH /api/news/{newsId}/featured — "Bỏ nổi bật" / "Đánh dấu nổi bật" on the detail page.
        [HttpPatch("{newsId}/featured")]
        [RoleAuthorize(EffectiveRole.StaffLeader)]
        public async Task<IActionResult> SetNewsFeatured(ulong newsId, [FromBody] SetFeaturedBody body, CancellationToken cancellationToken)
        {
            var command = new PEMS.Application.News.Commands.SetNewsFeatured.SetNewsFeaturedCommand
            {
                NewsId     = newsId,
                IsFeatured = body.IsFeatured,
                RowVersion = body.RowVersion
            };
            var result = await _mediator.Send(command, cancellationToken);
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

        // [HttpGet("viewnewsdetails")] removed — replaced by GET /api/news/{newsId}

        // UC Add Multilingual News: create a manual translation (or persist an auto-translated draft)
        [HttpPost("addmultilingualnews")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> AddMultilingualNews(
            [FromBody] PEMS.Application.News.Commands.AddMultilingualNews.AddMultilingualNewsCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // UC Auto Translate News: POST /api/news/{newsId}/translations/auto-translate
        // save=false → preview only; save=true → persist as a new translation.
        [HttpPost("{newsId}/translations/auto-translate")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> AutoTranslateNews(
            ulong newsId,
            [FromBody] AutoTranslateNewsBody body,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.News.Commands.TranslateNews.TranslateNewsCommand
            {
                NewsId         = newsId,
                SourceLanguage = body.SourceLanguage ?? "vi",
                TargetLanguage = body.TargetLanguage,
                Save           = body.Save
            }, cancellationToken);
            return Ok(result);
        }

        // UC Translate News Draft: POST /api/news/translate-draft — same translation preview as
        // auto-translate above, but for a post being composed (no NewsId yet). Never persists.
        [HttpPost("translate-draft")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
        public async Task<IActionResult> TranslateNewsDraft(
            [FromBody] PEMS.Application.News.Commands.TranslateNewsDraft.TranslateNewsDraftCommand command,
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

        // UC Edit News: PUT /api/news/{newsId}
        [HttpPut("{newsId}")]
        [RoleAuthorize(EffectiveRole.StaffLeader, EffectiveRole.Staff, EffectiveRole.Student)]
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
                LanguageCode    = string.IsNullOrWhiteSpace(body.LanguageCode) ? "vi" : body.LanguageCode!,
                ContentSections = body.ContentSections
                    ?? Array.Empty<PEMS.Application.News.Commands.EditNews.EditNewsContentSectionDto>()
            };
            var result = await _mediator.Send(command, cancellationToken);
            if (!result.Success) return Conflict(result);
            return Ok(result);
        }
    }

    public sealed record CreateVisitInstanceNewsBody(string Title, string? Summary, string? Body);
    public sealed record UpdateVisitInstanceNewsBody(string Title, string? Summary, string? Body, int RowVersion);
    public sealed record ReviewNewsBody(string Action, string? Reason, int RowVersion, bool? IsFeatured = null);
    public sealed record AutoTranslateNewsBody(string? SourceLanguage, string TargetLanguage, bool Save);
    public sealed record ChangeVisibilityBody(string TargetStatus, int RowVersion);
    public sealed record SetFeaturedBody(bool IsFeatured, int RowVersion);
    public sealed record EditNewsBody(
        int    RowVersion,
        ulong? CoverFileId,
        string? Title,
        string? Summary,
        string? LanguageCode,
        IReadOnlyList<PEMS.Application.News.Commands.EditNews.EditNewsContentSectionDto>? ContentSections);
}
