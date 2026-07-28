using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Documents;

namespace PEMS.Application.Reports.Common;

/// <summary>See <see cref="IReportArchiveService"/>.</summary>
public sealed class ReportArchiveService : IReportArchiveService
{
    private const string ReportFolderName = "Report";

    private readonly IApplicationDbContext _db;
    private readonly IFileUploadService _fileUpload;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IFileStorageFolderResolver _folderResolver;
    private readonly IDateTimeService _clock;
    private readonly ILogger<ReportArchiveService> _logger;

    public ReportArchiveService(
        IApplicationDbContext db,
        IFileUploadService fileUpload,
        IGoogleDriveStorageService drive,
        IFileStorageFolderResolver folderResolver,
        IDateTimeService clock,
        ILogger<ReportArchiveService> logger)
    {
        _db = db;
        _fileUpload = fileUpload;
        _drive = drive;
        _folderResolver = folderResolver;
        _clock = clock;
        _logger = logger;
    }

    public async Task ArchiveAsync(
        byte[] content, string fileName, string contentType, string documentCategory,
        ulong? campusId, ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            var rootFolderId = _folderResolver.ResolveFolderId(FilePurpose.VisitRequestPhoto);
            var reportFolder = await _drive.EnsureChildFolderAsync(ReportFolderName, rootFolderId, cancellationToken);

            await using var stream = new MemoryStream(content);
            var uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, fileName, contentType, content.LongLength,
                FilePurpose.ReportDocument, (long)userId, reportFolder.ExternalFolderId, cancellationToken);

            _db.Documents.Add(new Document
            {
                FileId = (ulong)uploaded.FileId,
                OwnerType = "REPORT",
                OwnerId = null,
                CampusId = campusId,
                Title = fileName,
                DocumentCategory = documentCategory,
                Status = "PUBLISHED",
                CreatedAt = _clock.VietnamNow,
                CreatedBy = userId,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Archiving is a side effect of the export, never a precondition for it — the user must
            // still get their file even if Drive/DB is unavailable right now.
            _logger.LogWarning(ex,
                "Failed to archive report {FileName} (category {DocumentCategory}) to Drive/documents.",
                fileName, documentCategory);
        }
    }
}
