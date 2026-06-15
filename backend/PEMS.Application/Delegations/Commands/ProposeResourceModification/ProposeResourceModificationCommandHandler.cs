using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ProposeResourceModification;

public sealed class ProposeResourceModificationCommandHandler : IRequestHandler<ProposeResourceModificationCommand, ProposeResourceModificationResponse>
{
    public Task<ProposeResourceModificationResponse> Handle(ProposeResourceModificationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Propose Resource Modification has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}