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
using PEMS.Application.DepartmentReceptionTasks.Commands.AcceptAssignedLogisticsTask;
using PEMS.Application.DepartmentReceptionTasks.Commands.DeclineAssignedLogisticsTask;
using PEMS.Application.DepartmentReceptionTasks.Commands.CreatePersonalEvent;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetDepartmentAssigneeCandidates;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetAssignmentsProgressList;
using PEMS.Application.DepartmentReceptionTasks.Queries.GetAttentionItems;
using PEMS.Application.Delegations.Commands.AssignDepartmentStaff;

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

        [HttpGet("assignments-progress")]
        public async Task<IActionResult> GetAssignmentsProgress([FromQuery] GetAssignmentsProgressListQuery query)
        {
            return Ok(await _mediator.Send(query));
        }

        [HttpGet("attention-items")]
        public async Task<IActionResult> GetAttentionItems()
        {
            return Ok(await _mediator.Send(new GetAttentionItemsQuery()));
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

        [HttpPost("invitations/{participantId}/assign")]
        public async Task<IActionResult> AssignInvitation(ulong participantId, [FromBody] DepartmentInvitationAssignBody body)
        {
            return Ok(await _mediator.Send(new AssignDepartmentStaffCommand(participantId, body.AssigneeUserId, body.Note ?? "")));
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

        [HttpPost("requests/{logisticsItemId}/accept-self")]
        public async Task<IActionResult> AcceptRequestSelf(ulong logisticsItemId)
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

        [HttpPost("requests/{logisticsItemId}/accept-assignment")]
        public async Task<IActionResult> AcceptAssignment(ulong logisticsItemId)
        {
            return Ok(await _mediator.Send(new AcceptAssignedLogisticsTaskCommand { LogisticsItemId = logisticsItemId }));
        }

        [HttpPost("requests/{logisticsItemId}/decline-assignment")]
        public async Task<IActionResult> DeclineAssignment(ulong logisticsItemId, [FromBody] DeclineAssignedLogisticsTaskCommand command)
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

    public sealed record DepartmentInvitationAssignBody(ulong AssigneeUserId, string? Note);
}
