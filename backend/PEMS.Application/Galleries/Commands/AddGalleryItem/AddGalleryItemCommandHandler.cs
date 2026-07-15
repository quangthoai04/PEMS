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

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 handler. Validates role/scope, the target location (must be ACTIVE and in the caller's
/// campus), and the files; uploads each file to Google Drive via the shared <see cref="IFileUploadService"/>
/// (MEDIA → <see cref="FilePurpose.GalleryItemImage"/>/<see cref="FilePurpose.GalleryItemVideo"/>,
/// VISIT_DELEGATION → <see cref="FilePurpose.GalleryDelegationImage"/>/<see cref="FilePurpose.GalleryDelegationVideo"/>);
/// creates the <c>gallery_items</c> row plus one <c>gallery_item_media</c> row per file (first = primary),
/// derives <c>media_kind</c> from the files, and writes an audit log.
/// </summary>
public sealed class AddGalleryItemCommandHandler
    : IRequestHandler<AddGalleryItemCommand, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGalleryExternalMediaService _externalMedia;
    private readonly IDateTimeService _clock;
    private readonly IGalleryItemTtsService _tts;
    private readonly ILogger<AddGalleryItemCommandHandler> _logger;

    public AddGalleryItemCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGalleryExternalMediaService externalMedia,
        IDateTimeService clock,
        IGalleryItemTtsService tts,
        ILogger<AddGalleryItemCommandHandler> logger)
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
        AddGalleryItemCommand request, CancellationToken cancellationToken)
    {
        var campusId = StaffLeaderGalleryScope.EnsureStaffLeaderCampus(_currentUser);
        var actorId = _currentUser.UserId!.Value;

        var files = request.Files ?? Array.Empty<GalleryUploadFileCommandDto>();
        var youtubeUrls = request.YoutubeUrls ?? Array.Empty<string>();
        if (files.Count + youtubeUrls.Count == 0)
            throw new BusinessRuleException(
                "Vui lòng chọn ít nhất một tệp media hoặc thêm một video YouTube.", GalleryErrorCodes.FilesRequired);
        if (files.Count + youtubeUrls.Count > 20)
            throw new BusinessRuleException(
                "Chỉ được tối đa 20 media (tệp + video YouTube).", GalleryErrorCodes.TooManyFiles);

        // Validate every YouTube URL up front (pure parse) so a bad URL rejects the whole request BEFORE any
        // file is uploaded or any files row is written — no orphaned Drive object / files row (AC-YT-03).
        foreach (var url in youtubeUrls)
            YouTubeUrlParser.Parse(url);

        var status = NormalizeStatus(request.Status);
        var itemType = GalleryItemTypes.Normalize(request.ItemType);

        // Location must exist, be ACTIVE and belong to the caller's campus (throws 404/403/422).
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        // A location may hold 0, 1 or many gallery items — no per-location uniqueness check.

        var title = Regex.Replace(request.Title?.Trim() ?? string.Empty, @"\s+", " ");
        var description = request.Description?.Trim() ?? string.Empty;

        // Narration cap (EverAI TTS): checked BEFORE any upload so a rejected request never orphans
        // a Drive object. 422 per the TTS spec.
        if (description.Length > 1000)
            throw new BusinessRuleException(
                "Mô tả không được vượt quá 1000 ký tự.", GalleryErrorCodes.DescriptionTooLong);

        // Build the media list in a stable order — uploads first, then YouTube — so display order and the
        // primaryMediaKey (upload:{i} / youtube:{i}) line up with what the client sent.
        var media = new List<MediaToCreate>(files.Count + youtubeUrls.Count);

        // Upload every file first (each commits its own files row + Drive object); classify image vs video.
        foreach (var file in files)
        {
            var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType, itemType);
            await using var stream = new MemoryStream(file.Content, writable: false);
            var uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, cancellationToken);
            media.Add(new MediaToCreate((ulong)uploaded.FileId, mediaType, file.Caption, file.AltText));
        }

        // Register each YouTube URL as a metadata-only files row (no Drive upload, no download). YouTube
        // media is always VIDEO for media_kind purposes.
        foreach (var url in youtubeUrls)
        {
            var registered = await _externalMedia.RegisterYouTubeAsync(url, (long)actorId, cancellationToken);
            media.Add(new MediaToCreate((ulong)registered.FileId, GalleryMediaClassifier.Video, null, null));
        }

        var primaryIndex = ResolvePrimaryIndex(request.PrimaryMediaKey, files.Count, youtubeUrls.Count);
        var mediaKind = GalleryMediaClassifier.ResolveMediaKind(media.Select(m => m.MediaType));
        var now = _clock.VietnamNow;

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
        for (var i = 0; i < media.Count; i++)
        {
            var m = media[i];
            item.Media.Add(new GalleryItemMedia
            {
                FileId = m.FileId,
                MediaType = m.MediaType,
                Caption = m.Caption,
                AltText = m.AltText,
                IsPrimary = i == primaryIndex,
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
                        title, locationId = request.LocationId, itemType, status, mediaKind,
                        mediaCount = media.Count, uploadCount = files.Count, youtubeCount = youtubeUrls.Count,
                    }),
                },
            },
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        // Fire-and-forget narration job (AUTO_GENERATE). The item is already saved — a TTS problem
        // (disabled config, EverAI down, running-job race) must never fail the create itself.
        try
        {
            await _tts.EnsureAudioAsync(
                (long)item.GalleryItemId, TtsTriggerSources.AutoGenerate, (long)actorId,
                requirePublicVisible: false, bypassFailedCooldown: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TTS auto-generate failed after creating gallery item {GalleryItemId}.",
                item.GalleryItemId);
        }

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

    /// <summary>
    /// Maps a primaryMediaKey (<c>upload:{i}</c> / <c>youtube:{i}</c>) to an index into the combined
    /// media list [uploads..., youtube...]. Anything unrecognised falls back to the first media (0).
    /// </summary>
    private static int ResolvePrimaryIndex(string? key, int uploadCount, int youtubeCount)
    {
        if (string.IsNullOrWhiteSpace(key)) return 0;
        var parts = key.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var idx) || idx < 0) return 0;

        return parts[0].ToLowerInvariant() switch
        {
            "upload" when idx < uploadCount => idx,
            "youtube" when idx < youtubeCount => uploadCount + idx,
            _ => 0,
        };
    }

    /// <summary>One media row to create (an uploaded file or a registered YouTube reference).</summary>
    private readonly record struct MediaToCreate(ulong FileId, string MediaType, string? Caption, string? AltText);
}
