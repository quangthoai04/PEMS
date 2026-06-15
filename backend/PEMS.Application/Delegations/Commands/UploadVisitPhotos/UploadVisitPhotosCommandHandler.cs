using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.UploadVisitPhotos;

public sealed class UploadVisitPhotosCommandHandler : IRequestHandler<UploadVisitPhotosCommand, UploadVisitPhotosResponse>
{
    public Task<UploadVisitPhotosResponse> Handle(UploadVisitPhotosCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Upload Visit Photos has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}