using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Files.Common;

namespace PEMS.Application.Files.Queries.GetFileContent;

/// <summary>
/// Streams one stored file's bytes to a user entitled to them.
///
/// <para>
/// This handler used to check only that the caller was signed in, and then read the file. That made the
/// numeric <c>file_id</c> a master key: any internal account could walk the ids and pull email
/// attachments, unsent drafts, visit photos and partner documents belonging to people they had no
/// relationship with. The authorization now runs BEFORE the storage path is resolved and before any
/// stream is opened, so a refused request touches neither the filesystem nor the Drive API.
/// </para>
/// </summary>
public sealed class GetFileContentQueryHandler : IRequestHandler<GetFileContentQuery, FileContentDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IFileAccessAuthorizationService _access;

    public GetFileContentQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IFileStorageService storage, IGoogleDriveStorageService drive,
        IFileAccessAuthorizationService access)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
        _drive = drive;
        _access = access;
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

        // ── The gate. Nothing below this line may run for a caller without access ──
        //
        // The refusal deliberately carries no detail: not the filename, the size, the MIME type, the
        // owner, nor which object the file belongs to. Anything more would turn a rejected download into
        // a metadata endpoint for files the caller cannot read.
        if (!await _access.CanDownloadAsync(file, cancellationToken))
            throw new ForbiddenException("Bạn không có quyền tải tệp này.");

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
