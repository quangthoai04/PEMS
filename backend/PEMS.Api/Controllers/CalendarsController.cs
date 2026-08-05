using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Personal and department calendars (UC-72..UC-78). Had no authorization attribute at all,
    /// so "my events" was readable anonymously. Roles per matrix 5.12; the handlers still scope
    /// each read to the caller's own events / own department.
    /// </summary>
    [ApiController]
    [Authorize]
    [RoleAuthorize(
        EffectiveRole.StaffLeader,
        EffectiveRole.Staff,
        EffectiveRole.DepartmentLead,
        EffectiveRole.Department,
        EffectiveRole.Student)]
    [Route("api/[controller]")]
    public class CalendarsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public CalendarsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewmyevents")]
        public async Task<IActionResult> ViewMyEvents([FromQuery] PEMS.Application.Calendars.Queries.ViewMyEvents.ViewMyEventsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewdepartmentcalendar")]
        public async Task<IActionResult> ViewDepartmentCalendar([FromQuery] PEMS.Application.Calendars.Queries.ViewDepartmentCalendar.ViewDepartmentCalendarQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("switchviewmode")]
        public async Task<IActionResult> SwitchViewMode([FromBody] PEMS.Application.Calendars.Commands.SwitchViewMode.SwitchViewModeCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("addpersonalevent")]
        public async Task<IActionResult> AddPersonalEvent([FromBody] PEMS.Application.Calendars.Commands.AddPersonalEvent.AddPersonalEventCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("deletepersonalevent")]
        public async Task<IActionResult> DeletePersonalEvent([FromBody] PEMS.Application.Calendars.Commands.DeletePersonalEvent.DeletePersonalEventCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatepersonalevent")]
        public async Task<IActionResult> UpdatePersonalEvent([FromBody] PEMS.Application.Calendars.Commands.UpdatePersonalEvent.UpdatePersonalEventCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("vieweventdetails")]
        public async Task<IActionResult> ViewEventDetails([FromQuery] PEMS.Application.Calendars.Queries.ViewEventDetails.ViewEventDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

    }
}
