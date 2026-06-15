using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.ApiIntegrations.Commands.ManageAPIStatus;

public sealed class ManageAPIStatusCommandHandler : IRequestHandler<ManageAPIStatusCommand, ManageAPIStatusResponse>
{
    public Task<ManageAPIStatusResponse> Handle(ManageAPIStatusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Manage API Status has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}