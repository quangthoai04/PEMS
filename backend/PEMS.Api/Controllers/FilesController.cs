using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Common.Files;
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
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string? purpose, CancellationToken cancellationToken)
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

            // A download is not a rendering context, so the stored type is declared as-is — but the
            // browser must not be free to sniff a different one out of the bytes and act on that.
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            // The filename was already reduced to a safe leaf by the handler; re-applying it here keeps
            // the guarantee local to the line that writes the header, where it is checkable.
            return File(result.Content, result.ContentType, FileResponseSafety.SafeFileName(result.FileName));
        }

        /// <summary>
        /// Inline content stream for a stored file — the preview path, and <c>users.avatar_url</c>
        /// (<c>/api/files/{id}/content</c>). Same bytes and the SAME authorization as <c>download</c>:
        /// both routes go through <c>GetFileContentQuery</c>, so preview and download can never end up
        /// enforcing two different policies.
        ///
        /// <para>
        /// What differs is only how the bytes are declared. Rendering happens here, so a type a browser
        /// would execute as a document (HTML, SVG, XSL) is reported as
        /// <c>application/octet-stream</c> instead — the file stays downloadable, it just stops being a
        /// page this origin will run.
        /// </para>
        /// </summary>
        [HttpGet("{id}/content")]
        public async Task<IActionResult> Content(ulong id, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetFileContentQuery(id), cancellationToken);
            Response.Headers.CacheControl = "private, max-age=3600";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(result.Content, FileResponseSafety.SafeInlineContentType(result.ContentType));
        }
    }
}
