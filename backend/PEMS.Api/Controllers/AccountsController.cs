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

    }
}
