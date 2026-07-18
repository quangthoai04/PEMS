using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Common.Files;

/// <summary>
/// Application-level shared upload service (see <see cref="IFileUploadService"/>). Wraps the low-level
/// <see cref="IGoogleDriveStorageService"/> with validation, checksum, object-key and DB bookkeeping so
/// every feature uploads files the same way. No DB transaction can span Drive: if the <c>files</c>
/// insert fails after a successful upload, the just-uploaded Drive file is deleted best-effort.
/// </summary>
public sealed class FileUploadService : IFileUploadService
{
    private const string StorageProvider = "GOOGLE_DRIVE";

    private readonly IApplicationDbContext _db;
    private readonly IGoogleDriveStorageService _storage;
    private readonly IFileStorageFolderResolver _folderResolver;
    private readonly IFileValidationPolicy _validationPolicy;
    private readonly IFileChecksumService _checksum;
    private readonly IFileObjectKeyBuilder _objectKeyBuilder;
    private readonly IDateTimeService _clock;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(
        IApplicationDbContext db,
        IGoogleDriveStorageService storage,
        IFileStorageFolderResolver folderResolver,
        IFileValidationPolicy validationPolicy,
        IFileChecksumService checksum,
        IFileObjectKeyBuilder objectKeyBuilder,
        IDateTimeService clock,
        ILogger<FileUploadService> logger)
    {
        _db = db;
        _storage = storage;
        _folderResolver = folderResolver;
        _validationPolicy = validationPolicy;
        _checksum = checksum;
        _objectKeyBuilder = objectKeyBuilder;
        _clock = clock;
        _logger = logger;
    }

    public Task<UploadedFileDto> UploadBusinessFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long fileSize,
        FilePurpose purpose,
        long uploadedBy,
        CancellationToken cancellationToken)
        => UploadCoreAsync(stream, originalFileName, contentType, purpose, uploadedBy,
            targetFolderId: null, cancellationToken);

    public Task<UploadedFileDto> UploadBusinessFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long fileSize,
        FilePurpose purpose,
        long uploadedBy,
        string targetFolderId,
        CancellationToken cancellationToken)
        => UploadCoreAsync(stream, originalFileName, contentType, purpose, uploadedBy,
            targetFolderId, cancellationToken);

    private async Task<UploadedFileDto> UploadCoreAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        FilePurpose purpose,
        long uploadedBy,
        string? targetFolderId,
        CancellationToken cancellationToken)
    {
        // Buffer once: validation needs the leading bytes, checksum needs a rewindable stream, and the
        // Drive client takes a byte[]. Uploads are bounded by the per-purpose size rule.
        await using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);
        var content = buffered.ToArray();

        var rule = _validationPolicy.GetRule(purpose);
        var validated = FileContentValidator.Validate(originalFileName, contentType, content, rule);

        buffered.Position = 0;
        var checksum = await _checksum.ComputeSha256HexAsync(buffered, cancellationToken);

        var objectKey = _objectKeyBuilder.Build(purpose, uploadedBy, originalFileName);
        var folderId = string.IsNullOrWhiteSpace(targetFolderId)
            ? _folderResolver.ResolveFolderId(purpose)
            : targetFolderId!;

        string? uploadedExternalFileId = null;
        try
        {
            var upload = await _storage.UploadFileAsync(content, objectKey, validated.MimeType, folderId, cancellationToken);
            uploadedExternalFileId = upload.ExternalFileId;

            var file = new UploadedFile
            {
                StorageProvider = StorageProvider,
                ObjectKey = objectKey,
                OriginalFilename = validated.SafeOriginalName,
                MimeType = validated.MimeType,
                FileSize = upload.FileSize,
                ChecksumSha256 = checksum,
                ExternalFileId = upload.ExternalFileId,
                WebViewUrl = upload.WebViewUrl,
                DownloadUrl = upload.DownloadUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
                FilePurpose = purpose.ToDbValue(),
                UploadedBy = (ulong)uploadedBy,
                UploadedAt = _clock.VietnamNow,
            };

            _db.Files.Add(file);
            await _db.SaveChangesAsync(cancellationToken); // assigns file.FileId

            return new UploadedFileDto
            {
                FileId = (long)file.FileId,
                FileUrl = $"/api/files/{file.FileId}/content",
                StorageProvider = StorageProvider,
                ExternalFileId = upload.ExternalFileId,
                WebViewUrl = upload.WebViewUrl,
                DownloadUrl = upload.DownloadUrl,
                ThumbnailUrl = upload.ThumbnailUrl,
                MimeType = validated.MimeType,
                FileSize = upload.FileSize,
                ChecksumSha256 = checksum,
                ObjectKey = objectKey,
            };
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(uploadedExternalFileId))
            {
                try { await _storage.DeleteAsync(uploadedExternalFileId!, cancellationToken); }
                catch (Exception cleanupEx)
                {
                    // Don't hide the original failure — just record that the orphan cleanup also failed.
                    _logger.LogWarning(cleanupEx,
                        "Failed to clean up orphaned Google Drive file {ExternalFileId} after a DB save error.",
                        uploadedExternalFileId);
                }
            }

            throw;
        }
    }
}
