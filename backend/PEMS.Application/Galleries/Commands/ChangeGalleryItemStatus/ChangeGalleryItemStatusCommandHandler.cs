using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.ChangeGalleryItemStatus;

/// <summary>
/// UC-GAL-05 / UC-GAL-06 handler. Enforces role/scope, then flips only <c>gallery_items.status</c>.
/// Enabling (→ PUBLISHED) requires at least one active media (BR-GAL-ENABLE-04). The toggle never
/// touches area / location / media status (BR-GAL-ENABLE-01/02, BR-GAL-DISABLE-02/03/04). Idempotent
/// no-op, and audits real changes.
/// </summary>
public sealed class ChangeGalleryItemStatusCommandHandler
    : IRequestHandler<ChangeGalleryItemStatusCommand, ChangeGalleryItemStatusResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ChangeGalleryItemStatusCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ChangeGalleryItemStatusResponse> Handle(
        ChangeGalleryItemStatusCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var itemId = (ulong)request.GalleryItemId;
        var newStatus = request.Status.Trim().ToUpperInvariant();

        if (newStatus is not ("PUBLISHED" or "HIDDEN"))
            throw new BusinessRuleException("Trạng thái không hợp lệ.", GalleryErrorCodes.InvalidStatus);

        var item = await _db.GalleryItems
            .Include(i => i.Location).ThenInclude(l => l.Area)
            .FirstOrDefaultAsync(i => i.GalleryItemId == itemId && i.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("GalleryItem", itemId);

        if (item.Location?.Area is null || item.Location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền thay đổi trạng thái gallery item này.", 403);

        if (item.Status == newStatus)
            return new ChangeGalleryItemStatusResponse
            {
                GalleryItemId = item.GalleryItemId,
                Status = item.Status,
                Message = "Trạng thái không thay đổi.",
            };

        // Enabling requires at least one active media (BR-GAL-ENABLE-04).
        if (newStatus == "PUBLISHED")
        {
            var hasActiveMedia = await _db.GalleryItemMedia.AnyAsync(
                m => m.GalleryItemId == itemId && m.DeletedAt == null && m.Status == "ACTIVE", cancellationToken);
            if (!hasActiveMedia)
                throw new BusinessRuleException(
                    "Không thể hiển thị gallery item khi chưa có media khả dụng.", GalleryErrorCodes.NoActiveMedia);
        }

        var oldStatus = item.Status;
        var now = _clock.UtcNow;
        item.Status = newStatus;
        item.UpdatedAt = now;
        item.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "CHANGE_GALLERY_ITEM_STATUS",
            EntityType = "GalleryItem",
            EntityId = item.GalleryItemId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange { FieldName = "Status", OldValueText = oldStatus, NewValueText = newStatus },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ChangeGalleryItemStatusResponse
        {
            GalleryItemId = item.GalleryItemId,
            Status = item.Status,
            Message = newStatus == "PUBLISHED" ? "Đã hiển thị gallery item." : "Đã ẩn gallery item.",
        };
    }
}
