using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

public sealed class UpdateGalleryItemCommandHandler : IRequestHandler<UpdateGalleryItemCommand, UpdateGalleryItemResponse>
{
    public Task<UpdateGalleryItemResponse> Handle(UpdateGalleryItemCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Update Gallery Item has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}