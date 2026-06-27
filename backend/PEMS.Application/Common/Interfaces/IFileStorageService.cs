using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Binary storage for uploaded files (attachments, inline images, documents). The DB row in
/// <c>files</c> holds the metadata; the bytes live here. The default implementation stores on the
/// local disk; <see cref="OpenReadAsync"/> also fetches remote providers (e.g. Google Drive) via the
/// file's download URL so the email pipeline can stream any file by its <c>file_id</c>.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Persists <paramref name="content"/> and returns where it landed (provider + object key + size + checksum).</summary>
    Task<StoredFileInfo> SaveAsync(
        Stream content, string originalFilename, string? contentType, string? purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream for an already-stored file. For LOCAL files it reads from disk; for
    /// remote providers it fetches the file's download/web-view URL. Returns null when the bytes
    /// cannot be located.
    /// </summary>
    Task<Stream?> OpenReadAsync(UploadedFile file, CancellationToken cancellationToken = default);
}

/// <summary>Result of persisting a file's bytes.</summary>
public sealed record StoredFileInfo(
    string StorageProvider,
    string ObjectKey,
    long FileSize,
    string? ChecksumSha256,
    string? BucketName = null);
