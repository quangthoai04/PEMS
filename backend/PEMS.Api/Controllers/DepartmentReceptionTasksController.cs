using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetDepartmentCalendar;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetInvitationDetail;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetRequestDetail;
using PEMS.Application.DepartmentReceptionTasks.Commands.AcceptInvitation;
using PEMS.Application.DepartmentReceptionTasks.Commands.DeclineInvitation;
using PEMS.Application.DepartmentReceptionTasks.Commands.ConfirmRequest;
using PEMS.Application.DepartmentReceptionTasks.Commands.RejectRequest;
using PEMS.Application.DepartmentReceptionTasks.Commands.ProposeRequestChange;
using PEMS.Application.DepartmentReceptionTasks.Commands.AssignRequestAssignee;
using PEMS.Application.DepartmentReceptionTasks.Commands.CreatePersonalEvent;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetDepartmentAssigneeCandidates;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/department/reception-tasks")]
    [Authorize]
    public class DepartmentReceptionTasksController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DepartmentReceptionTasksController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar([FromQuery] GetDepartmentCalendarQuery query)
        {
            return Ok(await _mediator.Send(query));
        }

        [HttpGet("invitations/{participantId}")]
        public async Task<IActionResult> GetInvitationDetail(ulong participantId)
        {
            return Ok(await _mediator.Send(new GetInvitationDetailQuery { ParticipantId = participantId }));
        }

        [HttpPost("invitations/{participantId}/accept")]
        public async Task<IActionResult> AcceptInvitation(ulong participantId)
        {
            return Ok(await _mediator.Send(new AcceptInvitationCommand { ParticipantId = participantId }));
        }

        [HttpPost("invitations/{participantId}/decline")]
        public async Task<IActionResult> DeclineInvitation(ulong participantId, [FromBody] DeclineInvitationCommand command)
        {
            command.ParticipantId = participantId;
            return Ok(await _mediator.Send(command));
        }

        [HttpGet("requests/{logisticsItemId}")]
        public async Task<IActionResult> GetRequestDetail(ulong logisticsItemId)
        {
            return Ok(await _mediator.Send(new GetRequestDetailQuery { LogisticsItemId = logisticsItemId }));
        }

        [HttpPost("requests/{logisticsItemId}/confirm")]
        public async Task<IActionResult> ConfirmRequest(ulong logisticsItemId)
        {
            return Ok(await _mediator.Send(new ConfirmRequestCommand { LogisticsItemId = logisticsItemId }));
        }

        [HttpPost("requests/{logisticsItemId}/reject")]
        public async Task<IActionResult> RejectRequest(ulong logisticsItemId, [FromBody] RejectRequestCommand command)
        {
            command.LogisticsItemId = logisticsItemId;
            return Ok(await _mediator.Send(command));
        }

        [HttpPost("requests/{logisticsItemId}/propose-change")]
        public async Task<IActionResult> ProposeChange(ulong logisticsItemId, [FromBody] ProposeRequestChangeCommand command)
        {
            command.LogisticsItemId = logisticsItemId;
            return Ok(await _mediator.Send(command));
        }

        [HttpPost("requests/{logisticsItemId}/assign")]
        public async Task<IActionResult> AssignAssignee(ulong logisticsItemId, [FromBody] AssignRequestAssigneeCommand command)
        {
            command.LogisticsItemId = logisticsItemId;
            return Ok(await _mediator.Send(command));
        }

        [HttpGet("assignee-candidates")]
        public async Task<IActionResult> GetDepartmentAssigneeCandidates()
        {
            return Ok(await _mediator.Send(new GetDepartmentAssigneeCandidatesQuery()));
        }

        [HttpPost("personal-events")]
        public async Task<IActionResult> CreatePersonalEvent([FromBody] CreatePersonalEventCommand command)
        {
            return Ok(await _mediator.Send(command));
        }
    }
}
