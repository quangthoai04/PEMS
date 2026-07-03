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

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 handler. Validates role/scope, the target location (must be ACTIVE and in the caller's
/// campus), and the files; uploads each file to Google Drive via the shared <see cref="IFileUploadService"/>
/// (image → <see cref="FilePurpose.GalleryImage"/>, video → <see cref="FilePurpose.GalleryVideo"/>);
/// creates the <c>gallery_items</c> row plus one <c>gallery_item_media</c> row per file (first = primary),
/// derives <c>media_kind</c> from the files, and writes an audit log.
/// </summary>
public sealed class AddGalleryItemCommandHandler
    : IRequestHandler<AddGalleryItemCommand, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IDateTimeService _clock;

    public AddGalleryItemCommandHandler(
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
        AddGalleryItemCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;

        var files = request.Files ?? Array.Empty<GalleryUploadFileCommandDto>();
        if (files.Count == 0)
            throw new BusinessRuleException("Vui lòng chọn ít nhất một tệp media.", GalleryErrorCodes.FilesRequired);
        if (files.Count > 20)
            throw new BusinessRuleException("Chỉ được tải lên tối đa 20 tệp.", GalleryErrorCodes.TooManyFiles);

        var status = NormalizeStatus(request.Status);
        var itemType = GalleryItemTypes.Normalize(request.ItemType);

        // Location must exist, be ACTIVE and belong to the caller's campus (throws 404/403/422).
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        // A location may hold 0, 1 or many gallery items — no per-location uniqueness check.

        var title = Regex.Replace(request.Title?.Trim() ?? string.Empty, @"\s+", " ");
        var description = request.Description?.Trim() ?? string.Empty;

        // Upload every file first (each commits its own files row + Drive object); classify image vs video.
        var uploads = new List<(UploadedFileDto Uploaded, string MediaType, string? Caption, string? AltText)>();
        foreach (var file in files)
        {
            var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType);
            await using var stream = new MemoryStream(file.Content, writable: false);
            var uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, cancellationToken);
            uploads.Add((uploaded, mediaType, file.Caption, file.AltText));
        }

        var mediaKind = GalleryMediaClassifier.ResolveMediaKind(uploads.Select(u => u.MediaType));
        var now = _clock.UtcNow;

        var item = new GalleryItem
        {
            LocationId = (ulong)request.LocationId,
            Title = title,
            Description = description,
            ItemType = itemType,
            MediaKind = mediaKind,
            Status = status,
            DisplayOrder = 0,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        uint order = 1;
        foreach (var u in uploads)
        {
            item.Media.Add(new GalleryItemMedia
            {
                FileId = (ulong)u.Uploaded.FileId,
                MediaType = u.MediaType,
                Caption = u.Caption,
                AltText = u.AltText,
                IsPrimary = order == 1,
                DisplayOrder = order,
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = actorId,
            });
            order++;
        }

        _db.GalleryItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            CampusId = campusId,
            Action = "CREATE_GALLERY_ITEM",
            EntityType = "GalleryItem",
            EntityId = item.GalleryItemId,
            Changes = new List<AuditLogChange>
            {
                new AuditLogChange
                {
                    FieldName = "GalleryItem",
                    NewValueText = JsonSerializer.Serialize(new
                    {
                        title, locationId = request.LocationId, itemType, status, mediaKind, mediaCount = uploads.Count,
                    }),
                },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return await GalleryDetailBuilder.BuildAsync(
            _db, item.GalleryItemId, cancellationToken, "Đã thêm gallery item mới.");
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "PUBLISHED";
        var s = status.Trim().ToUpperInvariant();
        if (s is not ("PUBLISHED" or "HIDDEN"))
            throw new BusinessRuleException("Trạng thái không hợp lệ.", GalleryErrorCodes.InvalidStatus);
        return s;
    }
}
