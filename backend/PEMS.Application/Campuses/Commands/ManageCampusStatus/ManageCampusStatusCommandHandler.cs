using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Campuses.Commands.ManageCampusStatus;

public sealed class ManageCampusStatusCommandHandler : IRequestHandler<ManageCampusStatusCommand, ManageCampusStatusResponse>
{
    public Task<ManageCampusStatusResponse> Handle(ManageCampusStatusCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Manage Campus Status has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}