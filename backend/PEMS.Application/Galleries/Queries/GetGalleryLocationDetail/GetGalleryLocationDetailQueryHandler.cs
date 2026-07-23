using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;

namespace PEMS.Application.Galleries.Queries.GetGalleryLocationDetail;

/// <summary>
/// Loads one location + its area for the edit modal. Campus scope enforced (403 cross-campus, 404
/// missing). Pomelo-safe: one joined head projection + one flat file-metadata lookup for the area cover.
/// </summary>
public sealed class GetGalleryLocationDetailQueryHandler
    : IRequestHandler<GetGalleryLocationDetailQuery, GalleryLocationEditDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetGalleryLocationDetailQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GalleryLocationEditDetailDto> Handle(
        GetGalleryLocationDetailQuery request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);

        var head = await _db.GalleryLocations.AsNoTracking()
            .Where(l => l.LocationId == (ulong)request.LocationId)
            .Select(l => new
            {
                l.LocationId,
                l.AreaId,
                AreaCampusId = l.Area.CampusId,
                l.Area.AreaName,
                l.Area.AreaNameEn,
                AreaTranslationStatus = l.Area.TranslationStatus,
                AreaCoverFileId = l.Area.CoverFileId,
                l.LocationName,
                l.LocationNameEn,
                LocationTranslationStatus = l.TranslationStatus,
                LocationCoverFileId = l.CoverFileId,
                l.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("GalleryLocation", request.LocationId);

        if (head.AreaCampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.LocationManageForbidden, "Bạn không có quyền thao tác vị trí này.", 403);

        // Area cover media type (IMAGE vs VIDEO) from the cover file's purpose/mime.
        var areaCoverMediaType = GalleryCoverMediaType.Image;
        if (head.AreaCoverFileId is { } coverId)
        {
            var cover = await _db.Files.AsNoTracking()
                .Where(f => f.FileId == coverId)
                .Select(f => new { f.FilePurpose, f.MimeType })
                .FirstOrDefaultAsync(cancellationToken);
            if (cover is not null)
                areaCoverMediaType = GalleryCoverMediaType.Resolve(cover.FilePurpose, cover.MimeType);
        }

        return new GalleryLocationEditDetailDto
        {
            LocationId = head.LocationId,
            AreaId = head.AreaId,
            AreaName = head.AreaName,
            AreaNameEn = head.AreaNameEn,
            AreaTranslationStatus = head.AreaTranslationStatus,
            AreaCoverFileId = head.AreaCoverFileId,
            AreaCoverUrl = GalleryFileUrls.ContentOrNull(head.AreaCoverFileId),
            AreaCoverMediaType = areaCoverMediaType,
            LocationName = head.LocationName,
            LocationNameEn = head.LocationNameEn,
            LocationTranslationStatus = head.LocationTranslationStatus,
            LocationCoverFileId = head.LocationCoverFileId,
            LocationCoverUrl = GalleryFileUrls.ContentOrNull(head.LocationCoverFileId),
            UpdatedAt = head.UpdatedAt,
        };
    }
}
