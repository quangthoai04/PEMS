using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.CreateAPIConfiguration;

public sealed class CreateAPIConfigurationCommandHandler : IRequestHandler<CreateAPIConfigurationCommand, CreateAPIConfigurationResponse>
{
    public Task<CreateAPIConfigurationResponse> Handle(CreateAPIConfigurationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create API Configuration has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}