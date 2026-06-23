using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Dashboard.Queries.GetDepartmentLeaderDashboardSummary;

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
        [PEMS.Api.Filters.RequirePermission("UC-69.VIEW_DASHBOARD_STATISTICS", PEMS.Domain.Constants.PermissionLevels.Read)]
        public async Task<IActionResult> GetDepartmentLeaderDashboardSummary(CancellationToken cancellationToken)
        {
            var query = new GetDepartmentLeaderDashboardSummaryQuery();
            var result = await _mediator.Send(query, cancellationToken);
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
