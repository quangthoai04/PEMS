using System.Threading;
using System.Threading.Tasks;
using MediatR;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.ViewGalleryItemList;

/// <summary>UC-GAL-01 handler — delegates to the shared <see cref="GalleryItemListQueryExecutor"/>.</summary>
public sealed class ViewGalleryItemListQueryHandler
    : IRequestHandler<ViewGalleryItemListQuery, PaginatedResult<GalleryItemListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewGalleryItemListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public Task<PaginatedResult<GalleryItemListItemDto>> Handle(
        ViewGalleryItemListQuery request, CancellationToken cancellationToken)
        => GalleryItemListQueryExecutor.ExecuteAsync(_db, _currentUser, request, cancellationToken);
}
