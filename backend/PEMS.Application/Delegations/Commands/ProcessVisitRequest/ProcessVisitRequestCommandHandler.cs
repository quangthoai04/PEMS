using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed class ProcessVisitRequestCommandHandler : IRequestHandler<ProcessVisitRequestCommand, ProcessVisitRequestResponse>
{
    public Task<ProcessVisitRequestResponse> Handle(ProcessVisitRequestCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Process Visit Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}