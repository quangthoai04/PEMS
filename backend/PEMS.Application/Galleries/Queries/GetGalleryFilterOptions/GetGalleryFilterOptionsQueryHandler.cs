using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.GetGalleryFilterOptions;

/// <summary>
/// Loads all areas + locations of the caller's campus (both ACTIVE and INACTIVE, so filters stay
/// complete) ordered by display order. The upload modal restricts itself to ACTIVE ones client-side;
/// the Add/Edit handlers re-validate on the server (<see cref="GalleryLocationGuard"/>).
/// </summary>
public sealed class GetGalleryFilterOptionsQueryHandler
    : IRequestHandler<GetGalleryFilterOptionsQuery, GalleryFilterOptionsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetGalleryFilterOptionsQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GalleryFilterOptionsDto> Handle(
        GetGalleryFilterOptionsQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);

        var areas = await _db.GalleryAreas.AsNoTracking()
            .Where(a => a.CampusId == campusId)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.AreaName)
            .Select(a => new { a.AreaId, a.AreaName, a.Status, a.CoverFileId })
            .ToListAsync(cancellationToken);

        var areaIds = areas.Select(a => a.AreaId).ToList();

        // Cover media type (IMAGE vs VIDEO) — one flat file-metadata lookup for the areas' cover file ids.
        var coverIds = areas.Where(a => a.CoverFileId.HasValue).Select(a => a.CoverFileId!.Value).Distinct().ToList();
        var coverMediaByFileId = coverIds.Count == 0
            ? new Dictionary<ulong, (string? Purpose, string? Mime)>()
            : (await _db.Files.AsNoTracking()
                    .Where(f => coverIds.Contains(f.FileId))
                    .Select(f => new { f.FileId, f.FilePurpose, f.MimeType })
                    .ToListAsync(cancellationToken))
                .ToDictionary(f => f.FileId, f => (f.FilePurpose, f.MimeType));
        var locations = areaIds.Count == 0
            ? new List<LocationRow>()
            : await _db.GalleryLocations.AsNoTracking()
                .Where(l => areaIds.Contains(l.AreaId))
                .OrderBy(l => l.DisplayOrder).ThenBy(l => l.LocationName)
                .Select(l => new LocationRow
                {
                    AreaId = l.AreaId,
                    LocationId = l.LocationId,
                    LocationName = l.LocationName,
                    Status = l.Status,
                })
                .ToListAsync(cancellationToken);

        var locationsByArea = locations.GroupBy(l => l.AreaId).ToDictionary(g => g.Key, g => g.ToList());

        return new GalleryFilterOptionsDto
        {
            Areas = areas.Select(a => new GalleryAreaOptionDto
            {
                AreaId = a.AreaId,
                AreaName = a.AreaName,
                Status = a.Status,
                CoverFileId = a.CoverFileId,
                CoverUrl = GalleryFileUrls.ContentOrNull(a.CoverFileId),
                CoverMediaType = GalleryCoverMediaType.ResolveFor(a.CoverFileId, coverMediaByFileId),
                Locations = (locationsByArea.TryGetValue(a.AreaId, out var ls) ? ls : new List<LocationRow>())
                    .Select(l => new GalleryLocationOptionDto
                    {
                        LocationId = l.LocationId,
                        LocationName = l.LocationName,
                        Status = l.Status,
                    }).ToList(),
            }).ToList(),
        };
    }

    private sealed class LocationRow
    {
        public ulong AreaId { get; init; }
        public ulong LocationId { get; init; }
        public string LocationName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
