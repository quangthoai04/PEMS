using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Queries.GetFileContent;

namespace PEMS.Application.Galleries.Public.Queries.GetPublicGalleryMedia;

/// <summary>
/// Streams a gallery file's bytes for the public page, but only after confirming the file id belongs to
/// a public-visible gallery media (as the main file or its thumbnail). The visibility chain matches the
/// navigation/detail queries (campus/area/location ACTIVE, item PUBLISHED &amp; not deleted, media ACTIVE
/// &amp; not deleted). Anything else → 404, so the proxy can never be used to fetch avatars, documents,
/// hidden media, etc. Google Drive files require an authorized fetch (mirrors GetFileContentQueryHandler).
/// </summary>
public sealed class GetPublicGalleryMediaQueryHandler : IRequestHandler<GetPublicGalleryMediaQuery, FileContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetPublicGalleryMediaQueryHandler(
        IApplicationDbContext db, IFileStorageService storage, IGoogleDriveStorageService drive)
    {
        _db = db;
        _storage = storage;
        _drive = drive;
    }

    public async Task<FileContentDto> Handle(GetPublicGalleryMediaQuery request, CancellationToken cancellationToken)
    {
        var isPublicGalleryFile = await _db.GalleryItemMedia.AsNoTracking().AnyAsync(m =>
            (m.FileId == request.FileId || m.ThumbnailFileId == request.FileId) &&
            m.Status == "ACTIVE" &&
            m.DeletedAt == null &&
            m.GalleryItem.Status == "PUBLISHED" &&
            m.GalleryItem.DeletedAt == null &&
            m.GalleryItem.Location.Status == "ACTIVE" &&
            m.GalleryItem.Location.Area.Status == "ACTIVE" &&
            m.GalleryItem.Location.Area.Campus.Status == "ACTIVE",
            cancellationToken);

        // Area/location cover images are master data (not gallery item media), but the Area Showcase
        // needs to display them on the anonymous public page. Allow a cover file id that belongs to an
        // ACTIVE area/location under an ACTIVE campus — still public-safe, never a raw arbitrary file.
        if (!isPublicGalleryFile)
        {
            isPublicGalleryFile = await _db.GalleryAreas.AsNoTracking().AnyAsync(a =>
                a.CoverFileId == request.FileId &&
                a.Status == "ACTIVE" &&
                a.Campus.Status == "ACTIVE",
                cancellationToken);
        }

        if (!isPublicGalleryFile)
        {
            isPublicGalleryFile = await _db.GalleryLocations.AsNoTracking().AnyAsync(l =>
                l.CoverFileId == request.FileId &&
                l.Status == "ACTIVE" &&
                l.Area.Status == "ACTIVE" &&
                l.Area.Campus.Status == "ACTIVE",
                cancellationToken);
        }

        // TTS narration audio (EverAI phase): /api/files is [Authorize], so the anonymous public page
        // plays a READY narration through this same scoped proxy. Only files linked from a READY TTS
        // row of a public-visible item are served — FAILED rows, stale hashes on HIDDEN items, etc.
        // stay unreachable.
        if (!isPublicGalleryFile)
        {
            isPublicGalleryFile = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
                t.AudioFileId == request.FileId &&
                t.Status == "READY" &&
                t.GalleryItem.Status == "PUBLISHED" &&
                t.GalleryItem.DeletedAt == null &&
                t.GalleryItem.Location.Status == "ACTIVE" &&
                t.GalleryItem.Location.Area.Status == "ACTIVE" &&
                t.GalleryItem.Location.Area.Campus.Status == "ACTIVE",
                cancellationToken);
        }

        if (!isPublicGalleryFile)
            throw new NotFoundException("PublicGalleryMedia", request.FileId);

        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.FileId == request.FileId, cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        var isGoogleDrive = string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(file.ExternalFileId);

        await using var stream = (isGoogleDrive
            ? await _drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
            : await _storage.OpenReadAsync(file, cancellationToken))
            ?? throw new NotFoundException("FileContent", request.FileId);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        return new FileContentDto
        {
            Content = ms.ToArray(),
            ContentType = string.IsNullOrWhiteSpace(file.MimeType) ? "application/octet-stream" : file.MimeType!,
            FileName = file.OriginalFilename,
        };
    }
}
