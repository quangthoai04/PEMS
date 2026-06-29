using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryLocationStatus;

/// <summary>
/// UC-LOC-08 / UC-LOC-09 handler. Flips <c>gallery_locations.status</c> within the caller's campus.
/// On disable, the location's gallery item is auto-hidden if it was PUBLISHED — both writes commit in one
/// transaction (UC §9.2 / §27.5). On enable, only the location flips; the item keeps its current status
/// (UC §9.3) so re-publishing stays an explicit action on the Quản lý Gallery screen. Idempotent no-op.
/// </summary>
public sealed class ChangeGalleryLocationStatusCommandHandler
    : IRequestHandler<ChangeGalleryLocationStatusCommand, GalleryLocationDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ChangeGalleryLocationStatusCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<GalleryLocationDetailDto> Handle(
        ChangeGalleryLocationStatusCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var now = _clock.UtcNow;

        var newStatus = (request.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (newStatus is not (EntityStatuses.Active or EntityStatuses.Inactive))
            throw new BusinessRuleException("Trạng thái không hợp lệ.", GalleryErrorCodes.InvalidStatus);

        var location = await GalleryLocationWriteGuard.LoadLocationInCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        if (location.Status == newStatus)
            return await GalleryLocationDetailBuilder.BuildAsync(
                _db, location.LocationId, cancellationToken, "Trạng thái không thay đổi.");

        await using var tx = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            location.Status = newStatus;
            location.UpdatedAt = now;
            location.UpdatedBy = actorId;

            string message;
            if (newStatus == EntityStatuses.Inactive)
            {
                // Auto-hide ALL currently-PUBLISHED gallery items of this location (a location may have
                // many items now). Items already HIDDEN keep their status (BR-LOC-DISABLE-02/03).
                var publishedItems = await _db.GalleryItems
                    .Where(i => i.LocationId == location.LocationId
                             && i.Status == "PUBLISHED"
                             && i.DeletedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var item in publishedItems)
                {
                    item.Status = "HIDDEN";
                    item.UpdatedAt = now;
                    item.UpdatedBy = actorId;
                }
                message = "Đã ngừng hoạt động vị trí.";
            }
            else
            {
                // Enable: location only — never auto-republish the item (BR-LOC-ENABLE-02/03).
                message = "Đã kích hoạt vị trí.";
            }

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = actorId,
                CampusId = campusId,
                Action = "CHANGE_GALLERY_LOCATION_STATUS",
                EntityType = "GalleryLocation",
                EntityId = location.LocationId,
                Changes = new List<AuditLogChange>
                {
                    new AuditLogChange
                    {
                        FieldName = "Status",
                        OldValueText = newStatus == EntityStatuses.Active ? EntityStatuses.Inactive : EntityStatuses.Active,
                        NewValueText = newStatus,
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return await GalleryLocationDetailBuilder.BuildAsync(
                _db, location.LocationId, cancellationToken, message);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
