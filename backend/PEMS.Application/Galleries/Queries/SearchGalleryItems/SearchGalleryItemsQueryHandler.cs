using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace PEMS.Application.Galleries.Queries.SearchGalleryItems;

public sealed class SearchGalleryItemsQueryHandler : IRequestHandler<SearchGalleryItemsQuery, SearchGalleryItemsDto>
{
    public Task<SearchGalleryItemsDto> Handle(SearchGalleryItemsQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("UC Search Gallery Items has been scaffolded. Business rules must be implemented after UC specification is completed.");
    }
}