using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Galleries.Public.Queries.GetPublicCampuses;
using PEMS.Application.Galleries.Public.Queries.GetPublicCampusNavigation;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemAudio;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemDetail;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMediaStream;
using PEMS.Application.Galleries.Public.Queries.GetPublicLocationGalleryItem;
using PEMS.Application.Galleries.Public.Queries.GetPublicLocationShowcase;

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

        // UC §4 — album grid of one location (every public item by its primary media).
        [HttpGet("locations/{locationId:long}/gallery-items")]
        public async Task<IActionResult> GetLocationGalleryItems(long locationId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPublicLocationGalleryItemsQuery(locationId), cancellationToken));

        // Location Showcase — the location's MEDIA items (right column) + VISIT_DELEGATION items (row).
        [HttpGet("locations/{locationId:long}/showcase")]
        public async Task<IActionResult> GetLocationShowcase(long locationId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetPublicLocationShowcaseQuery(locationId), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        // UC §5 — detail of one gallery item (all its media).
        [HttpGet("gallery-items/{galleryItemId:long}")]
        public async Task<IActionResult> GetGalleryItemDetail(long galleryItemId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetPublicGalleryItemDetailQuery(galleryItemId), cancellationToken));

        // Bilingual narration audio behind the public speaker icon. The file is resolved from the item id
        // + language (vi/en) server-side — never a client fileId — and only served when the item is
        // public-visible. Streams the bytes (range-aware) exactly like the media proxy.
        [HttpGet("gallery-items/{galleryItemId:long}/audio/{languageCode}")]
        public async Task<IActionResult> GetGalleryItemAudio(
            long galleryItemId, string languageCode, CancellationToken cancellationToken)
        {
            long? rangeFrom = null, rangeTo = null;
            var typedRange = Request.GetTypedHeaders().Range;
            if (typedRange is { Ranges.Count: 1 })
            {
                var r = typedRange.Ranges.First();
                rangeFrom = r.From;
                rangeTo = r.To;
            }

            var result = await _mediator.Send(
                new GetPublicGalleryItemAudioQuery(galleryItemId, languageCode, rangeFrom, rangeTo), cancellationToken);

            await using (result)
            {
                Response.Headers.CacheControl = "public, max-age=3600";
                if (result.SupportsRange)
                    Response.Headers.AcceptRanges = "bytes";
                Response.ContentType = result.ContentType;

                if (result.IsPartial)
                {
                    Response.StatusCode = StatusCodes.Status206PartialContent;
                    if (result.TotalLength is { } total)
                        Response.Headers.ContentRange = $"bytes {result.RangeStart}-{result.RangeEnd}/{total}";
                }

                if (result.ContentLength is { } len)
                    Response.ContentLength = len;

                await result.Stream.CopyToAsync(Response.Body, cancellationToken);
            }

            return new EmptyResult();
        }

        // Gallery-scoped public file proxy (images / audio / area cover video). Streams the bytes and
        // honours an HTTP Range request (206 Partial Content) so an area cover MP4 can seek without the
        // server buffering the whole file (UC §13). Authorization is enforced in the handler.
        [HttpGet("media/{fileId:long}/content")]
        public async Task<IActionResult> GetMediaContent(long fileId, CancellationToken cancellationToken)
        {
            // Parse a single byte range from the request, if any (multi-range is not supported → treated as full).
            long? rangeFrom = null, rangeTo = null;
            var typedRange = Request.GetTypedHeaders().Range;
            if (typedRange is { Ranges.Count: 1 })
            {
                var r = typedRange.Ranges.First();
                rangeFrom = r.From;
                rangeTo = r.To;
            }

            var result = await _mediator.Send(
                new GetPublicGalleryMediaStreamQuery((ulong)fileId, rangeFrom, rangeTo), cancellationToken);

            await using (result)
            {
                Response.Headers.CacheControl = "public, max-age=3600";
                if (result.SupportsRange)
                    Response.Headers.AcceptRanges = "bytes";
                Response.ContentType = result.ContentType;

                if (result.IsPartial)
                {
                    Response.StatusCode = StatusCodes.Status206PartialContent;
                    if (result.TotalLength is { } total)
                        Response.Headers.ContentRange = $"bytes {result.RangeStart}-{result.RangeEnd}/{total}";
                }

                if (result.ContentLength is { } len)
                    Response.ContentLength = len;

                await result.Stream.CopyToAsync(Response.Body, cancellationToken);
            }

            return new EmptyResult();
        }
    }
}
