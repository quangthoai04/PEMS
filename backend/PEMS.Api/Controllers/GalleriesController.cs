using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Galleries.Commands.AddGalleryItem;
using PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus;
using PEMS.Application.Galleries.Commands.ChangeGalleryLocationStatus;
using PEMS.Application.Galleries.Commands.CreateGalleryLocation;
using PEMS.Application.Galleries.Commands.UpdateGalleryItem;
using PEMS.Application.Galleries.Commands.UpdateGalleryLocation;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Queries.GetGalleryFilterOptions;
using PEMS.Application.Galleries.Queries.SearchGalleryItems;
using PEMS.Application.Galleries.Queries.ViewGalleryItemDetails;
using PEMS.Application.Galleries.Queries.ViewGalleryItemList;
using PEMS.Application.Galleries.Queries.ViewGalleryLocationList;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// VisitFPTU Gallery management (Staff Leader, sub_role LEADER). Verb-route convention to match the
    /// rest of the project. Every handler self-guards via <c>StaffLeaderGalleryScope</c> (campus is taken
    /// from the JWT, never the request), so anonymous/other roles get 403. Add/Edit are multipart so the
    /// shared Google Drive upload foundation can store the media files.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GalleriesController : ControllerBase
    {
        // Headroom for up to 20 media files (mix of images ≤5MB and videos ≤100MB) in one request.
        private const long GalleryUploadByteLimit = 1024L * 1024 * 1024; // 1 GB

        private readonly IMediator _mediator;
        public GalleriesController(IMediator mediator) => _mediator = mediator;

        // UC-GAL-01 View List.
        [HttpGet("viewgalleryitemlist")]
        public async Task<IActionResult> ViewGalleryItemList([FromQuery] ViewGalleryItemListQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // UC-GAL-02 Search / Filter.
        [HttpGet("searchgalleryitems")]
        public async Task<IActionResult> SearchGalleryItems([FromQuery] SearchGalleryItemsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // UC-GAL-03 View Detail.
        [HttpGet("viewgalleryitemdetails")]
        public async Task<IActionResult> ViewGalleryItemDetails([FromQuery] ViewGalleryItemDetailsQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // Reference data for the area / location dropdowns (filters + upload picker).
        [HttpGet("galleryfilteroptions")]
        public async Task<IActionResult> GetGalleryFilterOptions(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetGalleryFilterOptionsQuery(), cancellationToken));

        // UC-GAL-04 Add / Upload Media (multipart).
        [HttpPost("addgalleryitem")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(GalleryUploadByteLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = GalleryUploadByteLimit)]
        public async Task<IActionResult> AddGalleryItem(
            [FromForm] string title,
            [FromForm] string descriptionVi,
            [FromForm] string descriptionEn,
            [FromForm] long locationId,
            [FromForm] string? itemType,
            [FromForm] string? status,
            [FromForm] List<string>? youtubeUrls,
            [FromForm] string? primaryMediaKey,
            IFormFile? audioVi,
            IFormFile? audioEn,
            List<IFormFile>? files,
            CancellationToken cancellationToken)
        {
            var buffered = await BufferFilesAsync(files, cancellationToken);
            var command = new AddGalleryItemCommand(
                title,
                descriptionVi,
                await BufferOneAsync(audioVi, cancellationToken),
                descriptionEn,
                await BufferOneAsync(audioEn, cancellationToken),
                locationId, itemType, status, buffered,
                youtubeUrls ?? new List<string>(), primaryMediaKey);
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // UC-GAL-07 Edit (multipart).
        [HttpPost("updategalleryitem")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(GalleryUploadByteLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = GalleryUploadByteLimit)]
        public async Task<IActionResult> UpdateGalleryItem(
            [FromForm] long galleryItemId,
            [FromForm] string title,
            [FromForm] string descriptionVi,
            [FromForm] string descriptionEn,
            [FromForm] long locationId,
            [FromForm] string? itemType,
            [FromForm] List<long>? keepMediaIds,
            [FromForm] long? primaryMediaId,
            [FromForm] List<string>? youtubeUrls,
            [FromForm] string? primaryMediaKey,
            IFormFile? newAudioVi,
            IFormFile? newAudioEn,
            List<IFormFile>? newFiles,
            CancellationToken cancellationToken)
        {
            var buffered = await BufferFilesAsync(newFiles, cancellationToken);
            var command = new UpdateGalleryItemCommand(
                galleryItemId,
                title,
                descriptionVi,
                descriptionEn,
                await BufferOneAsync(newAudioVi, cancellationToken),
                await BufferOneAsync(newAudioEn, cancellationToken),
                locationId,
                itemType,
                keepMediaIds ?? new List<long>(),
                buffered,
                primaryMediaId,
                youtubeUrls ?? new List<string>(),
                primaryMediaKey);
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // UC-GAL-05 Enable / UC-GAL-06 Disable.
        [HttpPost("changegalleryitemstatus")]
        public async Task<IActionResult> ChangeGalleryItemStatus([FromBody] ChangeGalleryItemStatusCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        // ── Quản lý khu vực (area/location management, UC-LOC-01..09) ──

        // UC-LOC-01 List / UC-LOC-02 Search / UC-LOC-03 Filter.
        [HttpGet("viewgallerylocationlist")]
        public async Task<IActionResult> ViewGalleryLocationList([FromQuery] ViewGalleryLocationListQuery query, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(query, cancellationToken));

        // UC-LOC-04 Add location into existing area / UC-LOC-05 New area + first location (multipart —
        // carries the mandatory area cover VIDEO (new area) and the mandatory location cover image).
        [HttpPost("creategallerylocation")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(GalleryUploadByteLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = GalleryUploadByteLimit)]
        public async Task<IActionResult> CreateGalleryLocation(
            [FromForm] string mode,
            [FromForm] long? areaId,
            [FromForm] string? newAreaName,
            [FromForm] string locationName,
            IFormFile? areaCoverVideo,
            IFormFile? locationCoverImage,
            CancellationToken cancellationToken)
        {
            var command = new CreateGalleryLocationCommand(
                mode,
                areaId,
                newAreaName,
                locationName,
                await BufferOneAsync(areaCoverVideo, cancellationToken),
                await BufferOneAsync(locationCoverImage, cancellationToken));
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // UC-LOC-06 Edit (existing area) / UC-LOC-07 Edit (new area). Multipart — the location cover is
        // optional here (kept when omitted); a new area still requires its own cover VIDEO. On an existing
        // area a supplied area cover video replaces that area's cover (image → video allowed).
        [HttpPost("updategallerylocation")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(GalleryUploadByteLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = GalleryUploadByteLimit)]
        public async Task<IActionResult> UpdateGalleryLocation(
            [FromForm] long locationId,
            [FromForm] string mode,
            [FromForm] long? areaId,
            [FromForm] string? newAreaName,
            [FromForm] string locationName,
            IFormFile? areaCoverVideo,
            IFormFile? locationCoverImage,
            CancellationToken cancellationToken)
        {
            var command = new UpdateGalleryLocationCommand(
                locationId,
                mode,
                areaId,
                newAreaName,
                locationName,
                await BufferOneAsync(areaCoverVideo, cancellationToken),
                await BufferOneAsync(locationCoverImage, cancellationToken));
            return Ok(await _mediator.Send(command, cancellationToken));
        }

        // UC-LOC-08 Enable / UC-LOC-09 Disable.
        [HttpPost("changegallerylocationstatus")]
        public async Task<IActionResult> ChangeGalleryLocationStatus([FromBody] ChangeGalleryLocationStatusCommand command, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(command, cancellationToken));

        /// <summary>Buffers each uploaded file's bytes so the handler can sniff its type and hand it to
        /// the shared upload service in one pass.</summary>
        private static async Task<IReadOnlyList<GalleryUploadFileCommandDto>> BufferFilesAsync(
            List<IFormFile>? files, CancellationToken cancellationToken)
        {
            if (files is null || files.Count == 0)
                return new List<GalleryUploadFileCommandDto>();

            var result = new List<GalleryUploadFileCommandDto>(files.Count);
            foreach (var file in files.Where(f => f is { Length: > 0 }))
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                result.Add(new GalleryUploadFileCommandDto(
                    ms.ToArray(), file.FileName, file.ContentType, file.Length, null, null));
            }
            return result;
        }

        /// <summary>Buffers a single optional cover image (bytes + metadata), or null when none was sent.</summary>
        private static async Task<GalleryUploadFileCommandDto?> BufferOneAsync(
            IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is not { Length: > 0 })
                return null;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            return new GalleryUploadFileCommandDto(
                ms.ToArray(), file.FileName, file.ContentType, file.Length, null, null);
        }
    }
}
