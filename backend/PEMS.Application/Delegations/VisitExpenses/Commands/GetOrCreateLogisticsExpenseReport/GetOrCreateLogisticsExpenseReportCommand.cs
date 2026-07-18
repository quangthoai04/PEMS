using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateLogisticsExpenseReport;

public class GetOrCreateLogisticsExpenseReportCommand : IRequest<VisitExpenseReportDto>
{
    public ulong LogisticsItemId { get; set; }
}
