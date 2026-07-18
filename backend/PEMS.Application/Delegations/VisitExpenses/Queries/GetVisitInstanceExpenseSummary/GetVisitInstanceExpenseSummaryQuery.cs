using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.VisitExpenses.Queries.GetVisitInstanceExpenseSummary;

public class GetVisitInstanceExpenseSummaryQuery : IRequest<VisitInstanceExpenseSummaryDto>
{
    public ulong VisitInstanceId { get; set; }
}

public class VisitInstanceExpenseSummaryDto
{
    public ulong VisitInstanceId { get; set; }
    public decimal TotalAmount { get; set; }
    
    public VisitExpenseReportDto? GeneralReport { get; set; }
    public List<VisitExpenseReportDto> LogisticsReports { get; set; } = new();
}
