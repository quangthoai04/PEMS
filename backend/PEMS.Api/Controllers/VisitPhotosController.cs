using MediatR;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.VisitPhotos.Commands.RemoveVisitPhoto;
using PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;
using PEMS.Application.Delegations.VisitPhotos.Queries.GetMyVisitPhotoFolders;
using PEMS.Application.Delegations.VisitPhotos.Queries.GetVisitInstancePhotos;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Student visit-photo storage (visit_photo_folders / visit_photos — independent from Gallery).
    /// Authorization lives in the handlers: every call re-checks the ACTIVE-Student +
    /// ACCEPTED-participant scope against the DB (anti-IDOR); the controller only maps transport.
    /// </summary>
    [ApiController]
    [Route("api/visit-photos")]
    public class VisitPhotosController : ControllerBase
    {
        private readonly IMediator _mediator;
        public VisitPhotosController(IMediator mediator) => _mediator = mediator;

        // Tab "Quản lý ảnh đoàn khách": one row per accepted campus instance of the Student.
        [HttpGet("folders")]
        public async Task<IActionResult> GetMyFolders(
            [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null,
            [FromQuery] string? sortDirection = "DESC", [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _mediator.Send(
                new GetMyVisitPhotoFoldersQuery 
                { 
                    Page = page, 
                    PageSize = pageSize, 
                    Search = search,
                    SortDirection = sortDirection,
                    FromDate = fromDate,
                    ToDate = toDate
                },
                cancellationToken);
            return Ok(result);
        }

        // Photo list of one instance (contribution page block + "Xem chi tiết"/"Chỉnh sửa" modal).
        [HttpGet("instances/{visitInstanceId}")]
        public async Task<IActionResult> GetInstancePhotos(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstancePhotosQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // Multi-image upload; server-side policy re-validates MIME + magic bytes + size per file.
        [HttpPost("instances/{visitInstanceId}/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(60 * 1024 * 1024)] // 10 images × 5 MB + form overhead
        public async Task<IActionResult> UploadPhotos(
            ulong visitInstanceId, List<IFormFile> files, CancellationToken cancellationToken)
        {
            var command = new UploadVisitInstancePhotosCommand { VisitInstanceId = visitInstanceId };
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Length == 0) continue;
                    using var ms = new System.IO.MemoryStream();
                    await file.CopyToAsync(ms, cancellationToken);
                    command.Files.Add(new UploadVisitInstancePhotoFile(ms.ToArray(), file.FileName, file.ContentType));
                }
            }

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        public sealed class RemoveVisitPhotoBody
        {
            public string Reason { get; set; } = string.Empty;
        }

        // Soft delete (REMOVED + removed_at/by/reason) — own photos only; binary/files row are kept.
        [HttpPatch("{visitPhotoId}/remove")]
        public async Task<IActionResult> RemovePhoto(
            ulong visitPhotoId, [FromBody] RemoveVisitPhotoBody body, CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new RemoveVisitPhotoCommand { VisitPhotoId = visitPhotoId, Reason = body?.Reason ?? string.Empty },
                cancellationToken);
            return NoContent();
        }
    }
}
