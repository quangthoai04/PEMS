using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampusesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IApplicationDbContext _db;
        public CampusesController(IMediator mediator, IApplicationDbContext db)
        {
            _mediator = mediator;
            _db = db;
        }

        /// <summary>
        /// Returns active campuses for the login page campus dropdown.
        /// Anonymous access — no authentication required.
        /// </summary>
        [HttpGet("active")]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveCampuses(CancellationToken cancellationToken)
        {
            var campuses = await _db.Campuses
                .Where(c => c.Status == "ACTIVE")
                .OrderBy(c => c.CampusCode)
                .Select(c => new { campusId = c.CampusId, campusCode = c.CampusCode, campusName = c.Name })
                .ToListAsync(cancellationToken);
            return Ok(campuses);
        }

        [HttpPost("addnewcampus")]
        public async Task<IActionResult> AddNewCampus([FromBody] PEMS.Application.Campuses.Commands.AddNewCampus.AddNewCampusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewcampuslist")]
        public async Task<IActionResult> ViewCampusList([FromQuery] PEMS.Application.Campuses.Queries.ViewCampusList.ViewCampusListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchandfiltercampus")]
        public async Task<IActionResult> SearchandFilterCampus([FromQuery] PEMS.Application.Campuses.Queries.SearchandFilterCampus.SearchandFilterCampusQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewcampusdetails")]
        public async Task<IActionResult> ViewCampusDetails([FromQuery] PEMS.Application.Campuses.Queries.ViewCampusDetails.ViewCampusDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updatecampus")]
        public async Task<IActionResult> UpdateCampus([FromBody] PEMS.Application.Campuses.Commands.UpdateCampus.UpdateCampusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("managecampusstatus")]
        public async Task<IActionResult> ManageCampusStatus([FromBody] PEMS.Application.Campuses.Commands.ManageCampusStatus.ManageCampusStatusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("assigncampuslead")]
        public async Task<IActionResult> AssignCampusLead([FromBody] PEMS.Application.Campuses.Commands.AssignCampusLead.AssignCampusLeadCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

    }
}
