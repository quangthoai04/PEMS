using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ApproveResourceRequest;

public sealed class ApproveResourceRequestCommandHandler : IRequestHandler<ApproveResourceRequestCommand, ApproveResourceRequestResponse>
{
    public Task<ApproveResourceRequestResponse> Handle(ApproveResourceRequestCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Approve Resource Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}