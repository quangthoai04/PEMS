using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.ConfigureRequestLimit;

public sealed class ConfigureRequestLimitCommandHandler : IRequestHandler<ConfigureRequestLimitCommand, ConfigureRequestLimitResponse>
{
    public Task<ConfigureRequestLimitResponse> Handle(ConfigureRequestLimitCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Configure Request Limit has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}