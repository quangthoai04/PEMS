using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateLogisticsExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Commands.InitializeGeneralExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Commands.RemindExpenseReports;
using PEMS.Application.Delegations.VisitExpenses.Commands.SaveExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Queries.GetGeneralExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Queries.GetVisitInstanceExpenseSummary;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VisitExpensesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public VisitExpensesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Reads the instance's general expense report. A pure read: it creates nothing, so opening the
        /// "Sau tiếp khách" tab on a campus that has no report yet answers 204 rather than failing.
        /// </summary>
        [HttpGet("general/{visitInstanceId}")]
        public async Task<IActionResult> GetGeneralExpenseReport(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetGeneralExpenseReportQuery { VisitInstanceId = visitInstanceId }, cancellationToken);
            return result is null ? NoContent() : Ok(result);
        }

        /// <summary>Opens the general expense report — Host only, AFTER_VISIT only, idempotent.</summary>
        [HttpPost("general/{visitInstanceId}/initialize")]
        public async Task<IActionResult> InitializeGeneralExpenseReport(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new InitializeGeneralExpenseReportCommand { VisitInstanceId = visitInstanceId }, cancellationToken);
            return Ok(result);
        }

        [HttpGet("logistics/{logisticsItemId}")]
        public async Task<IActionResult> GetLogisticsExpenseReport(ulong logisticsItemId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetOrCreateLogisticsExpenseReportCommand { LogisticsItemId = logisticsItemId }, cancellationToken);
            return Ok(result);
        }

        [HttpPut("{expenseReportId}")]
        public async Task<IActionResult> SaveExpenseReport(ulong expenseReportId, [FromBody] SaveExpenseReportCommand command, CancellationToken cancellationToken)
        {
            command.ExpenseReportId = expenseReportId;
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("remind/{visitInstanceId}")]
        public async Task<IActionResult> RemindExpenseReports(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new RemindExpenseReportsCommand { VisitInstanceId = visitInstanceId }, cancellationToken);
            return Ok(result);
        }

        [HttpGet("summary/{visitInstanceId}")]
        public async Task<IActionResult> GetExpenseSummary(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = visitInstanceId }, cancellationToken);
            return Ok(result);
        }
    }
}
