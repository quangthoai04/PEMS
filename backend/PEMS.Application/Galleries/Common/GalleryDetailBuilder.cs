using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Builds the full <see cref="GalleryItemDetailDto"/> for one gallery item — shared by UC-GAL-03
/// (Detail) and as the success payload of Add (UC-GAL-04) / Edit (UC-GAL-07). Returns only ACTIVE,
/// non-deleted media (AF-GAL-DETAIL: an item may legitimately have none). Callers are responsible for
/// the scope check; this only reads. Pomelo-safe (single-join projections + an in-memory name lookup).
/// </summary>
internal static class GalleryDetailBuilder
{
    public static async Task<GalleryItemDetailDto> BuildAsync(
        IApplicationDbContext db, ulong galleryItemId, CancellationToken ct, string? message = null)
    {
        var head = await db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == galleryItemId)
            .Select(i => new
            {
                i.GalleryItemId,
                i.Title,
                i.Description,
                i.ItemType,
                i.Status,
                i.MediaKind,
                AreaId = i.Location.AreaId,
                AreaName = i.Location.Area.AreaName,
                i.LocationId,
                LocationName = i.Location.LocationName,
                CampusId = i.Location.Area.CampusId,
                CampusCode = i.Location.Area.Campus.CampusCode,
                CampusName = i.Location.Area.Campus.Name,
                i.CreatedAt,
                i.CreatedBy,
                i.UpdatedAt,
                i.UpdatedBy,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("GalleryItem", galleryItemId);

        // Project raw media + file metadata, then map in memory (source-specific URLs can't be built in SQL).
        var mediaRows = await db.GalleryItemMedia.AsNoTracking()
            .Where(m => m.GalleryItemId == galleryItemId && m.DeletedAt == null && m.Status == "ACTIVE")
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.MediaId)
            .Select(m => new
            {
                m.MediaId,
                m.FileId,
                m.MediaType,
                m.ThumbnailFileId,
                m.IsPrimary,
                m.Caption,
                m.AltText,
                m.DisplayOrder,
                FilePurpose = m.File.FilePurpose,
                ExternalFileId = m.File.ExternalFileId,
                WebViewUrl = m.File.WebViewUrl,
                FileThumbnailUrl = m.File.ThumbnailUrl,
            })
            .ToListAsync(ct);

        var media = mediaRows.Select(m =>
        {
            var source = GalleryMediaSourceResolver.Resolve(m.FilePurpose, m.ExternalFileId, m.WebViewUrl);
            var isYouTube = source.SourceType == GalleryMediaSourceTypes.YouTube;
            return new GalleryMediaDto
            {
                MediaId = m.MediaId,
                FileId = m.FileId,
                MediaType = m.MediaType,
                SourceType = source.SourceType,
                // YouTube has no binary → no content endpoint; its thumbnail is the direct YouTube URL.
                FileUrl = isYouTube ? null : GalleryFileUrls.Content(m.FileId),
                ThumbnailUrl = isYouTube ? m.FileThumbnailUrl : GalleryFileUrls.ContentOrNull(m.ThumbnailFileId),
                YoutubeVideoId = source.YoutubeVideoId,
                EmbedUrl = source.EmbedUrl,
                WebViewUrl = source.WebViewUrl,
                IsPrimary = m.IsPrimary,
                Caption = m.Caption,
                AltText = m.AltText,
                DisplayOrder = m.DisplayOrder,
            };
        }).ToList();

        var userIds = new List<ulong>();
        if (head.CreatedBy.HasValue) userIds.Add(head.CreatedBy.Value);
        if (head.UpdatedBy.HasValue) userIds.Add(head.UpdatedBy.Value);
        var names = userIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        return new GalleryItemDetailDto
        {
            GalleryItemId = head.GalleryItemId,
            Title = head.Title,
            Description = head.Description,
            ItemType = head.ItemType,
            ItemTypeLabel = GalleryItemTypes.Label(head.ItemType),
            Status = head.Status,
            MediaKind = head.MediaKind,
            Area = new GalleryAreaRefDto { AreaId = head.AreaId, AreaName = head.AreaName },
            Location = new GalleryLocationRefDto { LocationId = head.LocationId, LocationName = head.LocationName },
            Campus = new GalleryCampusRefDto
            {
                CampusId = head.CampusId,
                CampusCode = head.CampusCode,
                CampusName = head.CampusName,
            },
            CreatedAt = head.CreatedAt,
            CreatedByName = head.CreatedBy.HasValue && names.TryGetValue(head.CreatedBy.Value, out var cn) ? cn : null,
            UpdatedAt = head.UpdatedAt,
            UpdatedByName = head.UpdatedBy.HasValue && names.TryGetValue(head.UpdatedBy.Value, out var un) ? un : null,
            Media = media,
            Message = message,
        };
    }
}
