using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Reports.Queries.FilterDashboardByTime;

public sealed class FilterDashboardByTimeQueryHandler : IRequestHandler<FilterDashboardByTimeQuery, FilterDashboardByTimeDto>
{
    public Task<FilterDashboardByTimeDto> Handle(FilterDashboardByTimeQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Filter Dashboard By Time has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}