using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Queries.SearchAPILogs;

public sealed class SearchAPILogsQueryHandler : IRequestHandler<SearchAPILogsQuery, SearchAPILogsDto>
{
    public Task<SearchAPILogsDto> Handle(SearchAPILogsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search API Logs has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}