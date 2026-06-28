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
/// Loads the public-visible gallery item for a location plus its ACTIVE media (primary first, then
/// display order). Enforces the same effective-visibility chain as the navigation query; if it fails
/// (location/area/campus inactive, item hidden/deleted, or no active media) the item is treated as
/// non-existent → 404 (BR-PGAL-22). Anonymous / read-only — no admin or audit fields leave the server.
/// </summary>
public sealed class GetPublicLocationGalleryItemQueryHandler
    : IRequestHandler<GetPublicLocationGalleryItemQuery, PublicGalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicLocationGalleryItemQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicGalleryItemDetailDto> Handle(
        GetPublicLocationGalleryItemQuery request, CancellationToken cancellationToken)
    {
        var locationId = (ulong)request.LocationId;

        var head = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.LocationId == locationId &&
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
            ?? throw new NotFoundException("PublicGalleryItem", request.LocationId);

        var media = await _db.GalleryItemMedia.AsNoTracking()
            .Where(m => m.GalleryItemId == head.GalleryItemId && m.Status == "ACTIVE" && m.DeletedAt == null)
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.DisplayOrder)
            .ThenBy(m => m.MediaId)
            .Select(m => new PublicGalleryMediaDto
            {
                MediaId = m.MediaId,
                FileId = m.FileId,
                MediaType = m.MediaType,
                Url = PublicGalleryFileUrls.Content(m.FileId),
                ThumbnailUrl = m.ThumbnailFileId != null
                    ? PublicGalleryFileUrls.Content(m.ThumbnailFileId.Value)
                    : null,
                Caption = m.Caption,
                AltText = m.AltText,
                IsPrimary = m.IsPrimary,
                DisplayOrder = (int)m.DisplayOrder,
            })
            .ToListAsync(cancellationToken);

        // An item with zero active media would not have passed the navigation filter, but guard anyway.
        if (media.Count == 0)
            throw new NotFoundException("PublicGalleryItem", request.LocationId);

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
