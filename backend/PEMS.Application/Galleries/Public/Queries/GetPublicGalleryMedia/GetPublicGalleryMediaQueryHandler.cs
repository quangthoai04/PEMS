using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Queries.GetFileContent;
using PEMS.Application.Galleries.Public.Common;

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
        var isPublicGalleryFile = await PublicGalleryMediaAccess.IsPublicGalleryFileAsync(
            _db, request.FileId, cancellationToken);

        if (!isPublicGalleryFile)
            throw new NotFoundException("PublicGalleryMedia", request.FileId);

        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.FileId == request.FileId, cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        // A YouTube reference has no binary to stream — it is embedded via iframe on the client. Serving
        // it here would try (and fail) a bogus download, so reject it cleanly (controlled 404).
        if (string.Equals(file.FilePurpose, "GALLERY_YOUTUBE_VIDEO", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("PublicGalleryMedia", request.FileId);

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
