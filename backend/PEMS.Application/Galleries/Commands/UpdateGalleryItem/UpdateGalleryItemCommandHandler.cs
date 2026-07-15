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
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Tts;
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
    private readonly IGalleryExternalMediaService _externalMedia;
    private readonly IDateTimeService _clock;
    private readonly IGalleryItemTtsService _tts;
    private readonly ILogger<UpdateGalleryItemCommandHandler> _logger;

    public UpdateGalleryItemCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGalleryExternalMediaService externalMedia,
        IDateTimeService clock,
        IGalleryItemTtsService tts,
        ILogger<UpdateGalleryItemCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _externalMedia = externalMedia;
        _clock = clock;
        _tts = tts;
        _logger = logger;
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

        var itemType = GalleryItemTypes.Normalize(request.ItemType);

        // Narration cap (EverAI TTS): checked BEFORE any upload so a rejected request never orphans
        // a Drive object. 422 per the TTS spec.
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length > 1000)
            throw new BusinessRuleException(
                "Mô tả không được vượt quá 1000 ký tự.", GalleryErrorCodes.DescriptionTooLong);

        // New location must be ACTIVE and in the caller's campus (BR-GAL-EDIT-02/03; throws 404/403/422).
        // A location may hold many items, so moving an item into an already-used location is allowed.
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        var youtubeUrls = request.YoutubeUrls ?? Array.Empty<string>();
        // Validate every new YouTube URL up front (pure parse) so a bad URL rejects the edit BEFORE any
        // upload / files row / soft-delete — no orphans, no half-applied edit (AC-YT-03).
        foreach (var url in youtubeUrls)
            YouTubeUrlParser.Parse(url);

        var now = _clock.VietnamNow;
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

        // Upload + append new files, then register + append new YouTube media. Total media
        // (kept + new files + new YouTube) is capped at 20 — checked BEFORE any upload so a rejected
        // request never orphans a Drive object / files row.
        var newFiles = request.NewFiles ?? Array.Empty<GalleryUploadFileCommandDto>();
        if (kept.Count + newFiles.Count + youtubeUrls.Count > 20)
            throw new BusinessRuleException(
                "Gallery item chỉ được có tối đa 20 media.", GalleryErrorCodes.TooManyFiles);

        var appendedUploads = new List<GalleryItemMedia>();
        foreach (var file in newFiles)
        {
            var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType, itemType);
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
            appendedUploads.Add(media);
        }

        var appendedYoutube = new List<GalleryItemMedia>();
        foreach (var url in youtubeUrls)
        {
            var registered = await _externalMedia.RegisterYouTubeAsync(url, (long)actorId, cancellationToken);
            var media = new GalleryItemMedia
            {
                GalleryItemId = item.GalleryItemId,
                FileId = (ulong)registered.FileId,
                MediaType = GalleryMediaClassifier.Video,
                IsPrimary = false,
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = actorId,
            };
            item.Media.Add(media);
            appendedYoutube.Add(media);
        }

        // Final active media set: kept first (by original order), then new uploads, then new YouTube —
        // this order matches the upload:{i} / youtube:{i} primaryMediaKey indices.
        var keptOrdered = kept.OrderBy(m => m.DisplayOrder).ThenBy(m => m.MediaId).ToList();
        var finalActive = keptOrdered
            .Concat(appendedUploads)
            .Concat(appendedYoutube)
            .ToList();

        if (finalActive.Count == 0)
            throw new BusinessRuleException(
                "Gallery item phải có ít nhất một file media.", GalleryErrorCodes.MediaRequired);

        // Resolve the single primary (BR-GAL-EDIT-07). primaryMediaKey supports existing/upload/youtube;
        // it falls back to the legacy primaryMediaId (a kept media) and finally to the first active media.
        var primary = ResolvePrimary(
            request.PrimaryMediaKey, request.PrimaryMediaId, keptOrdered, appendedUploads, appendedYoutube)
            ?? finalActive[0];

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
        item.Description = description;
        item.LocationId = (ulong)request.LocationId;
        item.ItemType = itemType;
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
                        title = item.Title, locationId = request.LocationId, itemType, mediaKind,
                        keptMedia = kept.Count, addedFiles = appendedUploads.Count, addedYoutube = appendedYoutube.Count,
                    }),
                },
            },
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);

        // Fire-and-forget narration job (AUTO_GENERATE): an edited description changes the TTS hash,
        // so this queues a fresh generation unless a matching READY/running one already exists. A TTS
        // problem must never fail the edit itself.
        try
        {
            await _tts.EnsureAudioAsync(
                (long)item.GalleryItemId, TtsTriggerSources.AutoGenerate, (long)actorId,
                requirePublicVisible: false, bypassFailedCooldown: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TTS auto-generate failed after updating gallery item {GalleryItemId}.",
                item.GalleryItemId);
        }

        return await GalleryDetailBuilder.BuildAsync(
            _db, item.GalleryItemId, cancellationToken, "Đã cập nhật gallery item.");
    }

    /// <summary>
    /// Resolves the chosen primary media from primaryMediaKey (<c>existing:{mediaId}</c> /
    /// <c>upload:{index}</c> / <c>youtube:{index}</c>), falling back to the legacy primaryMediaId (a kept
    /// media). Returns null when neither is supplied (caller defaults to the first active media). Throws
    /// 422 when a key/id is supplied but does not resolve to a media in this edit.
    /// </summary>
    private static GalleryItemMedia? ResolvePrimary(
        string? primaryMediaKey,
        long? primaryMediaId,
        IReadOnlyList<GalleryItemMedia> kept,
        IReadOnlyList<GalleryItemMedia> appendedUploads,
        IReadOnlyList<GalleryItemMedia> appendedYoutube)
    {
        if (!string.IsNullOrWhiteSpace(primaryMediaKey))
        {
            var parts = primaryMediaKey.Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[1], out var n) && n >= 0)
            {
                switch (parts[0].ToLowerInvariant())
                {
                    case "existing":
                        return kept.FirstOrDefault(m => m.MediaId == (ulong)n)
                            ?? throw Invalid();
                    case "upload":
                        return n < appendedUploads.Count ? appendedUploads[(int)n] : throw Invalid();
                    case "youtube":
                        return n < appendedYoutube.Count ? appendedYoutube[(int)n] : throw Invalid();
                }
            }
            throw Invalid();
        }

        if (primaryMediaId is { } pmId && pmId > 0)
        {
            return kept.FirstOrDefault(m => m.MediaId == (ulong)pmId) ?? throw Invalid();
        }

        return null;

        static BusinessRuleException Invalid() => new(
            "Media chính được chọn không thuộc gallery item này.", GalleryErrorCodes.PrimaryMediaInvalid);
    }
}
