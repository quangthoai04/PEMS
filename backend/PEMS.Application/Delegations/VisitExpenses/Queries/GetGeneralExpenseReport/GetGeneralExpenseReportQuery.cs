using MediatR;
using PEMS.Application.Delegations.VisitExpenses.Models;

namespace PEMS.Application.Delegations.VisitExpenses.Queries.GetGeneralExpenseReport;

/// <summary>
/// Reads the general (Host) expense report of one visit instance. A READ — it never creates anything.
///
/// Returns null when the instance has no general report yet. That is a normal state, not a failure:
/// opening the "Sau tiếp khách" tab on a campus that is still under way must not manufacture a report,
/// and must not produce an error either.
/// </summary>
public sealed class GetGeneralExpenseReportQuery : IRequest<VisitExpenseReportDto?>
{
    public ulong VisitInstanceId { get; set; }
}
