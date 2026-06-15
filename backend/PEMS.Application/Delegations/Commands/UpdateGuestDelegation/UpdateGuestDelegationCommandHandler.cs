using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.UpdateGuestDelegation;

public sealed class UpdateGuestDelegationCommandHandler : IRequestHandler<UpdateGuestDelegationCommand, UpdateGuestDelegationResponse>
{
    public Task<UpdateGuestDelegationResponse> Handle(UpdateGuestDelegationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Guest Delegation has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}