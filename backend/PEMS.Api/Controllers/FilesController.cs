using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Files.Commands.UploadFile;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Generic file upload/download backing email attachments + inline images (and reusable for other
    /// uploads). Upload registers a row in <c>files</c> and returns its id + URLs; download streams the
    /// bytes by file_id. Authenticated users only (handlers re-check the current user).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FilesController(IMediator mediator) => _mediator = mediator;

        /// <summary>Upload one file (multipart). Returns file_id + URLs for attaching to an email/draft.</summary>
        [HttpPost("upload")]
        [RequestSizeLimit(30 * 1024 * 1024)] // a little above the 25 MB per-file business limit
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? purpose, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Tệp tải lên rỗng hoặc không hợp lệ." });

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(new UploadFileCommand
            {
                Content = ms.ToArray(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Purpose = purpose,
            }, cancellationToken);

            return Ok(result);
        }

        /// <summary>Stream a stored file's bytes (download link + inline-image blob fetch).</summary>
        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download(ulong id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetFileContentQuery(id), cancellationToken);
            return File(result.Content, result.ContentType, result.FileName);
        }

        /// <summary>
        /// Inline content stream for a stored file (used as <c>users.avatar_url</c> =
        /// <c>/api/files/{id}/content</c>). Same bytes as <c>download</c> but rendered inline (no
        /// attachment filename) with a short private cache so avatars don't refetch on every render.
        /// </summary>
        [HttpGet("{id}/content")]
        public async Task<IActionResult> Content(ulong id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetFileContentQuery(id), cancellationToken);
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(result.Content, result.ContentType);
        }
    }
}
