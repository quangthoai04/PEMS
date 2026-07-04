using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Queries.GetFileContent;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Queries.GetPublicPartnerMedia;

public sealed class GetPublicPartnerMediaQueryHandler : IRequestHandler<GetPublicPartnerMediaQuery, FileContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetPublicPartnerMediaQueryHandler(
        IApplicationDbContext db, IFileStorageService storage, IGoogleDriveStorageService drive)
    {
        _db = db;
        _storage = storage;
        _drive = drive;
    }

    public async Task<FileContentDto> Handle(GetPublicPartnerMediaQuery request, CancellationToken cancellationToken)
    {
        var isPublicPartnerFile = await _db.Partners.AsNoTracking().AnyAsync(p =>
            (p.LogoFileId == request.FileId || p.CoverFileId == request.FileId) &&
            p.ProfileStatus == PartnerProfileStatuses.Approved &&
            p.Visibility == PartnerVisibilities.Public,
            cancellationToken);

        if (!isPublicPartnerFile)
            throw new NotFoundException("PublicPartnerMedia", request.FileId);

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
