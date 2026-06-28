using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Validates that a target <c>gallery_locations</c> row is usable by the current Staff Leader: it must
/// exist, belong (via its area) to the caller's campus, and have both its location and area ACTIVE.
/// Used by Add (UC-GAL-04) and Edit (UC-GAL-07) before media is uploaded.
/// </summary>
internal static class GalleryLocationGuard
{
    public static async Task<GalleryLocation> LoadActiveLocationInCurrentCampusAsync(
        IApplicationDbContext db, ulong locationId, ulong campusId, CancellationToken ct)
    {
        var location = await db.GalleryLocations
            .Include(l => l.Area)
            .FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
            ?? throw new NotFoundException("GalleryLocation", locationId);

        if (location.Area is null || location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.LocationScopeForbidden,
                "Bạn không có quyền thêm media vào vị trí này.", 403);

        if (location.Status != EntityStatuses.Active || location.Area.Status != EntityStatuses.Active)
            throw new BusinessRuleException(
                "Vị trí này đang ngừng hoạt động, không thể upload media mới.",
                GalleryErrorCodes.LocationInactive);

        return location;
    }
}
