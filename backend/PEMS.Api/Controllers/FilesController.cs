using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public FilesController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Streams a stored file (e.g. an avatar) from the storage provider through the backend.
        /// Referenced by <c>users.avatar_url = /api/files/{fileId}/content</c>.
        /// </summary>
        [HttpGet("{fileId:long}/content")]
        [Authorize]
        public async Task<IActionResult> GetContent(long fileId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetFileContentQuery(fileId), cancellationToken);

            // Avatars are safe to cache privately per user; revalidation happens on a new fileId.
            Response.Headers.CacheControl = "private, max-age=3600";
            return File(result.Content, result.ContentType);
        }
    }
}
