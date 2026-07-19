using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Galleries.Commands.AddGalleryItem;

/// <summary>
/// UC-GAL-04 handler. Validates role/scope, the four mandatory bilingual fields (descriptionVi +
/// audioVi + descriptionEn + audioEn), the target location and the gallery media; uploads the two audio
/// recordings and every gallery image to Google Drive via the shared <see cref="IFileUploadService"/>
/// (audio → <see cref="FilePurpose.GalleryAudio"/>); creates the <c>gallery_items</c> row, its 1:1
/// <c>gallery_item_contents</c> row and one <c>gallery_item_media</c> row per media; and writes an audit
/// log. On any DB failure the already-uploaded Drive objects are cleaned up (compensation).
/// </summary>
public sealed class AddGalleryItemCommandHandler
    : IRequestHandler<AddGalleryItemCommand, GalleryItemDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IGalleryExternalMediaService _externalMedia;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IDateTimeService _clock;
    private readonly ILogger<AddGalleryItemCommandHandler> _logger;

    public AddGalleryItemCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IGalleryExternalMediaService externalMedia,
        IGoogleDriveStorageService drive,
        IDateTimeService clock,
        ILogger<AddGalleryItemCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _externalMedia = externalMedia;
        _drive = drive;
        _clock = clock;
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
        // file is uploaded or any files row is written — no orphaned Drive object / files row.
        foreach (var url in youtubeUrls)
            YouTubeUrlParser.Parse(url);

        // Bilingual content — all four fields are mandatory. Descriptions + audio presence are validated
        // BEFORE any upload so a rejected request never orphans a Drive object.
        var descriptionVi = GalleryContentRules.NormalizeDescription(request.DescriptionVi, vietnamese: true);
        var descriptionEn = GalleryContentRules.NormalizeDescription(request.DescriptionEn, vietnamese: false);
        var audioVi = GalleryContentRules.RequireAudio(request.AudioVi, vietnamese: true);
        var audioEn = GalleryContentRules.RequireAudio(request.AudioEn, vietnamese: false);

        var status = NormalizeStatus(request.Status);
        var itemType = GalleryItemTypes.Normalize(request.ItemType);

        // Location must exist, be ACTIVE and belong to the caller's campus (throws 404/403/422).
        await GalleryLocationGuard.LoadActiveLocationInCurrentCampusAsync(
            _db, (ulong)request.LocationId, campusId, cancellationToken);

        var title = Regex.Replace(request.Title?.Trim() ?? string.Empty, @"\s+", " ");

        // Track every Drive-backed / metadata files row created in this request so we can compensate
        // (delete Drive object + files row) if a later DB step fails.
        var uploadedFileIds = new List<ulong>();
        var media = new List<MediaToCreate>(files.Count + youtubeUrls.Count);

        try
        {
            // Upload the two audio recordings first (each is a GALLERY_AUDIO Drive file).
            var audioViFileId = await UploadAudioAsync(audioVi, vietnamese: true, actorId, cancellationToken);
            uploadedFileIds.Add(audioViFileId);
            var audioEnFileId = await UploadAudioAsync(audioEn, vietnamese: false, actorId, cancellationToken);
            uploadedFileIds.Add(audioEnFileId);

            // Upload every gallery image; classify image vs video (videos are rejected by the classifier).
            foreach (var file in files)
            {
                var (mediaType, purpose) = GalleryMediaClassifier.Classify(file.FileName, file.ContentType, itemType);
                await using var stream = new MemoryStream(file.Content, writable: false);
                var uploaded = await _fileUpload.UploadBusinessFileAsync(
                    stream, file.FileName, file.ContentType ?? string.Empty, file.FileSize, purpose, (long)actorId, cancellationToken);
                uploadedFileIds.Add((ulong)uploaded.FileId);
                media.Add(new MediaToCreate((ulong)uploaded.FileId, mediaType, file.Caption, file.AltText));
            }

            // Register each YouTube URL as a metadata-only files row (no Drive upload). YouTube media is VIDEO.
            foreach (var url in youtubeUrls)
            {
                var registered = await _externalMedia.RegisterYouTubeAsync(url, (long)actorId, cancellationToken);
                uploadedFileIds.Add((ulong)registered.FileId);
                media.Add(new MediaToCreate((ulong)registered.FileId, GalleryMediaClassifier.Video, null, null));
            }

            var primaryIndex = ResolvePrimaryIndex(request.PrimaryMediaKey, files.Count, youtubeUrls.Count);
            var mediaKind = GalleryMediaClassifier.ResolveMediaKind(media.Select(m => m.MediaType));
            var now = _clock.VietnamNow;

            var item = new GalleryItem
            {
                LocationId = (ulong)request.LocationId,
                Title = title,
                ItemType = itemType,
                MediaKind = mediaKind,
                Status = status,
                DisplayOrder = 0,
                CreatedAt = now,
                CreatedBy = actorId,
                Content = new GalleryItemContent
                {
                    DescriptionVi = descriptionVi,
                    AudioViFileId = audioViFileId,
                    DescriptionEn = descriptionEn,
                    AudioEnFileId = audioEnFileId,
                    CreatedAt = now,
                    CreatedBy = actorId,
                },
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
                            galleryItemId = item.GalleryItemId,
                            title, locationId = request.LocationId, itemType, status, mediaKind,
                            hasVietnameseContent = true, hasEnglishContent = true,
                            audioViFileId, audioEnFileId,
                            mediaCount = media.Count, uploadCount = files.Count, youtubeCount = youtubeUrls.Count,
                        }),
                    },
                },
                CreatedAt = now,
            });
            await _db.SaveChangesAsync(cancellationToken);

            return await GalleryDetailBuilder.BuildAsync(
                _db, item.GalleryItemId, cancellationToken, "Đã tạo Gallery Item với đầy đủ nội dung song ngữ.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Compensation: remove every Drive object + files row created before the failure.
            await GalleryFileCleanup.RemoveUploadedFilesAsync(_db, _drive, _logger, uploadedFileIds, cancellationToken);
            throw;
        }
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
