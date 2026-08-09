using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;

namespace PEMS.Application.Delegations.VisitExpenses.Commands.InitializeGeneralExpenseReport;

/// <summary>
/// Opens the general (Host) expense report for one visit instance. Idempotent: called twice, the second
/// call returns the report the first one made rather than a duplicate.
/// </summary>
public sealed class InitializeGeneralExpenseReportCommand : IRequest<VisitExpenseReportDto>
{
    public ulong VisitInstanceId { get; set; }
}
