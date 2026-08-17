using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.DeleteGalleryItem;

/// <summary>
/// "Xóa nội dung Gallery" handler. Enforces the Staff Leader campus scope, then SOFT-deletes the item
/// and its media rows (<c>deleted_at</c>/<c>deleted_by</c>). Every management and public query already
/// filters <c>DeletedAt == null</c>, so the item disappears from the list, search, detail, filters and
/// VisitFPTU in one write — while the row (and the audit trail pointing at it) survives.
///
/// Google Drive binaries are deliberately NOT removed: Drive is outside the MySQL transaction, the DB
/// keeps referencing the file rows, and a later retention/purge job (or a restore) can still use them.
/// A second delete of the same item is a controlled 409, never a silent no-op or a 500.
/// </summary>
public sealed class DeleteGalleryItemCommandHandler
    : IRequestHandler<DeleteGalleryItemCommand, DeleteGalleryItemResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public DeleteGalleryItemCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DeleteGalleryItemResponse> Handle(
        DeleteGalleryItemCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var itemId = (ulong)request.GalleryItemId;

        // Loaded WITHOUT the soft-delete filter so a repeat delete is answered deterministically
        // (409 ALREADY_DELETED) instead of being indistinguishable from a wrong id.
        var item = await _db.GalleryItems
            .Include(i => i.Location).ThenInclude(l => l.Area)
            .FirstOrDefaultAsync(i => i.GalleryItemId == itemId, cancellationToken)
            ?? throw new NotFoundException("GalleryItem", itemId);

        // Campus scope is checked BEFORE the already-deleted branch: a cross-campus caller must never
        // learn whether the item exists in another campus.
        if (item.Location?.Area is null || item.Location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền xóa gallery item này.", 403);

        if (item.DeletedAt is not null)
            throw new ConflictException(
                "Nội dung Gallery này đã được xóa trước đó.", GalleryErrorCodes.GalleryItemAlreadyDeleted);

        var now = _clock.VietnamNow;

        item.DeletedAt = now;
        item.DeletedBy = actorId;
        item.UpdatedAt = now;
        item.UpdatedBy = actorId;

        // Media follows the item (same convention as dropping a media during an edit): soft-deleted and
        // demoted, so nothing keeps serving it and no stale primary remains. The Drive object stays.
        var media = await _db.GalleryItemMedia
            .Where(m => m.GalleryItemId == itemId && m.DeletedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var m in media)
        {
            m.Status = "HIDDEN";
            m.DeletedAt = now;
            m.DeletedBy = actorId;
            m.IsPrimary = false;
            m.UpdatedAt = now;
            m.UpdatedBy = actorId;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "DELETE_GALLERY_ITEM",
            EntityType = "GalleryItem",
            EntityId = item.GalleryItemId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "GalleryItem",
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        galleryItemId = item.GalleryItemId,
                        title = item.Title,
                        locationId = item.LocationId,
                        areaId = item.Location!.AreaId,
                        mediaSoftDeleted = media.Count,
                        deletedAt = now,
                        deletedBy = actorId,
                    }),
                },
                new AuditLogChange { FieldName = "DeletedAt", NewValueText = now.ToString("O") },
            },
            CreatedAt = now,
        });

        // Item + media + audit commit atomically (one SaveChanges = one DB transaction).
        await _db.SaveChangesAsync(cancellationToken);

        return new DeleteGalleryItemResponse
        {
            GalleryItemId = item.GalleryItemId,
            Message = "Xóa nội dung Gallery thành công.",
        };
    }
}
