using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Galleries.Public.Common;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMediaStream;

/// <summary>
/// Streams a gallery file's bytes (optionally a byte range) for the anonymous public page. Reuses the
/// shared <see cref="PublicGalleryMediaAccess"/> authorization, then serves the bytes WITHOUT buffering
/// the whole file: a Google Drive file is streamed through <see cref="IGoogleDriveStorageService.DownloadRangeAsync"/>
/// (forwarding the HTTP range so &lt;video&gt; can seek); a local file is read from disk. A YouTube
/// reference has no binary and is rejected (controlled 404).
/// </summary>
public sealed class GetPublicGalleryMediaStreamQueryHandler
    : IRequestHandler<GetPublicGalleryMediaStreamQuery, PublicGalleryMediaStreamResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetPublicGalleryMediaStreamQueryHandler(
        IApplicationDbContext db, IFileStorageService storage, IGoogleDriveStorageService drive)
    {
        _db = db;
        _storage = storage;
        _drive = drive;
    }

    public async Task<PublicGalleryMediaStreamResult> Handle(
        GetPublicGalleryMediaStreamQuery request, CancellationToken cancellationToken)
    {
        if (!await PublicGalleryMediaAccess.IsPublicGalleryFileAsync(_db, request.FileId, cancellationToken))
            throw new NotFoundException("PublicGalleryMedia", request.FileId);

        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.FileId == request.FileId, cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        // A YouTube reference has no binary to stream — it is embedded via iframe on the client.
        if (string.Equals(file.FilePurpose, "GALLERY_YOUTUBE_VIDEO", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("PublicGalleryMedia", request.FileId);

        var contentType = string.IsNullOrWhiteSpace(file.MimeType) ? "application/octet-stream" : file.MimeType!;

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

        // Local disk (seekable). Serve the full body; the controller advertises Accept-Ranges so a
        // client can retry with a range if needed (kept simple — gallery files live on Drive).
        var stream = await _storage.OpenReadAsync(file, cancellationToken)
            ?? throw new NotFoundException("FileContent", request.FileId);

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
