using MediatR;

namespace PEMS.Application.Delegations.Queries.GetVisitInstanceSummary;

public sealed class GetVisitInstanceSummaryQuery : IRequest<ProcessSummaryPageDto>
{
    public ulong VisitInstanceId { get; }
    public GetVisitInstanceSummaryQuery(ulong visitInstanceId) => VisitInstanceId = visitInstanceId;
}
