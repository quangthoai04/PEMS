using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetStaffCalendarQuery(viewMode, from, to), cancellationToken);
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

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [HttpGet("debug-user")]
        public async Task<IActionResult> DebugUser(
            [FromQuery] string email,
            [FromServices] PEMS.Application.Common.Interfaces.IApplicationDbContext dbContext,
            [FromServices] global::Application.Common.Interfaces.IJwtTokenService jwtTokenService,
            CancellationToken cancellationToken)
        {
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                dbContext.Users.Include(u => u.Role), u => u.Email == email, cancellationToken);
            if (user == null) return NotFound("User not found");

            var tokenResult = jwtTokenService.GenerateAccessToken(user, 1, "LOCAL");
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokenResult.Token);

            var claims = jwt.Claims.Select(c => new { c.Type, c.Value }).ToList();

            return Ok(new
            {
                user.UserId,
                RoleCode = user.Role?.RoleCode,
                user.DepartmentId,
                Token = tokenResult.Token,
                Claims = claims
            });
        }
    }
}


