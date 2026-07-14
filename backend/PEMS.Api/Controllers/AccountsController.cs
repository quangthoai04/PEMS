using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEMS.Api.Filters;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AccountsController(IMediator mediator) => _mediator = mediator;

        [HttpGet("viewaccountlist")]
        [EnableRateLimiting("accounts-read")]

        public async Task<IActionResult> ViewAccountList([FromQuery] PEMS.Application.Accounts.Queries.ViewAccountList.ViewAccountListQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("createaccount")]

        public async Task<IActionResult> CreateAccount([FromBody] PEMS.Application.Accounts.Commands.CreateAccount.CreateAccountCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("manageaccountstatus")]

        public async Task<IActionResult> ManageAccountStatus([FromBody] PEMS.Application.Accounts.Commands.ManageAccountStatus.ManageAccountStatusCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("viewaccountdetails")]

        public async Task<IActionResult> ViewAccountDetails([FromQuery] PEMS.Application.Accounts.Queries.ViewAccountDetails.ViewAccountDetailsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("searchandfilteraccounts")]
        [EnableRateLimiting("accounts-read")]

        public async Task<IActionResult> SearchandFilterAccounts([FromQuery] PEMS.Application.Accounts.Queries.SearchandFilterAccounts.SearchandFilterAccountsQuery query, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        [HttpPost("updateaccountrole")]

        public async Task<IActionResult> UpdateAccountRole([FromBody] PEMS.Application.Accounts.Commands.UpdateAccountRole.UpdateAccountRoleCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("statistics")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetStatistics(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Accounts.Queries.ViewAccountStatistics.ViewAccountStatisticsQuery(), cancellationToken);
            return Ok(result);
        }

        [HttpGet("campus-departments")]
        public async Task<IActionResult> GetCampusDepartments(CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new PEMS.Application.Accounts.Queries.GetCampusDepartments.GetCampusDepartmentsQuery(), cancellationToken);
            return Ok(result);
        }

        // UC-100-SL — role-assignment options for the Staff Leader "Chỉnh sửa vai trò" modal.
        // Campus is resolved server-side from the authenticated Staff Leader; only the target
        // account id is accepted. Returns the campus IC department + active GENERAL departments
        // (each flagged with hasHead / isCurrentTargetHead / selectable).
        [HttpGet("role-assignment-options")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetRoleAssignmentOptions([FromQuery] ulong targetUserId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Accounts.Queries.GetRoleAssignmentOptions.GetRoleAssignmentOptionsQuery { TargetUserId = targetUserId },
                cancellationToken);
            return Ok(result);
        }

        // UC-96 — Staff Leader availability pre-check (HO only): can a Trưởng phòng IC be created
        // for the chosen campus? Drives the create modal's warning/disable state.
        [HttpGet("staff-leader-availability")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetStaffLeaderAvailability([FromQuery] ulong campusId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Accounts.Queries.StaffLeaderAvailability.GetStaffLeaderAvailabilityQuery { CampusId = campusId },
                cancellationToken);
            return Ok(result);
        }

        // UC-96 — HO campus pre-check (HO only): can a new HO account be created for the chosen
        // campus? Drives the create modal's warning/disable state.
        [HttpGet("ho-campus-check")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetHoCampusCheck([FromQuery] ulong campusId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Accounts.Queries.HoCampusCheck.GetHoCampusCheckQuery { CampusId = campusId },
                cancellationToken);
            return Ok(result);
        }

        // Replace Staff Leader (HO only) — preview: current IC Head + eligible replacement candidates.
        [HttpGet("staff-leader-replacement-preview")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetStaffLeaderReplacementPreview([FromQuery] ulong campusId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Accounts.Queries.StaffLeaderReplacementPreview.GetStaffLeaderReplacementPreviewQuery { CampusId = campusId },
                cancellationToken);
            return Ok(result);
        }

        // Replace Staff Leader (HO only) — promote an existing IC Staff or create a new leader; the
        // old leader is demoted to STAFF/STAFF, heads repointed, sessions revoked, all in one txn.
        [HttpPost("replacestaffleader")]
        public async Task<IActionResult> ReplaceStaffLeader([FromBody] PEMS.Application.Accounts.Commands.ReplaceStaffLeader.ReplaceStaffLeaderCommand command, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        // Staff Leader "Related Visitor Accounts" tab (read-only). Lists the Visitor accounts related
        // to the caller's campus via visit requests. Scope is resolved from the authenticated Staff
        // Leader inside the handler (any client campusId is ignored); non-Staff-Leaders get 403.
        [HttpGet("related-visitors")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetRelatedVisitors(
            [FromQuery] PEMS.Application.Accounts.Queries.RelatedVisitors.GetRelatedVisitorsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        // Staff Leader "Related Visitor Accounts" tab — detail of one related Visitor. The handler
        // re-checks campus scope and returns 404 for an out-of-scope Visitor (existence not leaked).
        [HttpGet("related-visitor-details")]
        [EnableRateLimiting("accounts-read")]
        public async Task<IActionResult> GetRelatedVisitorDetails(
            [FromQuery] ulong visitorUserId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new PEMS.Application.Accounts.Queries.RelatedVisitors.GetRelatedVisitorDetailsQuery { VisitorUserId = visitorUserId },
                cancellationToken);
            return Ok(result);
        }

    }
}
