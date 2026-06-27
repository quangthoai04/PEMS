using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Profiles.Commands.ChangePassword;
using PEMS.Application.Profiles.Commands.UploadProfileAvatar;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfilesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public ProfilesController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewprofile")]
        [Authorize]
        public async Task<IActionResult> ViewProfile([FromQuery] PEMS.Application.Profiles.Queries.ViewProfile.ViewProfileQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateprofile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] PEMS.Application.Profiles.Commands.UpdateProfile.UpdateProfileCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// UC-15 — upload/replace the current user's avatar (multipart field <c>avatar</c>). The user
        /// is resolved from the JWT; one can only change their own avatar. Bytes are buffered here so
        /// the handler can sniff magic bytes and upload to Google Drive in one pass.
        /// </summary>
        [HttpPut("me/avatar")]
        [Authorize]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(5 * 1024 * 1024)] // a little above the 2 MB business limit
        public async Task<IActionResult> UploadAvatar(IFormFile avatar, CancellationToken cancellationToken)
        {
            if (avatar is null || avatar.Length == 0)
                throw new BusinessRuleException("Vui lòng chọn ảnh đại diện.", "AVATAR_FILE_REQUIRED");

            using var ms = new MemoryStream();
            await avatar.CopyToAsync(ms, cancellationToken);

            var result = await _mediator.Send(
                new UploadProfileAvatarCommand(ms.ToArray(), avatar.FileName, avatar.ContentType, avatar.Length),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command, CancellationToken cancellationToken)
        {
            command.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers.UserAgent.ToString();
            command.UserAgent = string.IsNullOrWhiteSpace(ua) ? null : ua;

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}
