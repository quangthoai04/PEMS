using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Queries.ViewGalleryLocationList;

/// <summary>
/// Reads the campus-scoped location list (UC-LOC-01/02/03). Enforces the Staff Leader campus scope via
/// location → area → campus, applies keyword (area/location name + key, diacritic-insensitive), the
/// area and status filters, sorts and pages. The gallery-item indicator is loaded in a second flat
/// query and merged in memory to stay Pomelo-safe (no correlated subquery in the projection).
/// </summary>
public sealed class ViewGalleryLocationListQueryHandler
    : IRequestHandler<ViewGalleryLocationListQuery, PaginatedResult<GalleryLocationListItemDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ViewGalleryLocationListQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<GalleryLocationListItemDto>> Handle(
        ViewGalleryLocationListQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : (request.PageSize > 100 ? 100 : request.PageSize);

        // Campus scope: a location is in scope when its area belongs to the caller's campus.
        var query = _db.GalleryLocations.AsNoTracking()
            .Where(l => l.Area.CampusId == campusId);

        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword!.Trim();
        if (keyword is { Length: > 0 })
        {
            var lower = keyword.ToLower();
            var key = GalleryKeyNormalizer.ToKey(keyword);
            query = query.Where(l =>
                l.LocationName.ToLower().Contains(lower) ||
                l.Area.AreaName.ToLower().Contains(lower) ||
                (key != "" && (l.LocationKey.Contains(key) || l.Area.AreaKey.Contains(key))));
        }

        if (request.AreaId is { } areaId && areaId > 0)
            query = query.Where(l => l.AreaId == (ulong)areaId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var st = request.Status!.Trim().ToUpperInvariant();
            if (st is EntityStatuses.Active or EntityStatuses.Inactive)
                query = query.Where(l => l.Status == st);
        }

        // Only "createdAt" is a sortable column on this screen (UC §29.1); default DESC.
        var ascending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<GalleryLocation> ordered = ascending
            ? query.OrderBy(l => l.CreatedAt)
            : query.OrderByDescending(l => l.CreatedAt);
        var sortedQuery = ordered.ThenByDescending(l => l.LocationId);

        var totalItems = await query.CountAsync(cancellationToken);

        var rows = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new RowDto
            {
                LocationId = l.LocationId,
                AreaId = l.AreaId,
                AreaName = l.Area.AreaName,
                LocationName = l.LocationName,
                Status = l.Status,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var locationIds = rows.Select(r => r.LocationId).ToList();

        // The (single, non-deleted) gallery item per location — one flat query, merged in memory.
        var items = locationIds.Count == 0
            ? new List<ItemRow>()
            : await _db.GalleryItems.AsNoTracking()
                .Where(i => locationIds.Contains(i.LocationId) && i.DeletedAt == null)
                .Select(i => new ItemRow { LocationId = i.LocationId, GalleryItemId = i.GalleryItemId, Status = i.Status })
                .ToListAsync(cancellationToken);
        var itemByLocation = items
            .GroupBy(i => i.LocationId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = rows.Select(r =>
        {
            itemByLocation.TryGetValue(r.LocationId, out var gi);
            return new GalleryLocationListItemDto
            {
                LocationId = r.LocationId,
                AreaId = r.AreaId,
                AreaName = r.AreaName,
                LocationName = r.LocationName,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                HasGalleryItem = gi is not null,
                GalleryItemId = gi?.GalleryItemId,
                GalleryItemStatus = gi?.Status,
            };
        }).ToList();

        return PaginatedResult<GalleryLocationListItemDto>.Create(result, page, pageSize, totalItems);
    }

    private sealed class RowDto
    {
        public ulong LocationId { get; init; }
        public ulong AreaId { get; init; }
        public string AreaName { get; init; } = string.Empty;
        public string LocationName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    private sealed class ItemRow
    {
        public ulong LocationId { get; init; }
        public ulong GalleryItemId { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
