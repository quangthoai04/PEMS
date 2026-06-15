using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

public sealed class DeleteGalleryItemCommandHandler : IRequestHandler<DeleteGalleryItemCommand, DeleteGalleryItemResponse>
{
    public Task<DeleteGalleryItemResponse> Handle(DeleteGalleryItemCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Delete Gallery Item has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}