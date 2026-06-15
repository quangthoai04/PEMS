using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.DeleteAPIConfiguration;

public sealed class DeleteAPIConfigurationCommandHandler : IRequestHandler<DeleteAPIConfigurationCommand, DeleteAPIConfigurationResponse>
{
    public Task<DeleteAPIConfigurationResponse> Handle(DeleteAPIConfigurationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Delete API Configuration has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}