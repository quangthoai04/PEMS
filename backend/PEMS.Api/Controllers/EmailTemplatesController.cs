using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Queries.PreviewEmailTemplate;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/email-templates")]
    [RoleAuthorize(
        EffectiveRole.StaffLeader,
        EffectiveRole.Staff,
        EffectiveRole.DepartmentLead,
        EffectiveRole.Department,
        EffectiveRole.Ho
    )]
    public class EmailTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public EmailTemplatesController(IMediator mediator) => _mediator = mediator;

        // Render a template's subject/body for the "Xem trước email" modal. Read-only — the real
        // send still happens only when the user presses "Mời" / "Gửi yêu cầu".
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] PreviewEmailTemplateQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}
