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
/// location's status or its gallery item — the item simply shows under the new area through the join.
/// </summary>
public sealed class UpdateGalleryLocationCommandHandler
    : IRequestHandler<UpdateGalleryLocationCommand, GalleryLocationDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public UpdateGalleryLocationCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<GalleryLocationDetailDto> Handle(
        UpdateGalleryLocationCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.UtcNow;

        var mode = (request.Mode ?? string.Empty).Trim().ToUpperInvariant();
        var locationName = GalleryKeyNormalizer.CleanName(request.LocationName);
        if (locationName.Length == 0)
            throw new BusinessRuleException("Vui lòng nhập vị trí cụ thể.", GalleryErrorCodes.LocationNameRequired);
        var locationKey = GalleryKeyNormalizer.ToKey(locationName);

        var location = await GalleryLocationWriteGuard.LoadLocationInCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        var oldAreaId = location.AreaId;
        var oldName = location.LocationName;

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

            location.AreaId = targetArea.AreaId;
            location.LocationName = locationName;
            location.LocationKey = locationKey;
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
