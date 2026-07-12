using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryLocation;

/// <summary>
/// UC-LOC-06 / UC-LOC-07 handler. Renames a location and/or moves it to another area in the same campus,
/// either an existing ACTIVE area or a brand-new area created in the same transaction. Never touches the
/// location's status or its gallery item. The location cover is replaced only when a new image is
/// supplied (otherwise kept); creating a new area always requires an area cover image.
/// </summary>
public sealed class UpdateGalleryLocationCommandHandler
    : IRequestHandler<UpdateGalleryLocationCommand, GalleryLocationDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IDateTimeService _clock;

    public UpdateGalleryLocationCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _clock = clock;
    }

    public async Task<GalleryLocationDetailDto> Handle(
        UpdateGalleryLocationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.VietnamNow;

        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        var locationName = GalleryKeyNormalizer.CleanName(request.LocationName);
        if (locationName.Length == 0)
            throw new BusinessRuleException("Vui lòng nhập vị trí cụ thể.", GalleryErrorCodes.LocationNameRequired);
        var locationKey = GalleryKeyNormalizer.ToKey(locationName);

        var location = await GalleryLocationWriteGuard.LoadLocationInCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        var oldAreaId = location.AreaId;
        var oldName = location.LocationName;

        // A new cover is optional on edit — kept when omitted (BR-LOCATION-COVER-04).
        ulong? newLocationCoverId = request.LocationCoverImage is null
            ? null
            : await GalleryCoverImage.UploadAsync(
                _fileUpload, request.LocationCoverImage, isArea: false, actorId, cancellationToken);

        if (mode == GalleryLocationModes.ExistingArea)
        {
            if (request.AreaId is not { } rawAreaId || rawAreaId <= 0)
                throw new BusinessRuleException("Vui lòng chọn khu vực/tòa.", GalleryErrorCodes.AreaRequired);

            var targetArea = await GalleryLocationWriteGuard.LoadAreaInCampusAsync(
                _db, (ulong)rawAreaId, campusId, cancellationToken);
            if (targetArea.Status != EntityStatuses.Active)
                throw new BusinessRuleException("Khu vực này đang ngừng hoạt động.", GalleryErrorCodes.AreaInactive);

            await GalleryLocationWriteGuard.EnsureLocationKeyFreeAsync(
                _db, targetArea.AreaId, locationKey, location.LocationId, cancellationToken);

            // Optionally replace the existing area's cover image too (kept when omitted). targetArea is
            // tracked by LoadAreaInCampusAsync, so the change is persisted by SaveChangesAsync below.
            if (request.AreaCoverImage is not null)
            {
                targetArea.CoverFileId = await GalleryCoverImage.UploadAsync(
                    _fileUpload, request.AreaCoverImage, isArea: true, actorId, cancellationToken);
                targetArea.UpdatedAt = now;
                targetArea.UpdatedBy = actorId;
            }

            location.AreaId = targetArea.AreaId;
            location.LocationName = locationName;
            location.LocationKey = locationKey;
            if (newLocationCoverId is { } lc) location.CoverFileId = lc;
            location.UpdatedAt = now;
            location.UpdatedBy = actorId;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (mode == GalleryLocationModes.NewArea)
        {
            var areaName = GalleryKeyNormalizer.CleanName(request.NewAreaName);
            if (areaName.Length == 0)
                throw new BusinessRuleException("Vui lòng nhập tên khu vực/tòa mới.", GalleryErrorCodes.NewAreaNameRequired);
            var areaKey = GalleryKeyNormalizer.ToKey(areaName);

            // Creating a new area during edit requires an area cover (BR-LOCATION-COVER-05).
            if (request.AreaCoverImage is null)
                throw new BusinessRuleException(
                    "Vui lòng upload ảnh đại diện khu vực.", GalleryErrorCodes.AreaCoverRequired);

            await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(_db, campusId, areaKey, cancellationToken);

            var areaCoverId = await GalleryCoverImage.UploadAsync(
                _fileUpload, request.AreaCoverImage, isArea: true, actorId, cancellationToken);

            // Create the area and move the location together (UC §21.4).
            await using var tx = await _db.BeginTransactionAsync(cancellationToken);
            try
            {
                await GalleryLocationWriteGuard.EnsureAreaKeyFreeAsync(_db, campusId, areaKey, cancellationToken);

                var area = new GalleryArea
                {
                    CampusId = campusId,
                    AreaName = areaName,
                    AreaKey = areaKey,
                    CoverFileId = areaCoverId,
                    Status = EntityStatuses.Active,
                    DisplayOrder = 0,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.GalleryAreas.Add(area);
                await _db.SaveChangesAsync(cancellationToken);

                location.AreaId = area.AreaId;
                location.LocationName = locationName;
                location.LocationKey = locationKey;
                if (newLocationCoverId is { } lc) location.CoverFileId = lc;
                location.UpdatedAt = now;
                location.UpdatedBy = actorId;
                await _db.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            throw new BusinessRuleException("Chế độ cập nhật không hợp lệ.", GalleryErrorCodes.InvalidMode);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "UPDATE_GALLERY_LOCATION",
            EntityType = "GalleryLocation",
            EntityId = location.LocationId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "LocationName", OldValueText = oldName, NewValueText = locationName },
                new AuditLogChange { FieldName = "AreaId", OldValueText = oldAreaId.ToString(), NewValueText = location.AreaId.ToString() },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return await GalleryLocationDetailBuilder.BuildAsync(
            _db, location.LocationId, cancellationToken, "Đã cập nhật vị trí.");
    }
}
