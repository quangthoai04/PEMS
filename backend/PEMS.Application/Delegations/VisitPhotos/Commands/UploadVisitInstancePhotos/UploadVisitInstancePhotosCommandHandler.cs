using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.VisitPhotos.Commands.UploadVisitInstancePhotos;

public sealed class UploadVisitInstancePhotosCommandHandler
    : IRequestHandler<UploadVisitInstancePhotosCommand, UploadVisitInstancePhotosResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileUploadService _fileUpload;
    private readonly IFileValidationPolicy _validationPolicy;
    private readonly IVisitPhotoFolderService _folderService;
    private readonly IGoogleDriveStorageService _drive;
    private readonly ILogger<UploadVisitInstancePhotosCommandHandler> _logger;

    public UploadVisitInstancePhotosCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileUploadService fileUpload,
        IFileValidationPolicy validationPolicy,
        IVisitPhotoFolderService folderService,
        IGoogleDriveStorageService drive,
        ILogger<UploadVisitInstancePhotosCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _fileUpload = fileUpload;
        _validationPolicy = validationPolicy;
        _folderService = folderService;
        _drive = drive;
        _logger = logger;
    }

    public async Task<UploadVisitInstancePhotosResponse> Handle(
        UploadVisitInstancePhotosCommand request, CancellationToken cancellationToken)
    {
        // Authorize against the same rule the database trigger trg_visit_photos_validate_bi enforces
        // (Host, Admin/Staff, or an accepted/assigned participant) so a rejected caller fails with a
        // clean 403 here rather than letting the INSERT blow up as a 500.
        var scope = await VisitPhotoStudentScope.ResolveAcceptedStudentAsync(
            _db, _currentUser, request.VisitInstanceId, cancellationToken);

        if (!scope.CanUpload)
            throw new BusinessRuleException(
                "Chuyến thăm đã bị hủy — không thể tải ảnh lên.", "VISIT_PHOTO_INSTANCE_CANCELLED");

        // Fail the whole batch BEFORE any Drive upload: a fake-MIME or oversized file must not leave
        // earlier files of the same request half-uploaded.
        var rule = _validationPolicy.GetRule(FilePurpose.VisitRequestPhoto);
        foreach (var file in request.Files)
            FileContentValidator.Validate(file.FileName, file.ContentType, file.Content, rule);

        var target = await _folderService.EnsureUploadTargetAsync(
            scope.Instance, scope.CampusCode, scope.UserId, cancellationToken);

        var uploaded = new List<UploadedVisitPhotoDto>();
        foreach (var file in request.Files)
        {
            await using var stream = new MemoryStream(file.Content);
            var result = await _fileUpload.UploadBusinessFileAsync(
                stream, file.FileName, file.ContentType, file.Content.LongLength,
                FilePurpose.VisitRequestPhoto, (long)scope.UserId,
                target.CampusFolderExternalId, cancellationToken);

            var photo = new VisitPhoto
            {
                VisitRequestId = scope.Instance.VisitRequestId,
                VisitInstanceId = scope.Instance.VisitInstanceId,
                VisitPhotoFolderId = target.Folder.VisitPhotoFolderId,
                FileId = (ulong)result.FileId,
                Status = "ACTIVE",
                UploadedBy = scope.UserId,
                UploadedAt = VietnamTime.Now(),
            };
            _db.VisitPhotos.Add(photo);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // The Drive binary + files row are already committed — compensate so no orphan
                // remains, then surface the original failure.
                _db.VisitPhotos.Remove(photo);
                await CleanUpOrphanedUploadAsync((ulong)result.FileId, result.ExternalFileId);
                throw;
            }

            uploaded.Add(new UploadedVisitPhotoDto
            {
                VisitPhotoId = photo.VisitPhotoId,
                FileName = file.FileName,
                Url = result.FileUrl,
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = scope.UserId,
            Action = "UPLOAD_VISIT_PHOTOS",
            EntityType = "VisitRequestCampus",
            EntityId = scope.Instance.VisitInstanceId,
            CreatedAt = VietnamTime.Now(),
        });
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadVisitInstancePhotosResponse { Photos = uploaded };
    }

    private async Task CleanUpOrphanedUploadAsync(ulong fileId, string? externalFileId)
    {
        try
        {
            var fileRow = await _db.Files.FirstOrDefaultAsync(f => f.FileId == fileId, CancellationToken.None);
            if (fileRow != null)
            {
                _db.Files.Remove(fileRow);
                await _db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove orphaned files row {FileId} after visit photo link failure.", fileId);
        }

        if (!string.IsNullOrWhiteSpace(externalFileId))
        {
            try { await _drive.DeleteAsync(externalFileId!, CancellationToken.None); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphaned Drive file {ExternalFileId} after visit photo link failure.", externalFileId);
            }
        }
    }
}
