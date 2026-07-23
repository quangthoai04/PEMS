using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicLocationShowcase;

/// <summary>
/// Builds the Location Showcase payload. Effective public visibility mirrors the other public queries:
/// location/area/campus ACTIVE → gallery item PUBLISHED &amp; not deleted → at least one ACTIVE, non-deleted
/// media. Items are split by item_type (MEDIA vs VISIT_DELEGATION) and each is represented by its primary
/// media, falling back to the lowest display-order / media-id ACTIVE media when none is flagged primary.
/// Pomelo-safe: one flat item query + one flat media query, assembled in memory. Anonymous / read-only —
/// no admin/audit fields leave the server; media URLs go through the scoped public proxy.
/// </summary>
public sealed class GetPublicLocationShowcaseQueryHandler
    : IRequestHandler<GetPublicLocationShowcaseQuery, PublicLocationShowcaseDto?>
{
    private readonly IApplicationDbContext _db;

    public GetPublicLocationShowcaseQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicLocationShowcaseDto?> Handle(
        GetPublicLocationShowcaseQuery request, CancellationToken cancellationToken)
    {
        var locationId = (ulong)request.LocationId;

        // The location must be public-visible on its own (ACTIVE chain), else it "doesn't exist" publicly.
        var location = await _db.GalleryLocations.AsNoTracking()
            .Where(l =>
                l.LocationId == locationId &&
                l.Status == "ACTIVE" &&
                l.Area.Status == "ACTIVE" &&
                l.Area.Campus.Status == "ACTIVE")
            .Select(l => new
            {
                l.LocationId,
                l.LocationName,
                l.LocationNameEn,
                LocationTranslationStatus = l.TranslationStatus,
                AreaId = l.AreaId,
                AreaName = l.Area.AreaName,
                AreaNameEn = l.Area.AreaNameEn,
                AreaTranslationStatus = l.Area.TranslationStatus,
                CampusId = l.Area.CampusId,
                CampusCode = l.Area.Campus.CampusCode,
                CampusName = l.Area.Campus.Name,
                City = l.Area.Campus.City,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
            return null;

        // Public-visible MEDIA + VISIT_DELEGATION items for this location.
        var items = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.LocationId == locationId &&
                (i.ItemType == "MEDIA" || i.ItemType == "VISIT_DELEGATION") &&
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE" &&
                i.Media.Any(m => m.Status == "ACTIVE" && m.DeletedAt == null))
            .Select(i => new ItemRow
            {
                GalleryItemId = i.GalleryItemId,
                Title = i.Title,
                TitleEn = i.TitleEn,
                ItemTranslationStatus = i.TranslationStatus,
                ItemType = i.ItemType,
                MediaKind = i.MediaKind,
                DisplayOrder = i.DisplayOrder,
                CreatedAt = i.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var campus = new PublicCampusDto
        {
            CampusId = location.CampusId,
            CampusCode = location.CampusCode,
            CampusName = location.CampusName,
            City = location.City,
        };
        var area = new PublicGalleryAreaSummaryDto
        {
            AreaId = location.AreaId,
            AreaName = location.AreaName,
            AreaNameEn = PublicGalleryTranslation.EnOrNull(location.AreaTranslationStatus, location.AreaNameEn),
        };
        var loc = new PublicGalleryLocationSummaryDto
        {
            LocationId = location.LocationId,
            LocationName = location.LocationName,
            LocationNameEn = PublicGalleryTranslation.EnOrNull(
                location.LocationTranslationStatus, location.LocationNameEn),
        };

        if (items.Count == 0)
        {
            return new PublicLocationShowcaseDto
            {
                Campus = campus,
                Area = area,
                Location = loc,
                MediaItems = new List<PublicGalleryShowcaseItemDto>(),
                VisitDelegationItems = new List<PublicGalleryShowcaseItemDto>(),
            };
        }

        // Primary media (fallback lowest display-order / media-id) per item — flat query, grouped in memory.
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

        var primaryByItem = mediaRows
            .GroupBy(r => r.GalleryItemId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Media)
                      .OrderByDescending(m => m.IsPrimary)
                      .ThenBy(m => m.DisplayOrder)
                      .ThenBy(m => m.MediaId)
                      .First());

        var ordered = items
            // Add-order: earliest-added item first, latest last (by auto-increment id).
            .OrderBy(i => i.GalleryItemId)
            .Select(i => new PublicGalleryShowcaseItemDto
            {
                GalleryItemId = i.GalleryItemId,
                Title = i.Title,
                TitleEn = PublicGalleryTranslation.EnOrNull(i.ItemTranslationStatus, i.TitleEn),
                ItemType = i.ItemType,
                MediaKind = i.MediaKind,
                PrimaryMedia = primaryByItem.TryGetValue(i.GalleryItemId, out var pm) ? pm : null,
            })
            // An item without any active media wouldn't have passed the filter, but guard the in-memory join.
            .Where(x => x.PrimaryMedia is not null)
            .ToList();

        return new PublicLocationShowcaseDto
        {
            Campus = campus,
            Area = area,
            Location = loc,
            MediaItems = ordered.Where(x => x.ItemType == "MEDIA").ToList(),
            VisitDelegationItems = ordered.Where(x => x.ItemType == "VISIT_DELEGATION").ToList(),
        };
    }

    private sealed class ItemRow
    {
        public ulong GalleryItemId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? TitleEn { get; init; }
        public string? ItemTranslationStatus { get; init; }
        public string ItemType { get; init; } = string.Empty;
        public string MediaKind { get; init; } = string.Empty;
        public uint DisplayOrder { get; init; }
        public System.DateTime CreatedAt { get; init; }
    }
}
