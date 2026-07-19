using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Common;
using PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMediaStream;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryItemAudio;

/// <summary>
/// Serves one gallery item's bilingual narration audio to the anonymous public page. Resolves the audio
/// file from the item + language (never a client-supplied fileId), enforces the public-visibility chain,
/// verifies the file is a GALLERY_AUDIO file, then streams the bytes (Drive range-aware). Any failure —
/// unknown item, hidden item, inactive location/area/campus, missing content, wrong purpose, invalid
/// language — is a controlled 404 so no internal state leaks.
/// </summary>
public sealed class GetPublicGalleryItemAudioQueryHandler
    : IRequestHandler<GetPublicGalleryItemAudioQuery, PublicGalleryMediaStreamResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetPublicGalleryItemAudioQueryHandler(
        IApplicationDbContext db, IFileStorageService storage, IGoogleDriveStorageService drive)
    {
        _db = db;
        _storage = storage;
        _drive = drive;
    }

    public async Task<PublicGalleryMediaStreamResult> Handle(
        GetPublicGalleryItemAudioQuery request, CancellationToken cancellationToken)
    {
        var language = request.LanguageCode?.Trim().ToLowerInvariant();
        if (!GalleryLanguages.IsValid(language))
            throw new NotFoundException("PublicGalleryAudio", request.GalleryItemId);

        var itemId = (ulong)request.GalleryItemId;

        // Public-visibility chain + resolve the audio fileId for the requested language in one query.
        var row = await _db.GalleryItems.AsNoTracking()
            .Where(i =>
                i.GalleryItemId == itemId &&
                i.Status == "PUBLISHED" &&
                i.DeletedAt == null &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE" &&
                i.Content != null)
            .Select(i => new
            {
                AudioFileId = language == GalleryLanguages.Vietnamese
                    ? i.Content.AudioViFileId
                    : i.Content.AudioEnFileId,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("PublicGalleryAudio", request.GalleryItemId);

        var file = await _db.Files.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FileId == row.AudioFileId, cancellationToken)
            ?? throw new NotFoundException("PublicGalleryAudio", request.GalleryItemId);

        // Only ever serve a genuine gallery audio file — never an arbitrary file id.
        if (!string.Equals(file.FilePurpose, FilePurposeDbValues.GalleryAudio, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("PublicGalleryAudio", request.GalleryItemId);

        var contentType = string.IsNullOrWhiteSpace(file.MimeType) ? "audio/mpeg" : file.MimeType!;

        var isGoogleDrive = string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(file.ExternalFileId);

        if (isGoogleDrive)
        {
            var drive = await _drive.DownloadRangeAsync(
                file.ExternalFileId!, request.RangeFrom, request.RangeTo, cancellationToken);

            return new PublicGalleryMediaStreamResult
            {
                Stream = drive.Stream,
                ContentType = string.IsNullOrWhiteSpace(drive.ContentType) ? contentType : drive.ContentType!,
                TotalLength = drive.TotalLength,
                ContentLength = drive.ContentLength,
                RangeStart = drive.RangeStart,
                RangeEnd = drive.RangeEnd,
                IsPartial = drive.IsPartial,
                SupportsRange = true,
            }.Owning(drive);
        }

        var stream = await _storage.OpenReadAsync(file, cancellationToken)
            ?? throw new NotFoundException("PublicGalleryAudio", request.GalleryItemId);

        long? total = stream.CanSeek ? stream.Length : file.FileSize;
        return new PublicGalleryMediaStreamResult
        {
            Stream = stream,
            ContentType = contentType,
            TotalLength = total,
            ContentLength = total,
            RangeStart = 0,
            RangeEnd = total is { } t && t > 0 ? t - 1 : 0,
            IsPartial = false,
            SupportsRange = stream.CanSeek,
        };
    }
}
