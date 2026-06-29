using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicCampusNavigation;

/// <summary>
/// Builds the public navigation tree for a campus. Effective public visibility (UC §5, BR-PGAL-04..12):
/// campus ACTIVE → area ACTIVE → location ACTIVE → gallery item PUBLISHED &amp; not deleted → at least one
/// ACTIVE, non-deleted media. Areas/locations without a public-visible item are omitted entirely
/// (BR-PGAL-09/16.2). Pomelo-safe: one flat visible-item projection + a second flat primary-media query,
/// assembled in memory (no projection subqueries). Anonymous / read-only.
/// </summary>
public sealed class GetPublicCampusNavigationQueryHandler
    : IRequestHandler<GetPublicCampusNavigationQuery, PublicGalleryNavigationDto?>
{
    private readonly IApplicationDbContext _db;

    public GetPublicCampusNavigationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicGalleryNavigationDto?> Handle(
        GetPublicCampusNavigationQuery request, CancellationToken cancellationToken)
    {
        var code = (request.CampusCode ?? string.Empty).Trim();
        if (code.Length == 0)
            return null;

        var campus = await _db.Campuses.AsNoTracking()
            .Where(c => c.CampusCode == code && c.Status == "ACTIVE")
            .Select(c => new PublicCampusDto
            {
                CampusId = c.CampusId,
                CampusCode = c.CampusCode,
                CampusName = c.Name,
                City = c.City,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (campus is null)
            return null;

        // Gallery items that are publicly visible (a location may carry MANY items now, BR-PGAL-07).
        var rows = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.CampusId == campus.CampusId &&
                i.Media.Any(m => m.Status == "ACTIVE" && m.DeletedAt == null))
            .Select(i => new NavRow
            {
                AreaId = i.Location.AreaId,
                AreaName = i.Location.Area.AreaName,
                AreaDisplayOrder = i.Location.Area.DisplayOrder,
                LocationId = i.LocationId,
                LocationName = i.Location.LocationName,
                LocationDisplayOrder = i.Location.DisplayOrder,
                GalleryItemId = i.GalleryItemId,
                ItemDisplayOrder = i.DisplayOrder,
                CreatedAt = i.CreatedAt,
                Title = i.Title,
                MediaKind = i.MediaKind,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return new PublicGalleryNavigationDto { Campus = campus, Areas = new List<PublicGalleryAreaDto>() };

        // Primary media (or, if none flagged, the lowest display-order active media) per item — BR-PGAL-16.
        var itemIds = rows.Select(r => r.GalleryItemId).Distinct().ToList();
        var mediaRows = await _db.GalleryItemMedia.AsNoTracking()
            .Where(m => itemIds.Contains(m.GalleryItemId) && m.Status == "ACTIVE" && m.DeletedAt == null)
            .Select(m => new MediaRow
            {
                GalleryItemId = m.GalleryItemId,
                FileId = m.FileId,
                IsPrimary = m.IsPrimary,
                DisplayOrder = m.DisplayOrder,
                MediaId = m.MediaId,
            })
            .ToListAsync(cancellationToken);

        var primaryByItem = mediaRows
            .GroupBy(m => m.GalleryItemId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(m => m.IsPrimary)
                      .ThenBy(m => m.DisplayOrder)
                      .ThenBy(m => m.MediaId)
                      .First());

        var areas = rows
            .GroupBy(r => new { r.AreaId, r.AreaName, r.AreaDisplayOrder })
            .OrderBy(g => g.Key.AreaDisplayOrder)
            .ThenBy(g => g.Key.AreaName)
            .Select(g => new PublicGalleryAreaDto
            {
                AreaId = g.Key.AreaId,
                AreaName = g.Key.AreaName,
                DisplayOrder = (int)g.Key.AreaDisplayOrder,
                // A location may hold many visible items — collapse to one nav node per location,
                // represented by its lead item (lowest item display order, then newest), and carry the
                // total count so the UI can hint at a slider. The full list loads on location click.
                Locations = g
                    .GroupBy(r => new { r.LocationId, r.LocationName, r.LocationDisplayOrder })
                    .OrderBy(lg => lg.Key.LocationDisplayOrder)
                    .ThenBy(lg => lg.Key.LocationName)
                    .Select(lg =>
                    {
                        var lead = lg
                            .OrderBy(r => r.ItemDisplayOrder)
                            .ThenByDescending(r => r.CreatedAt)
                            .ThenByDescending(r => r.GalleryItemId)
                            .First();
                        return new PublicGalleryLocationDto
                        {
                            LocationId = lg.Key.LocationId,
                            LocationName = lg.Key.LocationName,
                            DisplayOrder = (int)lg.Key.LocationDisplayOrder,
                            GalleryItemId = lead.GalleryItemId,
                            Title = lead.Title,
                            MediaKind = lead.MediaKind,
                            PublicGalleryItemCount = lg.Count(),
                            PrimaryMediaUrl = primaryByItem.TryGetValue(lead.GalleryItemId, out var pm)
                                ? PublicGalleryFileUrls.Content(pm.FileId)
                                : null,
                        };
                    })
                    .ToList(),
            })
            .ToList();

        return new PublicGalleryNavigationDto { Campus = campus, Areas = areas };
    }

    private sealed class NavRow
    {
        public ulong AreaId { get; init; }
        public string AreaName { get; init; } = string.Empty;
        public uint AreaDisplayOrder { get; init; }
        public ulong LocationId { get; init; }
        public string LocationName { get; init; } = string.Empty;
        public uint LocationDisplayOrder { get; init; }
        public ulong GalleryItemId { get; init; }
        public uint ItemDisplayOrder { get; init; }
        public System.DateTime CreatedAt { get; init; }
        public string Title { get; init; } = string.Empty;
        public string MediaKind { get; init; } = string.Empty;
    }

    private sealed class MediaRow
    {
        public ulong GalleryItemId { get; init; }
        public ulong FileId { get; init; }
        public bool IsPrimary { get; init; }
        public uint DisplayOrder { get; init; }
        public ulong MediaId { get; init; }
    }
}
