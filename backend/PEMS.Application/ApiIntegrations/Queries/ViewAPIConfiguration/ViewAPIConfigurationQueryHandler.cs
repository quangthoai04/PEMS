using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Queries.ViewAPIConfiguration;

public sealed class ViewAPIConfigurationQueryHandler : IRequestHandler<ViewAPIConfigurationQuery, ViewAPIConfigurationDto>
{
    public Task<ViewAPIConfigurationDto> Handle(ViewAPIConfigurationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View API Configuration has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}