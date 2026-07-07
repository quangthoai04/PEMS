using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Tts;
using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Single source of truth for the Staff Leader gallery list read model used by UC-GAL-01 (View List)
/// and UC-GAL-02 (Search &amp; Filter).
///
/// Enforces the Staff Leader campus scope (via location → area → campus), applies keyword
/// (title / description / area name / location name), area / location / mediaKind / status filters,
/// sorts (whitelist), pages, and projects to <see cref="GalleryItemListItemDto"/>. The primary-media
/// thumbnail and creator name are resolved in a second pass (in-memory) to stay Pomelo-safe — no
/// correlated subqueries in the projection. Read-only — always <c>AsNoTracking()</c>.
/// </summary>
internal static class GalleryItemListQueryExecutor
{
    private static readonly string[] AllowedSortColumns = { "createdat", "title", "status" };

    private static readonly string[] AllowedAudioStatuses =
    {
        TtsManagementStatuses.Ready, TtsManagementStatuses.Processing, TtsManagementStatuses.Failed,
        TtsManagementStatuses.Stale, TtsManagementStatuses.NotCreated, TtsManagementStatuses.Disabled,
        TtsManagementStatuses.InvalidDescription,
    };

    public static async Task<PaginatedResult<GalleryItemListItemDto>> ExecuteAsync(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IGalleryItemTtsService tts,
        IGalleryItemListCriteria request,
        CancellationToken ct)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy)
            ? "createdat"
            : request.SortBy!.Trim().ToLowerInvariant();
        if (!AllowedSortColumns.Contains(sortBy))
            sortBy = "createdat";
        // Default sort is created_at DESC (BR-GAL-SEARCH-04); only explicit "asc" flips it.
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        // Campus scope: an item is in scope when its location's area belongs to the caller's campus.
        var query = db.GalleryItems.AsNoTracking()
            .Where(i => i.DeletedAt == null && i.Location.Area.CampusId == campusId);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim().ToLower();
        if (keyword is { Length: > 0 })
        {
            query = query.Where(i =>
                i.Title.ToLower().Contains(keyword) ||
                i.Description.ToLower().Contains(keyword) ||
                i.Location.Area.AreaName.ToLower().Contains(keyword) ||
                i.Location.LocationName.ToLower().Contains(keyword));
        }

        if (request.AreaId is { } areaId && areaId > 0)
            query = query.Where(i => i.Location.AreaId == (ulong)areaId);

        if (request.LocationId is { } locationId && locationId > 0)
            query = query.Where(i => i.LocationId == (ulong)locationId);

        if (!string.IsNullOrWhiteSpace(request.MediaKind))
        {
            var mk = request.MediaKind!.Trim().ToUpperInvariant();
            if (mk is "IMAGE" or "VIDEO" or "MIXED")
                query = query.Where(i => i.MediaKind == mk);
        }

        if (!string.IsNullOrWhiteSpace(request.ItemType))
        {
            var it = request.ItemType!.Trim().ToUpperInvariant();
            if (it is GalleryItemTypes.Media or GalleryItemTypes.VisitDelegation)
                query = query.Where(i => i.ItemType == it);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status!.Trim().ToUpperInvariant();
            if (st is "PUBLISHED" or "HIDDEN")
                query = query.Where(i => i.Status == st);
        }

        IOrderedQueryable<GalleryItem> ordered = (sortBy, ascending) switch
        {
            ("title", true) => query.OrderBy(i => i.Title),
            ("title", false) => query.OrderByDescending(i => i.Title),
            ("status", true) => query.OrderBy(i => i.Status),
            ("status", false) => query.OrderByDescending(i => i.Status),
            ("createdat", true) => query.OrderBy(i => i.CreatedAt),
            _ => query.OrderByDescending(i => i.CreatedAt),
        };
        var sortedQuery = ordered.ThenByDescending(i => i.GalleryItemId);

        var projected = sortedQuery.Select(i => new GalleryRow
        {
            GalleryItemId = i.GalleryItemId,
            AreaId = i.Location.AreaId,
            AreaName = i.Location.Area.AreaName,
            LocationId = i.LocationId,
            LocationName = i.Location.LocationName,
            Title = i.Title,
            Description = i.Description,
            ItemType = i.ItemType,
            MediaKind = i.MediaKind,
            Status = i.Status,
            CreatedAt = i.CreatedAt,
            CreatedBy = i.CreatedBy,
        });

        // Audio (narration) status is a computed value (per-item hash + TTS-row state machine), so it
        // can't be expressed in SQL. When no audio filter is active we keep the efficient SQL paging and
        // only compute the status for the returned page. When an audio filter IS active we must compute
        // over the whole filtered set first, then page the matches in memory — the SQL sort is preserved.
        var audioFilter = string.IsNullOrWhiteSpace(request.AudioStatus)
            ? null
            : request.AudioStatus!.Trim().ToUpperInvariant();
        if (audioFilter is not null && !AllowedAudioStatuses.Contains(audioFilter))
            audioFilter = null;

        List<GalleryRow> rows;
        int totalItems;
        IReadOnlyDictionary<long, GalleryItemTtsManagementStatus> statusByItem;

        if (audioFilter is null)
        {
            totalItems = await query.CountAsync(ct);
            rows = await projected.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            statusByItem = await tts.GetManagementStatusesAsync(Descriptors(rows), ct);
        }
        else
        {
            var allRows = await projected.ToListAsync(ct);
            statusByItem = await tts.GetManagementStatusesAsync(Descriptors(allRows), ct);
            var matched = allRows
                .Where(r => statusByItem.TryGetValue((long)r.GalleryItemId, out var s) && s.Status == audioFilter)
                .ToList();
            totalItems = matched.Count;
            rows = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        var itemIds = rows.Select(r => r.GalleryItemId).ToList();

        // Primary media (is_primary, not deleted) for the page's items — one flat query, mapped in memory.
        var primaryMedia = itemIds.Count == 0
            ? new List<MediaRow>()
            : await db.GalleryItemMedia.AsNoTracking()
                .Where(m => itemIds.Contains(m.GalleryItemId) && m.IsPrimary && m.DeletedAt == null)
                .Select(m => new MediaRow
                {
                    GalleryItemId = m.GalleryItemId,
                    MediaId = m.MediaId,
                    FileId = m.FileId,
                    MediaType = m.MediaType,
                    ThumbnailFileId = m.ThumbnailFileId,
                })
                .ToListAsync(ct);
        var primaryByItem = primaryMedia
            .GroupBy(m => m.GalleryItemId)
            .ToDictionary(g => g.Key, g => g.First());

        // Creator names for the page's items.
        var creatorIds = rows.Where(r => r.CreatedBy.HasValue).Select(r => r.CreatedBy!.Value).Distinct().ToList();
        var creatorNames = creatorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await db.Users.AsNoTracking()
                .Where(u => creatorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        var items = rows.Select(r => new GalleryItemListItemDto
        {
            GalleryItemId = r.GalleryItemId,
            AreaId = r.AreaId,
            AreaName = r.AreaName,
            LocationId = r.LocationId,
            LocationName = r.LocationName,
            Title = r.Title,
            Description = r.Description,
            ItemType = r.ItemType,
            ItemTypeLabel = GalleryItemTypes.Label(r.ItemType),
            MediaKind = r.MediaKind,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            CreatedByName = r.CreatedBy.HasValue && creatorNames.TryGetValue(r.CreatedBy.Value, out var n) ? n : null,
            PrimaryMedia = primaryByItem.TryGetValue(r.GalleryItemId, out var pm)
                ? new GalleryPrimaryMediaDto
                {
                    MediaId = pm.MediaId,
                    FileId = pm.FileId,
                    MediaType = pm.MediaType,
                    FileUrl = GalleryFileUrls.Content(pm.FileId),
                    ThumbnailUrl = GalleryFileUrls.ContentOrNull(pm.ThumbnailFileId),
                }
                : null,
            AudioStatus = statusByItem.TryGetValue((long)r.GalleryItemId, out var st)
                ? st.Status
                : TtsManagementStatuses.NotCreated,
        }).ToList();

        return PaginatedResult<GalleryItemListItemDto>.Create(items, page, pageSize, totalItems);
    }

    /// <summary>Projects the page/candidate rows into the batch narration-status lookup input.</summary>
    private static IReadOnlyCollection<GalleryTtsItemDescriptor> Descriptors(IEnumerable<GalleryRow> rows)
        => rows.Select(r => new GalleryTtsItemDescriptor((long)r.GalleryItemId, r.Description)).ToList();

    private sealed class GalleryRow
    {
        public ulong GalleryItemId { get; init; }
        public ulong AreaId { get; init; }
        public string AreaName { get; init; } = string.Empty;
        public ulong LocationId { get; init; }
        public string LocationName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public string MediaKind { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public ulong? CreatedBy { get; init; }
    }

    private sealed class MediaRow
    {
        public ulong GalleryItemId { get; init; }
        public ulong MediaId { get; init; }
        public ulong FileId { get; init; }
        public string MediaType { get; init; } = string.Empty;
        public ulong? ThumbnailFileId { get; init; }
    }
}
