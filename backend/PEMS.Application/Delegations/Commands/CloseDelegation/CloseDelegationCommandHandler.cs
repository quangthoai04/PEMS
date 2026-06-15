using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.CloseDelegation;

public sealed class CloseDelegationCommandHandler : IRequestHandler<CloseDelegationCommand, CloseDelegationResponse>
{
    public Task<CloseDelegationResponse> Handle(CloseDelegationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Close Delegation has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}