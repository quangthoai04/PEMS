using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/public")]
    public sealed class PublicContentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicContentController(IMediator mediator) => _mediator = mediator;

        [HttpGet("homepage")]
        public async Task<IActionResult> ViewHomepage(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewHomepage.ViewHomepageQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchInformation(
            [FromQuery] PEMS.Application.PublicContent.Queries.SearchInformation.SearchInformationQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("contact")]
        public async Task<IActionResult> ViewContactInfo(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewContactInfo.ViewContactInfoQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("policy")]
        public async Task<IActionResult> ViewPolicyAndTerms(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewPolicyAndTerms.ViewPolicyAndTermsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("faqs")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFaqs(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewFAQ.ViewFaqQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>Every faq_type with its PUBLISHED question count — for the FAQ page's topic cards.</summary>
        [HttpGet("faqs/type-counts")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFaqTypeCounts(
            [FromQuery] string? languageCode,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.PublicContent.Queries.GetFaqTypeCounts.GetFaqTypeCountsQuery
                {
                    LanguageCode = languageCode,
                },
                cancellationToken);
            return Ok(result);
        }

        [HttpGet("news")]
        [AllowAnonymous]
        public async Task<IActionResult> ViewNews(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewNews.ViewNewsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("news/{newsId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicNewsDetail(
            ulong newsId,
            [FromQuery] string? languageCode,
            CancellationToken cancellationToken)
        {
            var query = new PEMS.Application.PublicContent.Queries.ViewPublicNewsDetail.ViewPublicNewsDetailQuery(newsId, languageCode);
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Anonymous image/file stream for PUBLISHED news content (cover + section images).
        /// Files not referenced by a published post return 404.
        /// </summary>
        [HttpGet("news-files/{fileId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicNewsFile(ulong fileId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.PublicContent.Queries.GetPublicNewsFile.GetPublicNewsFileQuery(fileId),
                cancellationToken);
            Response.Headers.CacheControl = "public, max-age=3600";
            return File(result.Content, result.ContentType);
        }

        // NOTE: GET /api/public/partners moved to PublicPartnersController (Partner module,
        // docs/PARTNER_canh/01 §5.6) — only APPROVED + PUBLIC profiles are returned there.

        [HttpGet("gallery")]
        public async Task<IActionResult> ViewGallery(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewGallery.ViewGalleryQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("notifications")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> ViewNotifications(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewNotifications.ViewNotificationsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPatch("notifications/{notificationId}/read")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MarkNotificationRead(ulong notificationId, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new PEMS.Application.PublicContent.Commands.MarkNotificationsRead.MarkNotificationsReadCommand(notificationId),
                cancellationToken);
            return NoContent();
        }

        [HttpPatch("notifications/read-all")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new PEMS.Application.PublicContent.Commands.MarkNotificationsRead.MarkNotificationsReadCommand(null),
                cancellationToken);
            return NoContent();
        }
    }
}
