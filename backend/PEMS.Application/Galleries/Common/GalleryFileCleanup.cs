using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Galleries.Common;

/// <summary>
/// Best-effort compensation for gallery audio uploads that were committed to Google Drive + the
/// <c>files</c> table but then orphaned because a later step of the write failed (Drive is not part of
/// the MySQL transaction). Deletes the Drive object and removes the <c>files</c> row. Never throws — a
/// cleanup failure is logged but must not mask the original error.
/// </summary>
internal static class GalleryFileCleanup
{
    public static async Task RemoveUploadedFilesAsync(
        IApplicationDbContext db,
        IGoogleDriveStorageService drive,
        ILogger logger,
        IReadOnlyCollection<ulong> fileIds,
        CancellationToken ct)
    {
        if (fileIds is null || fileIds.Count == 0) return;

        try
        {
            var rows = await db.Files
                .Where(f => fileIds.Contains(f.FileId))
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.ExternalFileId))
                {
                    try { await drive.DeleteAsync(row.ExternalFileId!, ct); }
                    catch (System.Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Gallery orphan cleanup: failed to delete Drive object for file {FileId}.", row.FileId);
                    }
                }
                db.Files.Remove(row);
            }

            if (rows.Count > 0)
                await db.SaveChangesAsync(ct);
        }
        catch (System.Exception ex)
        {
            logger.LogWarning(ex, "Gallery orphan cleanup failed for files [{FileIds}].", string.Join(",", fileIds));
        }
    }
}
