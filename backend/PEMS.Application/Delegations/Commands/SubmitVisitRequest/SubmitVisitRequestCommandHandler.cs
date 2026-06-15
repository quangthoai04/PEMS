using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.SubmitVisitRequest;

public sealed class SubmitVisitRequestCommandHandler : IRequestHandler<SubmitVisitRequestCommand, SubmitVisitRequestResponse>
{
    public Task<SubmitVisitRequestResponse> Handle(SubmitVisitRequestCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Submit Visit Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}