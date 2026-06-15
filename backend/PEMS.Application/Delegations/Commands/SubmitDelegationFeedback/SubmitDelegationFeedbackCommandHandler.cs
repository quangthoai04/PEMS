using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.SubmitDelegationFeedback;

public sealed class SubmitDelegationFeedbackCommandHandler : IRequestHandler<SubmitDelegationFeedbackCommand, SubmitDelegationFeedbackResponse>
{
    public Task<SubmitDelegationFeedbackResponse> Handle(SubmitDelegationFeedbackCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Submit Delegation Feedback has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}