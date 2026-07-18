using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.GetOrCreateGeneralExpenseReport;

public class GetOrCreateGeneralExpenseReportCommand : IRequest<VisitExpenseReportDto>
{
    public ulong VisitInstanceId { get; set; }
}
