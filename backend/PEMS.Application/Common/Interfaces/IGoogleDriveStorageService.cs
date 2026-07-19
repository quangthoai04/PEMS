using PEMS.Application.Common.Models;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Google Drive binary storage for user avatars (UC-15). Implemented in Infrastructure as a
/// dependency-free REST client that mints a short-lived access token from the configured
/// long-lived <c>RefreshToken</c>. Distinct from <see cref="IFileStorageService"/> (the disk-backed
/// store for email attachments) because Drive needs authorized upload/download/delete.
///
/// On token problems it throws <see cref="PEMS.Application.Common.Exceptions.BusinessRuleException"/>
/// with one of: <c>GOOGLE_DRIVE_NOT_CONNECTED</c>, <c>GOOGLE_DRIVE_TOKEN_EXPIRED</c>,
/// <c>UPLOAD_AVATAR_FAILED</c>.
/// </summary>
public interface IGoogleDriveStorageService
{
    /// <summary>
    /// Uploads <paramref name="content"/> into the configured avatars folder and returns the
    /// Drive file id plus URLs.
    /// </summary>
    /// <param name="content">Raw file bytes.</param>
    /// <param name="driveFileName">Display name for the Drive file (the storage object key).</param>
    /// <param name="contentType">MIME type, e.g. <c>image/jpeg</c>.</param>
    Task<GoogleDriveUploadResult> UploadAvatarAsync(
        byte[] content, string driveFileName, string contentType, CancellationToken cancellationToken = default);

    Task<GoogleDriveUploadResult> UploadFileAsync(
        byte[] content, string driveFileName, string contentType, string? folderId = null, CancellationToken cancellationToken = default);

    /// <summary>Downloads a Drive file's bytes by its external id (authorized request).</summary>
    Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a Drive file (optionally a byte range) without buffering the whole file into memory —
    /// forwards an HTTP <c>Range</c> header to Drive so video playback can seek. <paramref name="from"/>
    /// null streams the whole file; <paramref name="to"/> null means "to the end". The returned
    /// <see cref="GoogleDriveDownloadResult"/> owns the live response and must be disposed by the caller.
    /// </summary>
    Task<GoogleDriveDownloadResult> DownloadRangeAsync(
        string externalFileId, long? from, long? to, CancellationToken cancellationToken = default);

    /// <summary>Best-effort delete of a Drive file (used to roll back an orphaned upload).</summary>
    Task DeleteAsync(string externalFileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the id (and web view link) of the child folder named <paramref name="folderName"/>
    /// under <paramref name="parentFolderId"/>, creating it when missing. Idempotent: an existing
    /// (non-trashed) folder with the same name is reused, never duplicated.
    /// </summary>
    Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
        string folderName, string parentFolderId, CancellationToken cancellationToken = default);
}
