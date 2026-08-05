using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PEMS.Api.Filters;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    /// <summary>
    /// Campus master data. Per PERMISSION_MATRIX §5.14 (UC-81..UC-87) and
    /// docs/CampusManagement/00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md the only actor is HO.
    ///
    /// ADMIN is a technical administrator, not a business superuser, and is refused here —
    /// it previously reached every action below because the class carried a bare
    /// [Authorize] and the actions carried nothing at all, so any authenticated role
    /// (including ADMIN, STUDENT and VISITOR) could list, create and disable campuses.
    ///
    /// The two anonymous actions are genuinely public surface: the login campus dropdown
    /// and the visit-registration campus options.
    /// </summary>
    [ApiController]
    [Authorize]
    [RoleAuthorize(EffectiveRole.Ho)]
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
            var campuses = await _mediator.Send(new PEMS.Application.Campuses.Queries.GetActiveCampuses.GetActiveCampusesQuery(), cancellationToken);
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

        [HttpGet("filter-options")]
        public async Task<IActionResult> GetFilterOptions(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Campuses.Queries.GetCampusFilterOptions.GetCampusFilterOptionsQuery(), cancellationToken);
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

        /// <summary>
        /// UC-86 §18 status-impact preview for the confirmation modal (blockers on disable,
        /// activation issues on enable). Read-only; the status command still rechecks.
        /// </summary>
        [HttpGet("campusstatusimpact")]
        public async Task<IActionResult> GetCampusStatusImpact([FromQuery] PEMS.Application.Campuses.Queries.GetCampusStatusImpact.GetCampusStatusImpactQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Campus options for the visit registration form (UC-86 §10). Anonymous — only campuses
        /// operationally available for registration (ACTIVE + active IC dept + valid Staff Leader).
        /// </summary>
        [HttpGet("available-for-registration")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRegistrationCampuses(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Campuses.Queries.GetRegistrationCampuses.GetRegistrationCampusesQuery(), cancellationToken);
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
