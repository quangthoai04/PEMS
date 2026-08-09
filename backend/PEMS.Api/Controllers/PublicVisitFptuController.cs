using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PEMS.Api.PublicGallery;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Galleries.Public.Common;
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
        private readonly IConfiguration _configuration;

        public PublicVisitFptuController(IMediator mediator, IConfiguration configuration)
        {
            _mediator = mediator;
            _configuration = configuration;
        }

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

        /// <summary>
        /// Server-rendered Open Graph preview of one gallery item, for social crawlers only. The URL people
        /// actually share stays the SPA deep link
        /// <c>/visit-fptu/{campusCode}?locationId=..&amp;itemId=..</c>; Vercel rewrites it here (same URL, no
        /// redirect) when the User-Agent is facebookexternalhit/Facebot, so Facebook can read a real
        /// title/description/image instead of the empty Vite shell.
        ///
        /// Visibility is NOT re-implemented: it reuses <see cref="GetPublicGalleryItemDetailQuery"/>, so a
        /// hidden/deleted item — or one under an inactive location/area/campus — 404s here exactly as it does
        /// for the page. The campus code and location id from the URL must also match that item, otherwise a
        /// hand-edited link could show one item's card and open another's page (cloaking).
        ///
        /// The ids arrive as strings on purpose: the Vercel rewrite carries them in its destination while the
        /// crawler's original query is passed through too, so each may appear twice ("21,21" once bound).
        /// Strict <c>long</c> binding would answer 400 to a perfectly good crawl; <see cref="TryParseId"/>
        /// accepts the repeat and anything genuinely malformed simply has no preview (404).
        /// </summary>
        [HttpGet("share-preview/{campusCode}")]
        public async Task<IActionResult> GetSharePreview(
            string campusCode,
            [FromQuery] string? locationId,
            [FromQuery] string? itemId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(campusCode) ||
                !TryParseId(locationId, out var locationIdValue) ||
                !TryParseId(itemId, out var itemIdValue))
            {
                return NotFound();
            }

            PublicGalleryItemDetailDto detail;
            try
            {
                detail = await _mediator.Send(new GetPublicGalleryItemDetailQuery(itemIdValue), cancellationToken);
            }
            catch (NotFoundException)
            {
                // Not public-visible → no card at all. Never a fabricated preview from stale data.
                return NotFound();
            }

            if (!string.Equals(detail.Campus.CampusCode, campusCode, StringComparison.OrdinalIgnoreCase) ||
                detail.Location.LocationId != (ulong)locationIdValue)
            {
                return NotFound();
            }

            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]
                ?? throw new InvalidOperationException("App:FrontendBaseUrl is required to build share previews.");

            var metadata = PublicGallerySharePreviewBuilder.BuildMetadata(
                detail, frontendBaseUrl, campusCode, locationIdValue, itemIdValue);

            // The item's title/description/primary image can change (or be hidden) at any moment, so this
            // response is never cached at the edge. Facebook keeps its own scraper cache regardless.
            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return Content(PublicGallerySharePreviewBuilder.RenderHtml(metadata), "text/html", Encoding.UTF8);
        }

        /// <summary>
        /// Reads a positive id out of a query value that may legitimately arrive repeated ("21,21" once the
        /// duplicate keys are joined by model binding). Every segment must be the same id — two DIFFERENT
        /// values mean a tampered URL, not an edge rewrite, and get no preview.
        /// </summary>
        private static bool TryParseId(string? raw, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!long.TryParse(part.Trim(), out var parsed) || parsed <= 0) return false;
                if (value == 0) value = parsed;
                else if (value != parsed) return false;
            }

            return value > 0;
        }

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
