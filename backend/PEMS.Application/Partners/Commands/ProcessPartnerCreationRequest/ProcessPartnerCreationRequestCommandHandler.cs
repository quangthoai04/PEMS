using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Partners.Commands.ProcessPartnerCreationRequest;

public sealed class ProcessPartnerCreationRequestCommandHandler : IRequestHandler<ProcessPartnerCreationRequestCommand, ProcessPartnerCreationRequestResponse>
{
    public Task<ProcessPartnerCreationRequestResponse> Handle(ProcessPartnerCreationRequestCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Process Partner Creation Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}