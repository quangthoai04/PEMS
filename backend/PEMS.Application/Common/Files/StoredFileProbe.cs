using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Storage;

namespace PEMS.Application.Common.Files;

/// <summary>What a probe found when it tried to read a stored file.</summary>
/// <param name="Availability">The outcome, for branching.</param>
/// <param name="ErrorCode">
/// The matching <see cref="StorageErrorCodes"/> value, or null when the read succeeded. Carried so a
/// caller can log or surface WHY without re-deriving it from a message string.
/// </param>
public readonly record struct StoredFileProbeResult(StoredFileAvailability Availability, string? ErrorCode)
{
    public bool IsAvailable => Availability == StoredFileAvailability.Available;
}

/// <summary>
/// Answers "can this <c>file_id</c>'s bytes actually be read right now", which is a different question
/// from "does a <c>files</c> row exist" — and the difference is exactly where this system had been
/// getting it wrong. A row can outlive its bytes (the file was deleted on Drive, the share was revoked,
/// the row was seeded with a placeholder id that never addressed anything), and every caller that
/// treated the row as proof of the file went on to build something on top of nothing.
///
/// <para>
/// It reads through <see cref="IGoogleDriveStorageService"/> rather than
/// <see cref="IFileStorageService.OpenReadAsync"/> for Drive-backed rows, deliberately: the latter
/// catches Drive's failure and returns null, which is convenient for a mailer that wants to skip a file
/// but destroys the distinction between "deleted" and "refused" that makes this diagnosable.
/// </para>
/// </summary>
public static class StoredFileProbe
{
    /// <summary>
    /// Attempts to read <paramref name="fileId"/>'s bytes and reports what happened. Never throws for a
    /// storage problem — that is the result, not an exception — but always propagates cancellation.
    /// </summary>
    public static async Task<StoredFileProbeResult> ProbeAsync(
        IApplicationDbContext db,
        IFileStorageService storage,
        IGoogleDriveStorageService drive,
        ulong fileId,
        CancellationToken cancellationToken)
    {
        var file = await db.Files.FirstOrDefaultAsync(f => f.FileId == fileId, cancellationToken);
        if (file is null)
            return new StoredFileProbeResult(
                StoredFileAvailability.ReferenceInvalid, StorageErrorCodes.FileReferenceInvalid);

        var isGoogleDrive = string.Equals(
            file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase);

        // A Drive row with no external id addresses nothing at all. Reported as a broken RECORD rather
        // than a missing file: no request was made, so nobody should go looking on Drive for it.
        if (isGoogleDrive && string.IsNullOrWhiteSpace(file.ExternalFileId))
            return new StoredFileProbeResult(
                StoredFileAvailability.ReferenceInvalid, StorageErrorCodes.FileReferenceInvalid);

        try
        {
            await using var stream = isGoogleDrive
                ? await drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
                : await storage.OpenReadAsync(file, cancellationToken);

            if (stream is null)
                return new StoredFileProbeResult(
                    StoredFileAvailability.NotFound, StorageErrorCodes.FileNotFound);

            // Touch the bytes. A stream that opens and then fails on first read is still an unreadable
            // file, and this method exists precisely so its caller does not discover that later.
            var probe = new byte[1];
            await stream.ReadAsync(probe.AsMemory(0, 1), cancellationToken);

            return new StoredFileProbeResult(StoredFileAvailability.Available, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BusinessRuleException ex)
        {
            return new StoredFileProbeResult(FromCode(ex.ErrorCode), ex.ErrorCode ?? StorageErrorCodes.Unavailable);
        }
        catch (Exception)
        {
            return new StoredFileProbeResult(StoredFileAvailability.Unavailable, StorageErrorCodes.Unavailable);
        }
    }

    private static StoredFileAvailability FromCode(string? code) => code switch
    {
        StorageErrorCodes.FileNotFound => StoredFileAvailability.NotFound,
        StorageErrorCodes.FileForbidden => StoredFileAvailability.Forbidden,
        StorageErrorCodes.FileReferenceInvalid => StoredFileAvailability.ReferenceInvalid,
        _ => StoredFileAvailability.Unavailable,
    };

    /// <summary>
    /// A short Vietnamese explanation of <paramref name="availability"/> for a message a Host will read.
    /// Kept next to the enum so the three causes cannot drift into one sentence again.
    /// </summary>
    public static string Describe(StoredFileAvailability availability) => availability switch
    {
        StoredFileAvailability.NotFound =>
            "tệp không còn trên Google Drive (đã bị xoá hoặc không được chia sẻ với hệ thống)",
        StoredFileAvailability.Forbidden =>
            "hệ thống không có quyền đọc tệp trên Google Drive",
        StoredFileAvailability.ReferenceInvalid =>
            "bản ghi tệp trong hệ thống không trỏ tới tệp hợp lệ",
        StoredFileAvailability.Unavailable =>
            "không kết nối được tới kho tệp",
        _ => "tệp đọc được bình thường",
    };
}
