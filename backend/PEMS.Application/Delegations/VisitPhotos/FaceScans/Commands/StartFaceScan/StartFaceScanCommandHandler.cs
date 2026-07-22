using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Common;
using PEMS.Application.Delegations.VisitPhotos.FaceScans.Services;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Delegations.VisitPhotos.FaceScans.Commands.StartFaceScan;

public sealed class StartFaceScanCommandHandler : IRequestHandler<StartFaceScanCommand, VisitPhotoFaceScanDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IFileStorageService _fileStorage;
    private readonly IGoogleDriveStorageService _drive;
    private readonly IFaceDetectionProvider _provider;
    private readonly IOcrCredentialResolver _credentialResolver;
    private readonly IFaceScanThrottle _throttle;

    public StartFaceScanCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IFileStorageService fileStorage,
        IGoogleDriveStorageService drive,
        IFaceDetectionProvider provider,
        IOcrCredentialResolver credentialResolver,
        IFaceScanThrottle throttle)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _fileStorage = fileStorage;
        _drive = drive;
        _provider = provider;
        _credentialResolver = credentialResolver;
        _throttle = throttle;
    }

    public async Task<VisitPhotoFaceScanDto> Handle(StartFaceScanCommand request, CancellationToken cancellationToken)
    {
        var photo = await _db.VisitPhotos
            .FirstOrDefaultAsync(p => p.VisitPhotoId == request.VisitPhotoId, cancellationToken)
            ?? throw new NotFoundException("VisitPhoto", request.VisitPhotoId);

        // Anti-IDOR: scope is resolved from the photo's OWN visit_instance_id, never a client value.
        var scope = await VisitPhotoFaceScanAccess.ResolveStaffAsync(
            _db, _currentUser, photo.VisitInstanceId, cancellationToken);
        var userId = scope.UserId;

        if (photo.Status != "ACTIVE")
            throw new BusinessRuleException(
                "Ảnh đã bị xóa — không thể quét khuôn mặt.", FaceScanErrorCodes.PhotoNotActive);

        // Anti-double-click: block a new scan while one is already PENDING/PROCESSING.
        var hasActiveScan = await _db.VisitPhotoFaceScans.AnyAsync(
            s => s.VisitPhotoId == photo.VisitPhotoId
                 && (s.Status == VisitPhotoFaceScan.StatusPending || s.Status == VisitPhotoFaceScan.StatusProcessing),
            cancellationToken);
        if (hasActiveScan)
            throw new ConflictException(
                "Ảnh này đang được quét khuôn mặt — vui lòng đợi.", FaceScanErrorCodes.ScanInProgress);

        var config = await _db.ApiConfigurations.FirstOrDefaultAsync(
            c => c.ApiCode == FaceDetectionConstants.ApiCode && c.DeletedAt == null, cancellationToken)
            ?? throw new BusinessRuleException("Chưa có cấu hình quét khuôn mặt.", FaceScanErrorCodes.ConfigInactive);
        if (config.Status != ApiIntegrationStatuses.Active)
            throw new BusinessRuleException(
                "Cấu hình quét khuôn mặt đang tắt. Liên hệ quản trị viên để kích hoạt.",
                FaceScanErrorCodes.ConfigInactive);

        if (!_throttle.TryAcquireUser(userId))
            throw new AuthBusinessException(FaceScanErrorCodes.RateLimited,
                "Bạn quét quá nhanh. Vui lòng thử lại sau ít phút.", 429);

        var now = _clock.VietnamNow;
        var period = now.ToString("yyyyMM");
        var quota = await _db.ApiUsageQuotas.FirstOrDefaultAsync(
            q => q.ApiConfigId == config.ApiConfigId && q.CampusScopeKey == "GLOBAL" && q.PeriodYyyymm == period,
            cancellationToken);
        if (quota is null)
        {
            quota = new ApiUsageQuota
            {
                ApiConfigId = config.ApiConfigId,
                CampusScopeKey = "GLOBAL",
                PeriodYyyymm = period,
                MonthlyLimit = (int)(config.MonthlyQuota ?? 1000),
                UsedCount = 0,
                CreatedAt = now,
                CreatedBy = userId,
            };
            _db.ApiUsageQuotas.Add(quota);
            await _db.SaveChangesAsync(cancellationToken);
        }
        if (quota.UsedCount >= quota.MonthlyLimit)
            throw new AuthBusinessException(FaceScanErrorCodes.QuotaExceeded,
                "Đã hết hạn mức quét khuôn mặt trong tháng.", 429);

        // Load the ALREADY-uploaded photo's bytes via the existing authenticated storage
        // abstraction — no second upload, no public URL required.
        var file = await _db.Files.FirstOrDefaultAsync(f => f.FileId == photo.FileId, cancellationToken)
            ?? throw new NotFoundException("UploadedFile", photo.FileId);

        var settings = FaceDetectionProviderSettings.Parse(config.SettingsJson);
        var mime = (file.MimeType ?? string.Empty).Trim().ToLowerInvariant();

        var isGoogleDrive = string.Equals(file.StorageProvider, "GOOGLE_DRIVE", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(file.ExternalFileId);
        byte[] fileBytes;
        await using (var stream = (isGoogleDrive
                ? await _drive.DownloadAsync(file.ExternalFileId!, cancellationToken)
                : await _fileStorage.OpenReadAsync(file, cancellationToken))
            ?? throw new BusinessRuleException("Không đọc được ảnh để quét.", FaceScanErrorCodes.PhotoNotActive))
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            fileBytes = ms.ToArray();
        }

        if (fileBytes.Length > settings.MaxFileSizeMb * 1024L * 1024L)
            throw new BusinessRuleException(
                $"Ảnh vượt quá kích thước tối đa {settings.MaxFileSizeMb} MB để quét khuôn mặt.",
                FaceScanErrorCodes.PhotoNotActive);

        var scan = new VisitPhotoFaceScan
        {
            VisitPhotoId = photo.VisitPhotoId,
            VisitRequestId = photo.VisitRequestId,
            VisitInstanceId = photo.VisitInstanceId,
            FileId = photo.FileId,
            ApiConfigId = config.ApiConfigId,
            Status = VisitPhotoFaceScan.StatusPending,
            ProviderName = FaceDetectionConstants.ProviderName,
            RequestedAt = now,
            RequestedBy = userId,
            CreatedAt = now,
        };
        _db.VisitPhotoFaceScans.Add(scan);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = "VISIT_PHOTO_FACE_SCAN_STARTED",
            EntityType = "VisitPhotoFaceScan",
            EntityId = scan.FaceScanId,
            VisitRequestId = scan.VisitRequestId,
            VisitInstanceId = scan.VisitInstanceId,
            CreatedAt = now,
        });

        scan.Status = VisitPhotoFaceScan.StatusProcessing;
        scan.UpdatedAt = _clock.VietnamNow;
        await _db.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        FaceDetectionProviderResult? result = null;
        string? errorCode = null;
        string? errorMessage = null;
        try
        {
            var credential = _credentialResolver.Resolve(config);
            if (string.IsNullOrEmpty(credential))
            {
                errorCode = ApiIntegrationErrorCodes.CredentialRequired;
                errorMessage = "Chưa cấu hình credential cho quét khuôn mặt.";
            }
            else
            {
                result = await _provider.DetectFacesAsync(
                    new FaceDetectionProviderInput { FileBytes = fileBytes, MimeType = mime },
                    new FaceDetectionRuntimeConfig
                    {
                        ApiConfigId = config.ApiConfigId,
                        Settings = settings,
                        CredentialJson = credential,
                        TimeoutSeconds = config.TimeoutSeconds,
                    },
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Keep messages provider-level only; no request payloads/credentials ever logged.
            errorCode = "FACE_SCAN_PROVIDER_FAILED";
            errorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        stopwatch.Stop();

        var processedAt = _clock.VietnamNow;
        if (result is not null)
        {
            scan.Status = VisitPhotoFaceScan.StatusSucceeded;
            scan.ProviderRequestId = result.ProviderRequestId;
            scan.ImageWidth = result.ImageWidth;
            scan.ImageHeight = result.ImageHeight;
            scan.DetectedFaceCount = (uint)result.Faces.Count;
            scan.CompletedAt = processedAt;
            scan.UpdatedAt = processedAt;

            uint index = 1;
            foreach (var face in result.Faces)
            {
                _db.VisitPhotoFaceDetections.Add(new VisitPhotoFaceDetection
                {
                    FaceScanId = scan.FaceScanId,
                    VisitRequestId = scan.VisitRequestId,
                    VisitInstanceId = scan.VisitInstanceId,
                    FileId = scan.FileId,
                    FaceIndex = index++,
                    BoundingBoxX = face.BoundingBoxX,
                    BoundingBoxY = face.BoundingBoxY,
                    BoundingBoxWidth = face.BoundingBoxWidth,
                    BoundingBoxHeight = face.BoundingBoxHeight,
                    DetectionConfidence = face.DetectionConfidence,
                    ReviewStatus = VisitPhotoFaceDetection.ReviewStatusDetected,
                    CreatedAt = processedAt,
                });
            }
        }
        else
        {
            scan.Status = VisitPhotoFaceScan.StatusFailed;
            scan.ErrorCode = errorCode;
            scan.ErrorMessage = errorMessage;
            scan.CompletedAt = processedAt;
            scan.UpdatedAt = processedAt;
        }

        // Sanitized request log for every cloud attempt (never the image bytes/raw response).
        _db.ApiRequestLogs.Add(new ApiRequestLog
        {
            ApiConfigId = config.ApiConfigId,
            RequestedBy = userId,
            RelatedType = "VISIT_PHOTO_FACE_SCAN",
            RelatedId = scan.FaceScanId,
            Endpoint = $"{config.BaseUrl}/v1/images:annotate",
            Method = "POST",
            HttpStatus = result is not null ? 200 : null,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            RequestSizeBytes = fileBytes.Length,
            Success = result is not null,
            ErrorCode = result is null ? errorCode : null,
            ErrorMessage = result is null ? errorMessage : null,
            CreatedAt = processedAt,
        });

        // Count the cloud call against the monthly quota (success or failure — both billable attempts).
        quota.UsedCount += 1;
        quota.LastUsedAt = processedAt;
        quota.UpdatedAt = processedAt;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = result is not null ? "VISIT_PHOTO_FACE_SCAN_SUCCEEDED" : "VISIT_PHOTO_FACE_SCAN_FAILED",
            EntityType = "VisitPhotoFaceScan",
            EntityId = scan.FaceScanId,
            VisitRequestId = scan.VisitRequestId,
            VisitInstanceId = scan.VisitInstanceId,
            Reason = result is not null
                ? $"detectedFaceCount={scan.DetectedFaceCount}"
                : $"errorCode={errorCode}",
            CreatedAt = processedAt,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return await FaceScanMapper.ToDtoAsync(_db, scan.FaceScanId, cancellationToken);
    }
}
