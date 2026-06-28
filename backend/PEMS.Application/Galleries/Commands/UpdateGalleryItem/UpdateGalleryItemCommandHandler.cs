using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// UC-GAL-07 handler. Enforces role/scope, validates the (possibly new) location, reconciles media
/// (keep / soft-delete old, upload + append new), guarantees the item keeps ≥1 active media and exactly
/// one primary, recomputes <c>media_kind</c>, updates metadata (never the PUBLISHED/HIDDEN status), and
/// writes an audit log. New files go through the shared Google Drive upload foundation.
/// </summary>
public sealed class UpdateGalleryItemCommandHandler
    : IRequestHandler<UpdateGalleryItemCommand, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IDateTimeService _clock;

    public UpdateGalleryItemCommandHandler(
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

    public async Task<GalleryItemDetailDto> Handle(
        UpdateGalleryItemCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;
        var itemId = (ulong)request.GalleryItemId;

        var item = await _db.GalleryItems
            .Include(i => i.Location).ThenInclude(l => l.Area)
            .Include(i => i.Media)
            .FirstOrDefaultAsync(i => i.GalleryItemId == itemId && i.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("GalleryItem", itemId);

        if (item.Location?.Area is null || item.Location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền chỉnh sửa gallery item này.", 403);

        // New location must be ACTIVE and in the caller's campus (BR-GAL-EDIT-02/03; throws 404/403/422).
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        // One gallery item per location: the chosen location must not belong to a DIFFERENT item.
        var locationTaken = await _db.GalleryItems.AnyAsync(
            i => i.LocationId == (ulong)request.LocationId && i.GalleryItemId != itemId && i.DeletedAt == null,
            cancellationToken);
        if (locationTaken)
            throw new ConflictException(
                "Vị trí này đã có gallery item khác. Mỗi vị trí chỉ được dùng cho một gallery item.",
                GalleryErrorCodes.LocationAlreadyUsed);

        var now = _clock.UtcNow;
        var keepSet = new HashSet<ulong>((request.KeepMediaIds ?? Array.Empty<long>()).Select(id => (ulong)id));

        var liveMedia = item.Media.Where(m => m.DeletedAt == null).ToList();
        var kept = liveMedia.Where(m => keepSet.Contains(m.MediaId)).ToList();

        // Soft-delete media the user dropped (file stays on Drive — BR-GAL-DISABLE rules apply).
        foreach (var m in liveMedia.Where(m => !keepSet.Contains(m.MediaId)))
        {
            m.Status = "HIDDEN";
            m.DeletedAt = now;
            m.DeletedBy = actorId;
            m.IsPrimary = false;
            m.UpdatedAt = now;
            m.UpdatedBy = actorId;
        }

        // Upload + append new files. Total media (kept + new) is capped at 20 — checked BEFORE
        // uploading so a rejected request never orphans a Drive object.
        var newFiles = request.NewFiles ?? Array.Empty<GalleryUploadFileCommandDto>();
        if (kept.Count + newFiles.Count > 20)
            throw new BusinessRuleException(
                "Gallery item chỉ được có tối đa 20 tệp media.", GalleryErrorCodes.TooManyFiles);

        var appended = new List<GalleryItemMedia>();
        foreach (var file in newFiles)
        {
            var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType);
            await using var stream = new MemoryStream(file.Content, writable: false);
            var uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, cancellationToken);

            var media = new GalleryItemMedia
            {
                GalleryItemId = item.GalleryItemId,
                FileId = (ulong)uploaded.FileId,
                MediaType = mediaType,
                Caption = file.Caption,
                AltText = file.AltText,
                IsPrimary = false,
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = actorId,
            };
            item.Media.Add(media);
            appended.Add(media);
        }

        // Final active media set, kept first (by original order) then newly appended.
        var finalActive = kept
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.MediaId)
            .Concat(appended)
            .ToList();

        if (finalActive.Count == 0)
            throw new BusinessRuleException(
                "Gallery item phải có ít nhất một file media.", GalleryErrorCodes.MediaRequired);

        // Resolve the single primary (BR-GAL-EDIT-07). primaryMediaId may only reference a KEPT media
        // (new files have no id yet); otherwise default to the first active media.
        GalleryItemMedia primary;
        if (request.PrimaryMediaId is { } pmId && pmId > 0)
        {
            primary = kept.FirstOrDefault(m => m.MediaId == (ulong)pmId)
                ?? throw new BusinessRuleException(
                    "Media chính được chọn không thuộc gallery item này.", GalleryErrorCodes.PrimaryMediaInvalid);
        }
        else
        {
            primary = finalActive[0];
        }

        uint order = 1;
        foreach (var m in finalActive)
        {
            m.IsPrimary = ReferenceEquals(m, primary);
            m.DisplayOrder = order++;
            if (kept.Contains(m))
            {
                m.UpdatedAt = now;
                m.UpdatedBy = actorId;
            }
        }

        var mediaKind = GalleryMediaClassifier.ResolveMediaKind(finalActive.Select(m => m.MediaType));

        item.Title = Regex.Replace(request.Title?.Trim() ?? string.Empty, @"\s+", " ");
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.LocationId = (ulong)request.LocationId;
        item.MediaKind = mediaKind;
        item.UpdatedAt = now;
        item.UpdatedBy = actorId;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "UPDATE_GALLERY_ITEM",
            EntityType = "GalleryItem",
            EntityId = item.GalleryItemId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "GalleryItem",
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        title = item.Title, locationId = request.LocationId, mediaKind,
                        keptMedia = kept.Count, addedMedia = appended.Count,
                    }),
                },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await GalleryDetailBuilder.BuildAsync(
            _db, item.GalleryItemId, cancellationToken, "Đã cập nhật gallery item.");
    }
}
