using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Files.Queries.GetFileContent;

/// <summary>
/// Loads the file metadata, then streams the binary from the storage provider. The caller is
/// already authenticated (controller-level <c>[Authorize]</c>); any signed-in user may view an
/// avatar that another user references. Stricter per-file policies would be layered here for
/// non-avatar purposes.
/// </summary>
public sealed class GetFileContentQueryHandler : IRequestHandler<GetFileContentQuery, FileContentResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _storage;

    public GetFileContentQueryHandler(IApplicationDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<FileContentResult> Handle(GetFileContentQuery request, CancellationToken cancellationToken)
    {
        var fileId = (ulong)request.FileId;

        var file = await _db.Files.AsNoTracking()
            .Where(f => f.FileId == fileId)
            .Select(f => new
            {
                f.StorageProvider,
                f.ExternalFileId,
                f.MimeType,
                f.OriginalFilename,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("File", request.FileId);

        if (string.IsNullOrWhiteSpace(file.ExternalFileId))
            throw new NotFoundException("File", request.FileId);

        // Only the Google Drive provider is wired today; metadata-only rows would 404 above.
        var stream = await _storage.DownloadAsync(file.ExternalFileId, cancellationToken);

        return new FileContentResult
        {
            Content = stream,
            ContentType = string.IsNullOrWhiteSpace(file.MimeType) ? "application/octet-stream" : file.MimeType,
            FileName = string.IsNullOrWhiteSpace(file.OriginalFilename) ? "file" : file.OriginalFilename,
        };
    }
}
