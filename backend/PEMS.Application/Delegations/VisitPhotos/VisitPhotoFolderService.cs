using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Delegations.VisitPhotos;

/// <summary>
/// See <see cref="IVisitPhotoFolderService"/>. Folder ids are never hard-coded: the root comes from
/// <see cref="IFileStorageFolderResolver"/> (purpose VISIT_REQUEST_PHOTO →
/// <c>GoogleDriveOptions.VisitRequestPhotoFolderId</c>); VR/campus subfolders are ensured
/// idempotently on Drive. The DB unique key on <c>visit_request_id</c> is the arbiter for
/// concurrent first uploads: the loser reuses the winner's row and best-effort removes its own
/// just-created Drive folder.
/// </summary>
public sealed class VisitPhotoFolderService : IVisitPhotoFolderService
{
    private readonly IApplicationDbContext _db;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IFileStorageFolderResolver _folderResolver;
    private readonly ILogger<VisitPhotoFolderService> _logger;

    public VisitPhotoFolderService(
        IApplicationDbContext db,
        IGoogleDriveStorageService drive,
        IFileStorageFolderResolver folderResolver,
        ILogger<VisitPhotoFolderService> logger)
    {
        _db = db;
        _drive = drive;
        _folderResolver = folderResolver;
        _logger = logger;
    }

    public async Task<VisitPhotoUploadTarget> EnsureUploadTargetAsync(
        VisitRequestCampus instance, string campusCode, ulong userId, CancellationToken cancellationToken)
    {
        var folder = await _db.VisitPhotoFolders
            .FirstOrDefaultAsync(f => f.VisitRequestId == instance.VisitRequestId, cancellationToken)
            ?? await CreateRequestFolderAsync(instance.VisitRequestId, userId, cancellationToken);

        var campusFolder = await _drive.EnsureChildFolderAsync(
            campusCode, folder.ExternalFolderId, cancellationToken);

        return new VisitPhotoUploadTarget
        {
            Folder = folder,
            CampusFolderExternalId = campusFolder.ExternalFolderId,
        };
    }

    private async Task<VisitPhotoFolder> CreateRequestFolderAsync(
        ulong visitRequestId, ulong userId, CancellationToken cancellationToken)
    {
        var rootFolderId = _folderResolver.ResolveFolderId(FilePurpose.VisitRequestPhoto);
        var folderName = $"VR-{visitRequestId}";
        var driveFolder = await _drive.EnsureChildFolderAsync(folderName, rootFolderId, cancellationToken);

        var folder = new VisitPhotoFolder
        {
            VisitRequestId = visitRequestId,
            StorageProvider = "GOOGLE_DRIVE",
            ExternalFolderId = driveFolder.ExternalFolderId,
            FolderName = folderName,
            WebViewUrl = driveFolder.WebViewUrl,
            Status = "ACTIVE",
            CreatedAt = VietnamTime.Now(),
            CreatedBy = userId,
        };

        _db.VisitPhotoFolders.Add(folder);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return folder;
        }
        catch (DbUpdateException)
        {
            // uq_visit_photo_folders_request fired: another upload created the row first. Reuse the
            // winner; if we created a different (duplicate) Drive folder in the race window, drop it.
            _db.VisitPhotoFolders.Remove(folder);

            var existing = await _db.VisitPhotoFolders
                .FirstOrDefaultAsync(f => f.VisitRequestId == visitRequestId, cancellationToken);
            if (existing is null)
                throw; // not the duplicate-key case — surface the original failure

            if (!string.Equals(existing.ExternalFolderId, driveFolder.ExternalFolderId, StringComparison.Ordinal))
            {
                try { await _drive.DeleteAsync(driveFolder.ExternalFolderId, cancellationToken); }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx,
                        "Failed to clean up duplicate visit photo Drive folder {FolderId} for request {VisitRequestId}.",
                        driveFolder.ExternalFolderId, visitRequestId);
                }
            }

            return existing;
        }
    }
}
