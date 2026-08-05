using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Dashboard.Queries.GetDepartmentLeaderDashboardSummary;
using PEMS.Application.Dashboard.Queries.GetHODashboardOverview;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;
using PEMS.Application.Dashboard.Queries.GetStaffCalendarDetail;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DashboardController(IMediator mediator) => _mediator = mediator;

        [HttpGet("department-leader/summary")]
        [PEMS.Api.Filters.RoleAuthorize(PEMS.Application.Common.Security.EffectiveRole.DepartmentLead)]
        public async Task<IActionResult> GetDepartmentLeaderDashboardSummary(CancellationToken cancellationToken)
        {
            var query = new GetDepartmentLeaderDashboardSummaryQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("ho-overview")]
        [PEMS.Api.Filters.RoleAuthorize(PEMS.Application.Common.Security.EffectiveRole.Ho)]
        public async Task<IActionResult> GetHODashboardOverview(CancellationToken cancellationToken)
        {
            var query = new GetHODashboardOverviewQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // ── Dashboard bảng lịch cho Staff Leader (STAFF+LEADER) và Staff thường (STAFF+STAFF) ──
        // viewMode=office (Lịch văn phòng) | mine (Lịch của tôi — chỉ item user là host).
        // Scope/action flags do handler tính; user ngoài role STAFF nhận 403.
        [HttpGet("staff/calendar")]
        [PEMS.Api.Filters.RoleAuthorize(
            PEMS.Application.Common.Security.EffectiveRole.StaffLeader,
            PEMS.Application.Common.Security.EffectiveRole.Staff)]
        public async Task<IActionResult> GetStaffCalendar(
            [FromQuery] string? viewMode,
            [FromQuery] System.DateTime from,
            [FromQuery] System.DateTime to,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetStaffCalendarQuery(viewMode, from, to, year), cancellationToken);
            return Ok(result);
        }

        [HttpGet("staff/calendar/{visitInstanceId}/detail")]
        [PEMS.Api.Filters.RoleAuthorize(
            PEMS.Application.Common.Security.EffectiveRole.StaffLeader,
            PEMS.Application.Common.Security.EffectiveRole.Staff)]
        public async Task<IActionResult> GetStaffCalendarDetail(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetStaffCalendarDetailQuery(visitInstanceId), cancellationToken);
            return Ok(result);
        }

        // NOTE: `GET debug-user` was removed (P0). It was [AllowAnonymous], looked a user up by
        // email and returned a freshly minted access token — an unauthenticated impersonation
        // endpoint for any account whose email an attacker could guess. It must not come back in
        // any form (env flag, renamed route, "dev only"); PEMS.ArchitectureTests asserts its absence.
    }
}


