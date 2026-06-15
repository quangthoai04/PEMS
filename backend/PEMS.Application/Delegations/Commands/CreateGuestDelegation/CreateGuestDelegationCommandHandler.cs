using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.CreateGuestDelegation;

public sealed class CreateGuestDelegationCommandHandler : IRequestHandler<CreateGuestDelegationCommand, CreateGuestDelegationResponse>
{
    public Task<CreateGuestDelegationResponse> Handle(CreateGuestDelegationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Create Guest Delegation has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}