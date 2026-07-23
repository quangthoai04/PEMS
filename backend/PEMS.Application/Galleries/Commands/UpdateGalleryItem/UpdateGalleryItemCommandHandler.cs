using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.UpdateGalleryItem;

/// <summary>
/// UC-GAL-07 handler. Enforces role/scope, validates the (possibly new) location, keeps both mandatory
/// descriptions, reconciles media (keep / soft-delete old, upload + append new), keeps or replaces each
/// audio recording independently (never removes one), guarantees ≥1 active media + exactly one primary,
/// recomputes <c>media_kind</c>, updates metadata (never the PUBLISHED/HIDDEN status) and writes an audit
/// log. New audio/files go through the shared Google Drive upload foundation. A newly uploaded audio is
/// only swapped in after commit; the replaced old audio is cleaned up post-commit (best-effort).
/// </summary>
public sealed class UpdateGalleryItemCommandHandler
    : IRequestHandler<UpdateGalleryItemCommand, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGalleryExternalMediaService _externalMedia;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IGalleryTranslationCoordinator _translator;
    private readonly IDateTimeService _clock;
    private readonly ILogger<UpdateGalleryItemCommandHandler> _logger;

    public UpdateGalleryItemCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGalleryExternalMediaService externalMedia,
        IGoogleDriveStorageService drive,
        IGalleryTranslationCoordinator translator,
        IDateTimeService clock,
        ILogger<UpdateGalleryItemCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _externalMedia = externalMedia;
        _drive = drive;
        _translator = translator;
        _clock = clock;
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
            .Include(i => i.Content)
            .FirstOrDefaultAsync(i => i.GalleryItemId == itemId && i.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException("GalleryItem", itemId);

        if (item.Location?.Area is null || item.Location.Area.CampusId != campusId)
            throw new AuthBusinessException(
                GalleryErrorCodes.GalleryScopeForbidden,
                "Bạn không có quyền chỉnh sửa gallery item này.", 403);

        var itemType = GalleryItemTypes.Normalize(request.ItemType);

        // Both descriptions are always required (validated before any upload → no orphans).
        var descriptionVi = GalleryContentRules.NormalizeDescription(request.DescriptionVi, vietnamese: true);
        var descriptionEn = GalleryContentRules.NormalizeDescription(request.DescriptionEn, vietnamese: false);

        // Audio: keep the current file unless a replacement is supplied. A supplied replacement must be a
        // valid non-empty upload. A legacy item with no content row must supply BOTH audio files.
        var hasNewAudioVi = request.NewAudioVi is { Content.Length: > 0, FileSize: > 0 };
        var hasNewAudioEn = request.NewAudioEn is { Content.Length: > 0, FileSize: > 0 };
        if (item.Content is null)
        {
            if (!hasNewAudioVi)
                throw new BusinessRuleException(
                    "Vui lòng chọn bản ghi âm tiếng Việt.", GalleryErrorCodes.AudioViRequired);
            if (!hasNewAudioEn)
                throw new BusinessRuleException(
                    "Vui lòng chọn bản ghi âm tiếng Anh.", GalleryErrorCodes.AudioEnRequired);
        }

        // New location must be ACTIVE and in the caller's campus (throws 404/403/422).
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        var youtubeUrls = request.YoutubeUrls ?? Array.Empty<string>();
        foreach (var url in youtubeUrls)
            YouTubeUrlParser.Parse(url);

        var now = _clock.VietnamNow;

        // Title translation decision (§15). A previewed (AUTO_PREVIEW + matching hash) or manual EN is
        // reused as-is — the provider is NOT called again; a stale preview throws PREVIEW_STALE here,
        // BEFORE any upload. With no usable client EN the provider runs ONLY when the normalized
        // Vietnamese title actually changed (BR §6.4) — media/audio/description/location/type edits
        // never hit the provider. Called before the upload block; the coordinator never throws for
        // provider errors, so a translation failure can never trip the compensation path — the update
        // still saves with EN = NULL / FAILED.
        var newTitle = TranslationSourceNormalizer.Normalize(request.Title);
        var titleChanged = !string.Equals(
            TranslationSourceNormalizer.Normalize(item.Title), newTitle, StringComparison.Ordinal);
        var resolvedTitle = GalleryPreviewedTranslation.TryResolve(
            newTitle, request.TitleEn, request.TitleTranslationOrigin,
            request.TitleTranslationSourceHash, GalleryTranslationLimits.TitleEnMaxLength);
        GalleryTranslationResult? titleTranslation = null;
        var titleSource = GalleryTranslationSources.Auto;
        if (resolvedTitle is not null)
        {
            // Apply only when something actually differs — an untouched VI + identical READY EN keeps
            // its stored metadata exactly (§15: "VI và EN không đổi → không sửa translation metadata").
            var enUnchanged = !titleChanged
                && item.TranslationStatus == GalleryTranslationStatuses.Ready
                && string.Equals(item.TitleEn, resolvedTitle.Result.TranslatedText, StringComparison.Ordinal);
            if (!enUnchanged)
            {
                titleTranslation = resolvedTitle.Result;
                titleSource = resolvedTitle.TranslationSource;
            }
        }
        else if (titleChanged)
        {
            var titleTranslations = await _translator.TranslateAsync(
                new[] { new GalleryTranslationRequest(newTitle, GalleryTranslationLimits.TitleEnMaxLength) },
                cancellationToken);
            titleTranslation = titleTranslations[0];
        }

        var keepSet = new HashSet<ulong>((request.KeepMediaIds ?? Array.Empty<long>()).Select(id => (ulong)id));

        var liveMedia = item.Media.Where(m => m.DeletedAt == null).ToList();
        var kept = liveMedia.Where(m => keepSet.Contains(m.MediaId)).ToList();

        var newFiles = request.NewFiles ?? Array.Empty<GalleryUploadFileCommandDto>();
        if (kept.Count + newFiles.Count + youtubeUrls.Count > 20)
            throw new BusinessRuleException(
                "Gallery item chỉ được có tối đa 20 media.", GalleryErrorCodes.TooManyFiles);

        // Every Drive object / files row created in this request — cleaned up if the DB write fails.
        var uploadedFileIds = new List<ulong>();
        // Old audio files replaced by a new upload — cleaned up AFTER a successful commit (never before).
        var replacedAudioFileIds = new List<ulong>();

        try
        {
            // Soft-delete media the user dropped (file stays on Drive).
            foreach (var m in liveMedia.Where(m => !keepSet.Contains(m.MediaId)))
            {
                m.Status = "HIDDEN";
                m.DeletedAt = now;
                m.DeletedBy = actorId;
                m.IsPrimary = false;
                m.UpdatedAt = now;
                m.UpdatedBy = actorId;
            }

            var appendedUploads = new List<GalleryItemMedia>();
            foreach (var file in newFiles)
            {
                var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType, itemType);
                await using var stream = new MemoryStream(file.Content, writable: false);
                var uploaded = await _fileUpload.UploadBusinessFileAsync(
                    stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, cancellationToken);
                uploadedFileIds.Add((ulong)uploaded.FileId);

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
                uploadedFileIds.Add((ulong)registered.FileId);
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

            var keptOrdered = kept.OrderBy(m => m.DisplayOrder).ThenBy(m => m.MediaId).ToList();
            var finalActive = keptOrdered
                .Concat(appendedUploads)
                .Concat(appendedYoutube)
                .ToList();

            if (finalActive.Count == 0)
                throw new BusinessRuleException(
                    "Gallery item phải có ít nhất một file media.", GalleryErrorCodes.MediaRequired);

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

            // Upload replacement audio (if any) and resolve the final audio file ids.
            ulong audioViFileId;
            if (hasNewAudioVi)
            {
                audioViFileId = await UploadAudioAsync(request.NewAudioVi!, vietnamese: true, actorId, cancellationToken);
                uploadedFileIds.Add(audioViFileId);
                if (item.Content is not null) replacedAudioFileIds.Add(item.Content.AudioViFileId);
            }
            else
            {
                audioViFileId = item.Content!.AudioViFileId;
            }

            ulong audioEnFileId;
            if (hasNewAudioEn)
            {
                audioEnFileId = await UploadAudioAsync(request.NewAudioEn!, vietnamese: false, actorId, cancellationToken);
                uploadedFileIds.Add(audioEnFileId);
                if (item.Content is not null) replacedAudioFileIds.Add(item.Content.AudioEnFileId);
            }
            else
            {
                audioEnFileId = item.Content!.AudioEnFileId;
            }

            var descriptionViChanged = item.Content?.DescriptionVi != descriptionVi;
            var descriptionEnChanged = item.Content?.DescriptionEn != descriptionEn;
            var oldAudioViFileId = item.Content?.AudioViFileId;
            var oldAudioEnFileId = item.Content?.AudioEnFileId;

            if (item.Content is null)
            {
                item.Content = new GalleryItemContent
                {
                    GalleryItemId = item.GalleryItemId,
                    DescriptionVi = descriptionVi,
                    AudioViFileId = audioViFileId,
                    DescriptionEn = descriptionEn,
                    AudioEnFileId = audioEnFileId,
                    CreatedAt = now,
                    CreatedBy = actorId,
                };
                _db.GalleryItemContents.Add(item.Content);
            }
            else
            {
                item.Content.DescriptionVi = descriptionVi;
                item.Content.AudioViFileId = audioViFileId;
                item.Content.DescriptionEn = descriptionEn;
                item.Content.AudioEnFileId = audioEnFileId;
                item.Content.UpdatedAt = now;
                item.Content.UpdatedBy = actorId;
            }

            item.Title = newTitle;
            // A changed title must never keep the old English translation (stale meaning) — apply the
            // fresh/previewed/manual result (READY) or clear EN + mark FAILED. An unchanged title with
            // no EN edit keeps ALL metadata.
            if (titleTranslation is not null)
                GalleryTranslationApplier.Apply(item, titleTranslation, now, titleSource);
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
                            titleChanged,
                            translationStatus = titleTranslation is not null ? item.TranslationStatus : null,
                            hasEnglishTitle = item.TitleEn != null,
                            descriptionViChanged, descriptionEnChanged,
                            oldAudioViFileId, newAudioViFileId = audioViFileId,
                            oldAudioEnFileId, newAudioEnFileId = audioEnFileId,
                            keptMedia = kept.Count, addedFiles = appendedUploads.Count, addedYoutube = appendedYoutube.Count,
                        }),
                    },
                },
                CreatedAt = now,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Compensation: remove every Drive object + files row created before the failure. The old
            // audio files are untouched (their FKs were never swapped because the commit failed).
            await GalleryFileCleanup.RemoveUploadedFilesAsync(_db, _drive, _logger, uploadedFileIds, cancellationToken);
            throw;
        }

        // Post-commit: drop the audio files that were replaced by a new upload (best-effort).
        await GalleryFileCleanup.RemoveUploadedFilesAsync(_db, _drive, _logger, replacedAudioFileIds, cancellationToken);

        return await GalleryDetailBuilder.BuildAsync(
            _db, item.GalleryItemId, cancellationToken, "Đã cập nhật Gallery Item.",
            titleTranslation is { Success: false } ? GalleryTranslationMessages.TranslationFailedWarning : null);
    }

    /// <summary>Uploads one bilingual audio recording, mapping upload-policy rejections to a language-specific code.</summary>
    private async Task<ulong> UploadAudioAsync(
        GalleryUploadFileCommandDto audio, bool vietnamese, ulong actorId, CancellationToken ct)
    {
        try
        {
            await using var stream = new MemoryStream(audio.Content, writable: false);
            var uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, audio.FileName, audio.ContentType ?? string.Empty, audio.FileSize,
                FilePurpose.GalleryAudio, (long)actorId, ct);
            return (ulong)uploaded.FileId;
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode == "FILE_TOO_LARGE")
        {
            throw new BusinessRuleException(
                "Bản ghi âm vượt quá dung lượng cho phép (tối đa 20 MB).", GalleryErrorCodes.AudioTooLarge);
        }
        catch (BusinessRuleException ex) when (ex.ErrorCode is "FILE_INVALID_EXTENSION" or "FILE_INVALID_TYPE"
                                                   or "FILE_MAGIC_BYTES_MISMATCH" or "FILE_EMPTY")
        {
            throw new BusinessRuleException(
                vietnamese ? "Bản ghi âm tiếng Việt không đúng định dạng (chỉ MP3/WAV)."
                           : "Bản ghi âm tiếng Anh không đúng định dạng (chỉ MP3/WAV).",
                vietnamese ? GalleryErrorCodes.AudioViInvalid : GalleryErrorCodes.AudioEnInvalid);
        }
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
