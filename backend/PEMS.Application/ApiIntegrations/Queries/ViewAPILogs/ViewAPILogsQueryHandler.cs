using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Queries.ViewAPILogs;

public sealed class ViewAPILogsQueryHandler : IRequestHandler<ViewAPILogsQuery, ViewAPILogsDto>
{
    public Task<ViewAPILogsDto> Handle(ViewAPILogsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View API Logs has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}