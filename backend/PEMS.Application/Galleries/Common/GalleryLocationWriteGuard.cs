using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Shared scope / uniqueness checks for the area-location write commands (UC-LOC-04..09). Keeps the
/// campus-scope rule (every area/location touched must belong to the caller's campus) and the normalized
/// key uniqueness rules in one place so Create and Update enforce them identically.
/// </summary>
internal static class GalleryLocationWriteGuard
{
    /// <summary>Loads an area, asserting it belongs to the caller's campus (404 missing / 403 cross-campus).</summary>
    public static async Task<GalleryArea> LoadAreaInCampusAsync(
        IApplicationDbContext db, ulong areaId, ulong campusId, CancellationToken ct)
    {
        var area = await db.GalleryAreas.FirstOrDefaultAsync(a => a.AreaId == areaId, ct)
            ?? throw new NotFoundException("GalleryArea", areaId);
        if (area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.LocationManageForbidden, "Bạn không có quyền thao tác khu vực này.", 403);
        return area;
    }

    /// <summary>Loads a location (with its area), asserting it belongs to the caller's campus.</summary>
    public static async Task<GalleryLocation> LoadLocationInCampusAsync(
        IApplicationDbContext db, ulong locationId, ulong campusId, CancellationToken ct)
    {
        var location = await db.GalleryLocations
            .Include(l => l.Area)
            .FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
            ?? throw new NotFoundException("GalleryLocation", locationId);
        if (location.Area is null || location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.LocationManageForbidden, "Bạn không có quyền thao tác vị trí này.", 403);
        return location;
    }

    /// <summary>Rejects a duplicate normalized area key within the campus (HTTP 409), optionally ignoring
    /// one row (the area being renamed in place).</summary>
    public static async Task EnsureAreaKeyFreeAsync(
        IApplicationDbContext db, ulong campusId, string areaKey, CancellationToken ct,
        ulong? excludeAreaId = null)
    {
        var exists = await db.GalleryAreas.AnyAsync(
            a => a.CampusId == campusId && a.AreaKey == areaKey
                 && (excludeAreaId == null || a.AreaId != excludeAreaId), ct);
        if (exists)
            throw new ConflictException(
                "Khu vực/tòa này đã tồn tại trong cơ sở.", GalleryErrorCodes.AreaDuplicate);
    }

    /// <summary>Rejects a duplicate normalized location key within the area (HTTP 409), optionally ignoring one row.</summary>
    public static async Task EnsureLocationKeyFreeAsync(
        IApplicationDbContext db, ulong areaId, string locationKey, ulong? excludeLocationId, CancellationToken ct)
    {
        var exists = await db.GalleryLocations.AnyAsync(
            l => l.AreaId == areaId && l.LocationKey == locationKey
                 && (excludeLocationId == null || l.LocationId != excludeLocationId), ct);
        if (exists)
            throw new ConflictException(
                "Vị trí này đã tồn tại trong khu vực đã chọn.", GalleryErrorCodes.LocationDuplicate);
    }
}
