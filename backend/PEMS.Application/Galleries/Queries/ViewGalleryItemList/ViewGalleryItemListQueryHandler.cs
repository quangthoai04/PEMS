using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Queries.ViewGalleryItemList;

public sealed class ViewGalleryItemListQueryHandler : IRequestHandler<ViewGalleryItemListQuery, ViewGalleryItemListDto>
{
    public Task<ViewGalleryItemListDto> Handle(ViewGalleryItemListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC View Gallery Item List has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}