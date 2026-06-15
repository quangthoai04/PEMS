using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.ScanBusinessCard;

public sealed class ScanBusinessCardCommandHandler : IRequestHandler<ScanBusinessCardCommand, ScanBusinessCardResponse>
{
    public Task<ScanBusinessCardResponse> Handle(ScanBusinessCardCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Scan Business Card has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}