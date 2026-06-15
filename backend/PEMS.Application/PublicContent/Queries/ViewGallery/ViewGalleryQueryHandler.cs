using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.PublicContent.Queries.ViewGallery;

public sealed class ViewGalleryQueryHandler : IRequestHandler<ViewGalleryQuery, ViewGalleryDto>
{
    public Task<ViewGalleryDto> Handle(ViewGalleryQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Gallery has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}