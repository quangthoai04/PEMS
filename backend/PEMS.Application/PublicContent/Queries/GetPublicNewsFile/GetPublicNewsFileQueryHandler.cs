using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Queries.GetFileContent;
using PEMS.Domain.Constants;

namespace PEMS.Application.PublicContent.Queries.GetPublicNewsFile;

public sealed class GetPublicNewsFileQueryHandler : IRequestHandler<GetPublicNewsFileQuery, FileContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetPublicNewsFileQueryHandler(
        IApplicationDbContext db,
        IFileStorageService storage,
        IGoogleDriveStorageService drive)
    {
        _db = db;
        _storage = storage;
        _drive = drive;
    }

    public async Task<FileContentDto> Handle(GetPublicNewsFileQuery request, CancellationToken cancellationToken)
    {
        // The file must belong to a PUBLISHED news post — either as its cover…
        var isPublishedCover = await _db.News
            .AsNoTracking()
            .AnyAsync(n => n.CoverFileId == request.FileId
                        && n.Status == NewsConstants.Status.Published, cancellationToken);

        // …or as a section file of one of its translations.
        var isPublishedSectionFile = !isPublishedCover && await _db.NewsSectionFiles
            .AsNoTracking()
            .AnyAsync(sf => sf.FileId == request.FileId
                         && _db.NewsContentSections.Any(s => s.SectionId == sf.SectionId
                             && _db.NewsTranslations.Any(t => t.NewsTranslationId == s.NewsTranslationId
                                 && _db.News.Any(n => n.NewsId == t.NewsId
                                                   && n.Status == NewsConstants.Status.Published))),
                cancellationToken);

        if (!isPublishedCover && !isPublishedSectionFile)
            throw new NotFoundException("File", request.FileId);

        var file = await _db.Files
            .AsNoTracking()
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
