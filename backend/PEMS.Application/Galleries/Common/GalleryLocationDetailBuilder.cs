using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Builds the <see cref="GalleryLocationDetailDto"/> for one location — shared as the success payload
/// of Create (UC-LOC-04/05), Update (UC-LOC-06/07) and the status toggle (UC-LOC-08/09). Reports the
/// aggregate gallery-item counts of the location (a location may hold many items now). Caller owns the
/// scope check; this only reads. Pomelo-safe (single-join projection + one flat item lookup).
/// </summary>
internal static class GalleryLocationDetailBuilder
{
    public static async Task<GalleryLocationDetailDto> BuildAsync(
        IApplicationDbContext db, ulong locationId, CancellationToken ct, string? message = null)
    {
        var head = await db.GalleryLocations.AsNoTracking()
            .Where(l => l.LocationId == locationId)
            .Select(l => new
            {
                l.LocationId,
                l.AreaId,
                AreaName = l.Area.AreaName,
                AreaCoverFileId = l.Area.CoverFileId,
                l.LocationName,
                LocationCoverFileId = l.CoverFileId,
                l.Status,
                l.CreatedAt,
                l.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("GalleryLocation", locationId);

        var items = await db.GalleryItems.AsNoTracking()
            .Where(i => i.LocationId == locationId && i.DeletedAt == null)
            .Select(i => new { i.Status, i.ItemType })
            .ToListAsync(ct);

        return new GalleryLocationDetailDto
        {
            LocationId = head.LocationId,
            AreaId = head.AreaId,
            AreaName = head.AreaName,
            AreaCoverFileId = head.AreaCoverFileId,
            AreaCoverUrl = GalleryFileUrls.ContentOrNull(head.AreaCoverFileId),
            LocationName = head.LocationName,
            LocationCoverFileId = head.LocationCoverFileId,
            LocationCoverUrl = GalleryFileUrls.ContentOrNull(head.LocationCoverFileId),
            Status = head.Status,
            CreatedAt = head.CreatedAt,
            UpdatedAt = head.UpdatedAt,
            HasGalleryItems = items.Count > 0,
            GalleryItemCount = items.Count,
            PublishedGalleryItemCount = items.Count(i => i.Status == "PUBLISHED"),
            HiddenGalleryItemCount = items.Count(i => i.Status == "HIDDEN"),
            MediaCount = items.Count(i => i.ItemType == GalleryItemTypes.Media),
            VisitDelegationCount = items.Count(i => i.ItemType == GalleryItemTypes.VisitDelegation),
            Message = message,
        };
    }
}
