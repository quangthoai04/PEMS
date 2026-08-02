using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;
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

        // ── Why the bytes could not be read is three different people's problem ──
        //
        // "Không tải được tệp" is the same sentence for a file somebody deleted, a credential that was
        // never granted access, and a `files` row that never addressed anything real. Each failure below
        // therefore carries the StorageErrorCodes value that names its cause, so the frontend can show
        // the operator the sentence that matches (errors:api.STORAGE_*) instead of a bare 404.
        //
        // Note what this does NOT change: refusing a caller is still ForbiddenException above, decided
        // before any of this runs. A permission problem between the USER and the file, and a permission
        // problem between US and the provider, must never arrive as the same answer.
        var isGoogleDriveRow = string.Equals(
            file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase);

        // A row that addresses nothing at all. No request is made of the provider — the record itself is
        // the defect, so nobody should go looking on Drive (or on disk) for it.
        if (isGoogleDriveRow && string.IsNullOrWhiteSpace(file.ExternalFileId))
            throw new NotFoundException(
                "Bản ghi tệp trong hệ thống không trỏ tới tệp hợp lệ.",
                StorageErrorCodes.FileReferenceInvalid);

        if (string.Equals(file.StorageProvider, "LOCAL", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(file.ObjectKey))
            throw new NotFoundException(
                "Bản ghi tệp trong hệ thống không trỏ tới tệp hợp lệ.",
                StorageErrorCodes.FileReferenceInvalid);

        // Google Drive files need an authorized fetch (the generic OpenReadAsync only does an
        // unauthenticated GET, which fails for private Drive files such as avatars).
        byte[] content;
        try
        {
            await using var stream = (isGoogleDriveRow
                ? await _drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
                : await _storage.OpenReadAsync(file, cancellationToken))
                ?? throw new NotFoundException(
                    "Không đọc được nội dung tệp.", StorageErrorCodes.FileNotFound);

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            content = ms.ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BusinessRuleException)
        {
            // Already classified by the provider (FORBIDDEN / AUTH_FAILED / NOT_FOUND / UNAVAILABLE).
            throw;
        }
        catch (NotFoundException)
        {
            throw;
        }
        catch (Exception)
        {
            // A stream that opened and then failed mid-read: a network drop, a 5xx, a truncated body.
            // The storage services log the exception where it happens; the caller gets a stable code and
            // a safe sentence, never the raw text.
            throw new BusinessRuleException(
                "Không kết nối được tới kho tệp. Vui lòng thử lại sau.",
                StorageErrorCodes.Unavailable);
        }

        return new FileContentDto
        {
            Content = content,
            ContentType = string.IsNullOrWhiteSpace(file.MimeType) ? "application/octet-stream" : file.MimeType!,
            // Sanitised at the edge of the application, so every transport that echoes it (the
            // Content-Disposition header, a save-as dialog) gets a leaf name with no control characters.
            FileName = FileResponseSafety.SafeFileName(file.OriginalFilename),
        };
    }
}
