using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Content for the public website, plus the signed-in user's notification tray.
    ///
    /// This is a MIXED surface, which is why [AllowAnonymous] is declared per action and NOT on
    /// the class: [AllowAnonymous] anywhere in an endpoint's metadata short-circuits authorization
    /// entirely, so a class-level one would silently void the [Authorize] on the three
    /// notifications actions below and publish one user's notifications to anonymous callers.
    ///
    /// Every action here must therefore carry its own decision. The global fallback policy
    /// (AddAppAuthorization) authenticates anything that carries neither, which is how homepage,
    /// search, contact, policy and gallery — all genuinely public — started returning 401 to
    /// guests; the header search box answered that with a bogus "session expired" toast.
    /// PEMS.ArchitectureTests.AuthorizationTests asserts both halves of this rule.
    /// </summary>
    [ApiController]
    [Route("api/public")]
    public sealed class PublicContentController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicContentController(IMediator mediator) => _mediator = mediator;

        [HttpGet("homepage")]
        [AllowAnonymous]
        public async Task<IActionResult> ViewHomepage(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewHomepage.ViewHomepageQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchInformation(
            [FromQuery] PEMS.Application.PublicContent.Queries.SearchInformation.SearchInformationQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("contact")]
        [AllowAnonymous]
        public async Task<IActionResult> ViewContactInfo(
            [FromQuery] PEMS.Application.PublicContent.Queries.ViewContactInfo.ViewContactInfoQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("policy")]
        [AllowAnonymous]
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
        [AllowAnonymous]
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
