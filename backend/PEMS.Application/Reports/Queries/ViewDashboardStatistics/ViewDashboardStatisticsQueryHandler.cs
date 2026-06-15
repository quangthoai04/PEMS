using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Reports.Queries.ViewDashboardStatistics;

public sealed class ViewDashboardStatisticsQueryHandler : IRequestHandler<ViewDashboardStatisticsQuery, ViewDashboardStatisticsDto>
{
    public Task<ViewDashboardStatisticsDto> Handle(ViewDashboardStatisticsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Dashboard Statistics has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}