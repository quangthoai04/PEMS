using PEMS.Application.Common.Models;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the external file storage provider (currently Google Drive).
/// Handlers depend only on this seam so the binary never touches the database and the
/// concrete provider can be swapped without touching application code.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads <paramref name="stream"/> into <paramref name="folderId"/> on the provider and
    /// returns the stored object's metadata (external id, view/download/thumbnail URLs, ...).
    /// </summary>
    Task<FileStorageUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folderId,
        CancellationToken cancellationToken);

    /// <summary>Opens a read stream for the object identified by <paramref name="externalFileId"/>.</summary>
    Task<Stream> DownloadAsync(
        string externalFileId,
        CancellationToken cancellationToken);

    /// <summary>Best-effort delete used to roll back an upload when the following DB write fails.</summary>
    Task DeleteAsync(
        string externalFileId,
        CancellationToken cancellationToken);
}
