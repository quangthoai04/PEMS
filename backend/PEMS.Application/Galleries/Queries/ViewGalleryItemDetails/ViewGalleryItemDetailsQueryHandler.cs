using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.ViewGalleryItemDetails;

/// <summary>
/// UC-GAL-03 handler. Loads a gallery item and enforces the Staff Leader campus scope: missing /
/// soft-deleted → 404 (AF-GAL-DETAIL-01); in another campus → 403 (AF-GAL-DETAIL-02). The full detail
/// (including only ACTIVE media) is built by the shared <see cref="GalleryDetailBuilder"/>.
/// </summary>
public sealed class ViewGalleryItemDetailsQueryHandler
    : IRequestHandler<ViewGalleryItemDetailsQuery, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewGalleryItemDetailsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GalleryItemDetailDto> Handle(
        ViewGalleryItemDetailsQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var itemId = (ulong)request.GalleryItemId;

        var scope = await _db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == itemId && i.DeletedAt == null)
            .Select(i => new { i.GalleryItemId, CampusId = i.Location.Area.CampusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("GalleryItem", itemId);

        if (scope.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền xem gallery item này.", 403);

        return await GalleryDetailBuilder.BuildAsync(_db, itemId, cancellationToken);
    }
}
