using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Storage;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// Google Drive, on disk. Folders are paths, files are files, and ids are stable strings derived from
/// them — so a suite can archive a document, read the same bytes back through
/// <see cref="IFileStorageService.OpenReadAsync"/>, and never leave the machine.
///
/// <para>
/// This is a substitute for a THIRD-PARTY service, not for PEMS code. Everything between the handler and
/// here — the folder service, the upload service, the checksum, the validation policy, the
/// <c>files</c>/<c>documents</c> rows, the GOOGLE_DRIVE branch of the local storage reader — is the real
/// implementation, which is the part a test of the setup-progress flow is actually about. Pointing the
/// suite at real Drive would test Google's availability and a refresh token instead.
/// </para>
/// <para>
/// <see cref="EnsureChildFolderAsync"/> is idempotent, as the interface promises: the same name under the
/// same parent returns the same id twice. A double that minted a fresh id each call would hide exactly
/// the duplicate-folder bug the real implementation is written to avoid.
/// </para>
/// </summary>
public sealed class FakeGoogleDriveStorage : IGoogleDriveStorageService
{
    /// <summary>
    /// The stored bytes, keyed by the id handed back at upload. In memory rather than on disk: reports
    /// are small, nothing outside the test reads them, and a temp directory is one more thing that can
    /// be missing when a suite needs it.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>How many files this double is holding — a cheap check that an upload really happened.</summary>
    public int FileCount => _files.Count;

    public Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
        string folderName, string parentFolderId, CancellationToken cancellationToken = default)
        => Task.FromResult(new GoogleDriveFolderResult
        {
            // Deterministic and hierarchical, so the same (parent, name) is the same folder on every call.
            ExternalFolderId = $"{parentFolderId}/{Sanitize(folderName)}",
            WebViewUrl = null,
        });

    public Task<GoogleDriveUploadResult> UploadFileAsync(
        byte[] content, string driveFileName, string contentType, string? folderId = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        _files[id] = content;

        return Task.FromResult(new GoogleDriveUploadResult
        {
            StorageProvider = "GOOGLE_DRIVE",
            ExternalFileId = id,
            WebViewUrl = null,
            DownloadUrl = null,
            FileSize = content.LongLength,
        });
    }

    /// <summary>
    /// Serves the stored bytes, or fails the way the real client fails — a
    /// <see cref="BusinessRuleException"/> carrying <see cref="StorageErrorCodes.FileNotFound"/>. It used
    /// to throw <see cref="FileNotFoundException"/>, which no production code path recognises, so a suite
    /// exercising "the report was deleted" saw a generic unavailability instead of the code the handlers
    /// actually branch on.
    /// </summary>
    public Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken = default)
        => _files.TryGetValue(externalFileId, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new BusinessRuleException(
                "Không đọc được tệp trên Google Drive: tệp không tồn tại, đã bị xoá, hoặc tài khoản dịch vụ "
                + "không được chia sẻ tệp này.",
                StorageErrorCodes.FileNotFound);

    /// <summary>Forgets a file's bytes while leaving every DB row intact — a purge, as seen from PEMS.</summary>
    public bool Forget(string externalFileId) => _files.TryRemove(externalFileId, out _);

    public Task DeleteAsync(string externalFileId, CancellationToken cancellationToken = default)
    {
        _files.TryRemove(externalFileId, out _);
        return Task.CompletedTask;
    }

    // ── Not reachable from the flows these suites exercise ──────────────────
    //
    // Throwing rather than returning something plausible keeps the assumption visible: if a change makes
    // one of these run, the test says so instead of quietly passing on invented data.

    public Task<GoogleDriveUploadResult> UploadAvatarAsync(
        byte[] content, string driveFileName, string contentType, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Avatar upload is not part of any flow using this double.");

    public Task<GoogleDriveDownloadResult> DownloadRangeAsync(
        string externalFileId, long? from, long? to, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Ranged download is not part of any flow using this double.");

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }
}
