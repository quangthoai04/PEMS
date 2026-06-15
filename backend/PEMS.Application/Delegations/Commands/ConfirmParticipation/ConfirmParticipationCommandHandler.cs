using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ConfirmParticipation;

public sealed class ConfirmParticipationCommandHandler : IRequestHandler<ConfirmParticipationCommand, ConfirmParticipationResponse>
{
    public Task<ConfirmParticipationResponse> Handle(ConfirmParticipationCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Confirm Participation has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}