using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.UpdateAPIConfiguration;

public sealed class UpdateAPIConfigurationCommandHandler : IRequestHandler<UpdateAPIConfigurationCommand, UpdateAPIConfigurationResponse>
{
    public Task<UpdateAPIConfigurationResponse> Handle(UpdateAPIConfigurationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update API Configuration has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}