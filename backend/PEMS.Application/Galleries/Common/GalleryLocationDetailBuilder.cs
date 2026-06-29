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
                l.LocationName,
                l.Status,
                l.CreatedAt,
                l.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("GalleryLocation", locationId);

        var statuses = await db.GalleryItems.AsNoTracking()
            .Where(i => i.LocationId == locationId && i.DeletedAt == null)
            .Select(i => i.Status)
            .ToListAsync(ct);

        return new GalleryLocationDetailDto
        {
            LocationId = head.LocationId,
            AreaId = head.AreaId,
            AreaName = head.AreaName,
            LocationName = head.LocationName,
            Status = head.Status,
            CreatedAt = head.CreatedAt,
            UpdatedAt = head.UpdatedAt,
            HasGalleryItems = statuses.Count > 0,
            GalleryItemCount = statuses.Count,
            PublishedGalleryItemCount = statuses.Count(s => s == "PUBLISHED"),
            HiddenGalleryItemCount = statuses.Count(s => s == "HIDDEN"),
            Message = message,
        };
    }
}
