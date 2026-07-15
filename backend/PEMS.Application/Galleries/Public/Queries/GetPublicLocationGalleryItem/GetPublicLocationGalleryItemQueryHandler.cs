using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicLocationGalleryItem;

/// <summary>
/// Loads the album grid of a location: ALL public-visible gallery items (location/area/campus ACTIVE,
/// item PUBLISHED &amp; not deleted, ≥1 active media), each represented by its primary media — falling
/// back to the lowest display-order / media-id ACTIVE media when no media is flagged primary
/// (BR-PGAL-GRID-04). Items ordered by display order, then newest (UC §8.1). 404 if none qualify
/// (BR-PGAL-GRID-11). Anonymous / read-only — no admin/audit fields leave the server (BR-PGAL-GRID-12).
/// Pomelo-safe: one flat item query + one flat media query, assembled in memory.
/// </summary>
public sealed class GetPublicLocationGalleryItemsQueryHandler
    : IRequestHandler<GetPublicLocationGalleryItemsQuery, PublicLocationGalleryGridDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicLocationGalleryItemsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicLocationGalleryGridDto> Handle(
        GetPublicLocationGalleryItemsQuery request, CancellationToken cancellationToken)
    {
        var locationId = (ulong)request.LocationId;

        var items = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.LocationId == locationId &&
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE" &&
                i.Media.Any(m => m.Status == "ACTIVE" && m.DeletedAt == null))
            .OrderBy(i => i.DisplayOrder)
            .ThenByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.GalleryItemId)
            .Select(i => new
            {
                CampusId = i.Location.Area.CampusId,
                CampusCode = i.Location.Area.Campus.CampusCode,
                CampusName = i.Location.Area.Campus.Name,
                City = i.Location.Area.Campus.City,
                AreaId = i.Location.AreaId,
                AreaName = i.Location.Area.AreaName,
                LocationId = i.LocationId,
                LocationName = i.Location.LocationName,
                i.GalleryItemId,
                i.Title,
                i.Description,
                i.MediaKind,
            })
            .ToListAsync(cancellationToken);

        // No public-visible item for this location → treat as non-existent (BR-PGAL-GRID-11).
        if (items.Count == 0)
            throw new NotFoundException("PublicLocationGallery", request.LocationId);

        var itemIds = items.Select(i => i.GalleryItemId).ToList();
        var mediaRaw = await _db.GalleryItemMedia.AsNoTracking()
            .Where(m => itemIds.Contains(m.GalleryItemId) && m.Status == "ACTIVE" && m.DeletedAt == null)
            .Select(m => new
            {
                m.GalleryItemId,
                m.MediaId,
                m.FileId,
                m.MediaType,
                m.ThumbnailFileId,
                m.Caption,
                m.AltText,
                m.IsPrimary,
                m.DisplayOrder,
                FilePurpose = m.File.FilePurpose,
                ExternalFileId = m.File.ExternalFileId,
                WebViewUrl = m.File.WebViewUrl,
                FileThumbnailUrl = m.File.ThumbnailUrl,
            })
            .ToListAsync(cancellationToken);

        var mediaRows = mediaRaw.Select(m => new
        {
            m.GalleryItemId,
            Media = PublicGalleryMediaFactory.Build(
                m.MediaId, m.FileId, m.MediaType, m.ThumbnailFileId, m.Caption, m.AltText, m.IsPrimary,
                (int)m.DisplayOrder, m.FilePurpose, m.ExternalFileId, m.WebViewUrl, m.FileThumbnailUrl),
        }).ToList();

        // Primary media per item, fallback to lowest display-order / media-id when none flagged (BR-PGAL-GRID-04).
        var primaryByItem = mediaRows
            .GroupBy(r => r.GalleryItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Media)
                      .OrderByDescending(m => m.IsPrimary)
                      .ThenBy(m => m.DisplayOrder)
                      .ThenBy(m => m.MediaId)
                      .First());

        var head = items[0];

        var gridItems = items
            .Select(i => new PublicGalleryGridItemDto
            {
                GalleryItemId = i.GalleryItemId,
                Title = i.Title,
                DescriptionPreview = BuildPreview(i.Description),
                MediaKind = i.MediaKind,
                PrimaryMedia = primaryByItem.TryGetValue(i.GalleryItemId, out var pm) ? pm : null,
            })
            // An item left without media after the in-memory join would not have passed the filter; guard.
            .Where(g => g.PrimaryMedia is not null)
            .ToList();

        if (gridItems.Count == 0)
            throw new NotFoundException("PublicLocationGallery", request.LocationId);

        return new PublicLocationGalleryGridDto
        {
            Campus = new PublicCampusDto
            {
                CampusId = head.CampusId,
                CampusCode = head.CampusCode,
                CampusName = head.CampusName,
                City = head.City,
            },
            Area = new PublicGalleryAreaSummaryDto { AreaId = head.AreaId, AreaName = head.AreaName },
            Location = new PublicGalleryLocationSummaryDto { LocationId = head.LocationId, LocationName = head.LocationName },
            Items = gridItems,
        };
    }

    /// <summary>A short, single-line preview of the description for the grid card.</summary>
    private static string BuildPreview(string? description)
    {
        var text = (description ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        const int max = 120;
        return text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "…";
    }
}
