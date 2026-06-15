using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ApproveCrossCampusRequest;

public sealed class ApproveCrossCampusRequestCommandHandler : IRequestHandler<ApproveCrossCampusRequestCommand, ApproveCrossCampusRequestResponse>
{
    public Task<ApproveCrossCampusRequestResponse> Handle(ApproveCrossCampusRequestCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Approve Cross-Campus Request has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}