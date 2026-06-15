using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DepartmentsController(IMediator mediator) => _mediator = mediator;

        [HttpPost("addnewdepartment")]
        public async Task<IActionResult> AddNewDepartment([FromBody] PEMS.Application.Departments.Commands.AddNewDepartment.AddNewDepartmentCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatedepartment")]
        public async Task<IActionResult> UpdateDepartment([FromBody] PEMS.Application.Departments.Commands.UpdateDepartment.UpdateDepartmentCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchandfilterdepartments")]
        public async Task<IActionResult> SearchandFilterDepartments([FromQuery] PEMS.Application.Departments.Queries.SearchandFilterDepartments.SearchandFilterDepartmentsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewdepartmentlist")]
        public async Task<IActionResult> ViewDepartmentList([FromQuery] PEMS.Application.Departments.Queries.ViewDepartmentList.ViewDepartmentListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewdepartmentdetails")]
        public async Task<IActionResult> ViewDepartmentDetails([FromQuery] PEMS.Application.Departments.Queries.ViewDepartmentDetails.ViewDepartmentDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("managedepartmentstatus")]
        public async Task<IActionResult> ManageDepartmentStatus([FromBody] PEMS.Application.Departments.Commands.ManageDepartmentStatus.ManageDepartmentStatusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("adddepartmentpersonnel")]
        public async Task<IActionResult> AddDepartmentPersonnel([FromBody] PEMS.Application.Departments.Commands.AddDepartmentPersonnel.AddDepartmentPersonnelCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewpersonneldetails")]
        public async Task<IActionResult> ViewPersonnelDetails([FromQuery] PEMS.Application.Departments.Queries.ViewPersonnelDetails.ViewPersonnelDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchpersonnel")]
        public async Task<IActionResult> SearchPersonnel([FromQuery] PEMS.Application.Departments.Queries.SearchPersonnel.SearchPersonnelQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reviewassignedtasks")]
        public async Task<IActionResult> ReviewAssignedTasks([FromBody] PEMS.Application.Departments.Commands.ReviewAssignedTasks.ReviewAssignedTasksCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("assigntasks")]
        public async Task<IActionResult> AssignTasks([FromBody] PEMS.Application.Departments.Commands.AssignTasks.AssignTasksCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("signtheservicedeliveryreport")]
        public async Task<IActionResult> SignTheServiceDeliveryReport([FromBody] PEMS.Application.Departments.Commands.SignTheServiceDeliveryReport.SignTheServiceDeliveryReportCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("removepersonnel")]
        public async Task<IActionResult> RemovePersonnel([FromBody] PEMS.Application.Departments.Commands.RemovePersonnel.RemovePersonnelCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewcoordinationtasks")]
        public async Task<IActionResult> ViewCoordinationTasks([FromQuery] PEMS.Application.Departments.Queries.ViewCoordinationTasks.ViewCoordinationTasksQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchcoordinationtasks")]
        public async Task<IActionResult> SearchCoordinationTasks([FromQuery] PEMS.Application.Departments.Queries.SearchCoordinationTasks.SearchCoordinationTasksQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("reassigndepartmentlead")]
        public async Task<IActionResult> ReassignDepartmentLead([FromBody] PEMS.Application.Departments.Commands.ReassignDepartmentLead.ReassignDepartmentLeadCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}
