using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ProcessVisitRequest;

public sealed class ProcessVisitRequestCommandHandler : IRequestHandler<ProcessVisitRequestCommand, ProcessVisitRequestResponse>
{
    public Task<ProcessVisitRequestResponse> Handle(ProcessVisitRequestCommand request, CancellationToken cancellationToken)
    {
        // TODO: Enforce business rules when implemented
        // if (role == "HO" && visitRequest.VisitScope != "MULTI_CAMPUS") throw ForbiddenAccessException...
        // if (role == "STAFF" && subRole == "Leader" && visitRequest.VisitScope != "SINGLE_CAMPUS") throw ForbiddenAccessException...

        throw new NotImplementedException("UC Process Visit Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}