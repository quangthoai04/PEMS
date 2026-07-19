using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.SearchGalleryItems;

/// <summary>UC-GAL-02 handler — delegates to the shared <see cref="GalleryItemListQueryExecutor"/>.</summary>
public sealed class SearchGalleryItemsQueryHandler
    : IRequestHandler<SearchGalleryItemsQuery, PaginatedResult<GalleryItemListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SearchGalleryItemsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<PaginatedResult<GalleryItemListItemDto>> Handle(
        SearchGalleryItemsQuery request, CancellationToken cancellationToken)
        => GalleryItemListQueryExecutor.ExecuteAsync(_db, _currentUser, request, cancellationToken);
}
