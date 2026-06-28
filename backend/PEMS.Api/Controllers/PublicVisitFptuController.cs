using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Galleries.Public.Queries.GetPublicCampuses;
using PEMS.Application.Galleries.Public.Queries.GetPublicCampusNavigation;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMedia;
using PEMS.Application.Galleries.Public.Queries.GetPublicLocationGalleryItem;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Public VisitFPTU Gallery (UC §6/§7). Anonymous read-only display layer over the gallery managed by
    /// Staff Leaders — campus picker, area/location navigation, location detail, and a gallery-scoped media
    /// proxy. No login required (BR-PGAL-02); every query only returns effective public-visible data and no
    /// admin/audit fields. The media route is the public, scoped alternative to the authenticated
    /// <c>/api/files/{id}/content</c> so the page can render images/videos without a token (BR-PGAL-13/14).
    /// </summary>
    [ApiController]
    [Route("api/public/visit-fptu")]
    [AllowAnonymous]
    public sealed class PublicVisitFptuController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicVisitFptuController(IMediator mediator) => _mediator = mediator;

        // UC §7.1 — ACTIVE campus list for the picker.
        [HttpGet("campuses")]
        public async Task<IActionResult> GetCampuses(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPublicCampusesQuery(), cancellationToken));

        // UC §7.2 — area/location navigation tree of a campus (by code).
        [HttpGet("campuses/{campusCode}/navigation")]
        public async Task<IActionResult> GetNavigation(string campusCode, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPublicCampusNavigationQuery(campusCode), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        // UC §7.3 — the public gallery item of one location.
        [HttpGet("locations/{locationId:long}/gallery-item")]
        public async Task<IActionResult> GetLocationGalleryItem(long locationId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPublicLocationGalleryItemQuery(locationId), cancellationToken));

        // Gallery-scoped public file proxy (images/videos) — inline, short private cache.
        [HttpGet("media/{fileId:long}/content")]
        public async Task<IActionResult> GetMediaContent(long fileId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPublicGalleryMediaQuery((ulong)fileId), cancellationToken);
            Response.Headers.CacheControl = "public, max-age=3600";
            return File(result.Content, result.ContentType);
        }
    }
}
