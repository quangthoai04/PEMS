using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateGeneralExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateLogisticsExpenseReport;
using PEMS.Application.Delegations.VisitExpenses.Commands.SaveExpenseReport;
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

        [HttpGet("general/{visitInstanceId}")]
        public async Task<IActionResult> GetGeneralExpenseReport(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetOrCreateGeneralExpenseReportCommand { VisitInstanceId = visitInstanceId }, cancellationToken);
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

        [HttpGet("summary/{visitInstanceId}")]
        public async Task<IActionResult> GetExpenseSummary(ulong visitInstanceId, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(new GetVisitInstanceExpenseSummaryQuery { VisitInstanceId = visitInstanceId }, cancellationToken);
            return Ok(result);
        }
    }
}
