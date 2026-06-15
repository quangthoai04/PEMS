using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

public sealed class AddGalleryItemCommandHandler : IRequestHandler<AddGalleryItemCommand, AddGalleryItemResponse>
{
    public Task<AddGalleryItemResponse> Handle(AddGalleryItemCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Add Gallery Item has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}