using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Files.Queries.GetFileContent;

public sealed class GetFileContentQueryHandler : IRequestHandler<GetFileContentQuery, FileContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;

    public GetFileContentQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IFileStorageService storage, IGoogleDriveStorageService drive)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _drive = drive;
    }

    public async Task<FileContentDto> Handle(GetFileContentQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var file = await _db.Files
            .FirstOrDefaultAsync(f => f.FileId == request.FileId, cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        // A YouTube reference is metadata only (no binary). It is embedded via iframe on the client, so
        // this content endpoint must never be used for it — reject cleanly instead of a bogus download.
        if (string.Equals(file.FilePurpose, "GALLERY_YOUTUBE_VIDEO", StringComparison.OrdinalIgnoreCase))
            throw new NotFoundException("FileContent", request.FileId);

        // Google Drive files need an authorized fetch (the generic OpenReadAsync only does an
        // unauthenticated GET, which fails for private Drive files such as avatars).
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
