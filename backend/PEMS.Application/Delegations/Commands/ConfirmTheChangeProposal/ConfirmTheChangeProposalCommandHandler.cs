using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ConfirmTheChangeProposal;

public sealed class ConfirmTheChangeProposalCommandHandler : IRequestHandler<ConfirmTheChangeProposalCommand, ConfirmTheChangeProposalResponse>
{
    public Task<ConfirmTheChangeProposalResponse> Handle(ConfirmTheChangeProposalCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Confirm The Change Proposal has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}