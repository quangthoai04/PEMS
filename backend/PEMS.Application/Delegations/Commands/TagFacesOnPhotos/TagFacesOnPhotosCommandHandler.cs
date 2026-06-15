using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Delegations.Commands.TagFacesonPhotos;

public sealed class TagFacesonPhotosCommandHandler : IRequestHandler<TagFacesonPhotosCommand, TagFacesonPhotosResponse>
{
    public Task<TagFacesonPhotosResponse> Handle(TagFacesonPhotosCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Tag Faces on Photos has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}