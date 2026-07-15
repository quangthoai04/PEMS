using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemDetail;

/// <summary>
/// Loads one public-visible gallery item (by id) plus ALL its ACTIVE media (primary first, then display
/// order). Enforces the effective-visibility chain (location/area/campus ACTIVE, item PUBLISHED &amp; not
/// deleted); if it fails or there is no active media the item is treated as non-existent → 404
/// (BR-PGAL-GRID-10/11). Anonymous / read-only — no admin/audit fields leave the server (BR-PGAL-GRID-12).
/// </summary>
public sealed class GetPublicGalleryItemDetailQueryHandler
    : IRequestHandler<GetPublicGalleryItemDetailQuery, PublicGalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicGalleryItemDetailQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicGalleryItemDetailDto> Handle(
        GetPublicGalleryItemDetailQuery request, CancellationToken cancellationToken)
    {
        var itemId = (ulong)request.GalleryItemId;

        var head = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.GalleryItemId == itemId &&
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE")
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
                i.Status,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("PublicGalleryItem", request.GalleryItemId);

        var mediaRows = await _db.GalleryItemMedia.AsNoTracking()
            .Where(m => m.GalleryItemId == head.GalleryItemId && m.Status == "ACTIVE" && m.DeletedAt == null)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.MediaId)
            .Select(m => new
            {
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

        var media = mediaRows.Select(m => PublicGalleryMediaFactory.Build(
            m.MediaId, m.FileId, m.MediaType, m.ThumbnailFileId, m.Caption, m.AltText, m.IsPrimary,
            (int)m.DisplayOrder, m.FilePurpose, m.ExternalFileId, m.WebViewUrl, m.FileThumbnailUrl)).ToList();

        // An item with zero active media is not public-visible (BR-PGAL-GRID-11).
        if (media.Count == 0)
            throw new NotFoundException("PublicGalleryItem", request.GalleryItemId);

        return new PublicGalleryItemDetailDto
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
            GalleryItem = new PublicGalleryItemSummaryDto
            {
                GalleryItemId = head.GalleryItemId,
                Title = head.Title,
                Description = head.Description,
                MediaKind = head.MediaKind,
                Status = head.Status,
            },
            Media = media,
        };
    }
}
