using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.BusinessCardOcr.Common;
using PEMS.Application.BusinessCardOcr.Services;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Application.Partners.VisitLinks.Common;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.BusinessCardOcr.Commands.ScanBusinessCard;

public sealed class ScanBusinessCardCommandHandler
    : IRequestHandler<ScanBusinessCardCommand, BusinessCardOcrJobDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IFileUploadService _fileUpload;
    private readonly IBusinessCardOcrProvider _provider;
    private readonly IOcrCredentialResolver _credentialResolver;
    private readonly IBusinessCardOcrThrottle _throttle;
    private readonly ISecretProtector _secretProtector;

    public ScanBusinessCardCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IFileUploadService fileUpload,
        IBusinessCardOcrProvider provider,
        IOcrCredentialResolver credentialResolver,
        IBusinessCardOcrThrottle throttle,
        ISecretProtector secretProtector)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _fileUpload = fileUpload;
        _provider = provider;
        _credentialResolver = credentialResolver;
        _throttle = throttle;
        _secretProtector = secretProtector;
    }

    public async Task<BusinessCardOcrJobDto> Handle(
        ScanBusinessCardCommand request, CancellationToken cancellationToken)
    {
        // 1–2) Auth + role: only IC Staff / Staff Leader scan business cards.
        if (_currentUser.UserId is not { } userId
            || !(PartnerAccess.IsStaff(_currentUser) || PartnerAccess.IsStaffLeader(_currentUser)))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.Forbidden,
                "Chỉ IC Staff / Trưởng phòng IC mới được quét danh thiếp.", 403);

        // 3) Visit-context scope check when the scan comes from a meeting row.
        if (request.VisitInstanceId is { } visitInstanceId)
            await VisitLinkSupport.LoadInstanceWithAccessAsync(_db, _currentUser, visitInstanceId, cancellationToken);

        // 4) ACTIVE provider config.
        var config = await _db.ApiConfigurations.FirstOrDefaultAsync(
            c => c.ApiCode == BusinessCardOcrConstants.ApiCode && c.DeletedAt == null, cancellationToken)
            ?? throw new BusinessRuleException("Chưa có cấu hình OCR danh thiếp.",
                BusinessCardOcrErrorCodes.ConfigInactive);
        if (config.Status != ApiIntegrationStatuses.Active)
            throw new BusinessRuleException(
                "Cấu hình OCR danh thiếp đang tắt. Liên hệ quản trị viên để kích hoạt.",
                BusinessCardOcrErrorCodes.ConfigInactive);

        // Idempotency replay — return the previously created job without any cloud call.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey)
            && _throttle.TryGetIdempotentJob(userId, request.IdempotencyKey!, out var idempotentJobId))
        {
            var existingByKey = await _db.BusinessCardOcrJobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.OcrJobId == idempotentJobId, cancellationToken);
            if (existingByKey is not null) return ToDto(existingByKey);
        }

        // 5) Rate limits (per user / per IP) → HTTP 429.
        if (!_throttle.TryAcquireUser(userId))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.RateLimited,
                "Bạn quét quá nhanh. Vui lòng thử lại sau ít phút.", 429);
        if (!string.IsNullOrWhiteSpace(request.ClientIp) && !_throttle.TryAcquireIp(request.ClientIp!))
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.RateLimited,
                "Địa chỉ IP của bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau.", 429);

        var now = _clock.VietnamNow;
        var period = now.ToString("yyyyMM");

        // 6) Monthly quota → HTTP 429 without calling the cloud.
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
            throw new AuthBusinessException(BusinessCardOcrErrorCodes.QuotaExceeded,
                "Đã hết hạn mức quét danh thiếp trong tháng.", 429);

        // 7) File validation from settings_json.
        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        if (request.FileBytes.Length == 0)
            throw new BusinessRuleException("Tệp tải lên rỗng.", BusinessCardOcrErrorCodes.InvalidFile);
        if (request.FileBytes.Length > settings.MaxFileSizeMb * 1024L * 1024L)
            throw new BusinessRuleException(
                $"Tệp vượt quá kích thước tối đa {settings.MaxFileSizeMb} MB.",
                BusinessCardOcrErrorCodes.InvalidFile);
        var mime = (request.ContentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!settings.AllowedMimeTypes.Any(m => string.Equals(m, mime, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException(
                "Định dạng tệp không được hỗ trợ (chỉ JPEG/PNG/WebP/PDF).",
                BusinessCardOcrErrorCodes.InvalidFile);

        // 8–9) Same user + same file hash + recent SUCCEEDED job → return the old draft.
        var sha256 = Convert.ToHexString(SHA256.HashData(request.FileBytes)).ToLowerInvariant();
        var recentCutoff = now.AddHours(-24);
        var duplicate = await _db.BusinessCardOcrJobs.AsNoTracking()
            .Where(j => j.CreatedBy == userId
                        && j.FileSha256 == sha256
                        && j.Status == Domain.Entities.ApiIntegrations.BusinessCardOcrJob.StatusSucceeded
                        && j.CreatedAt >= recentCutoff)
            .OrderByDescending(j => j.OcrJobId)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                _throttle.RememberIdempotentJob(userId, request.IdempotencyKey!, duplicate.OcrJobId);
            return ToDto(duplicate);
        }

        // 10) Persist the card file to Google Drive (metadata row in files via the shared upload service).
        var fileName = string.IsNullOrWhiteSpace(request.FileName)
            ? "business-card"
            : Path.GetFileName(request.FileName.Trim());
        UploadedFileDto uploadedFile;
        await using (var ms = new MemoryStream(request.FileBytes, writable: false))
        {
            uploadedFile = await _fileUpload.UploadBusinessFileAsync(
                ms, fileName, mime, request.FileBytes.Length, FilePurpose.BusinessCard, (long)userId, cancellationToken);
        }

        // 11) OCR job row (PROCESSING).
        var job = new BusinessCardOcrJob
        {
            ScannedCardFileId = (ulong)uploadedFile.FileId,
            ApiConfigId = config.ApiConfigId,
            Status = BusinessCardOcrJob.StatusProcessing,
            ProviderName = BusinessCardOcrConstants.ProviderName,
            ProviderProcessorId = settings.ProcessorId,
            ProviderLocation = settings.Location,
            SourceVisitInstanceId = request.VisitInstanceId,
            SourceGuestMemberId = request.GuestMemberId,
            SourceMinuteParticipantId = request.MinuteParticipantId,
            FileSha256 = sha256,
            CreatedAt = now,
            CreatedBy = userId,
            ExpiresAt = config.RetentionDays is { } days ? now.AddDays(days) : null,
        };
        _db.BusinessCardOcrJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            _throttle.RememberIdempotentJob(userId, request.IdempotencyKey!, job.OcrJobId);

        // 12) Cloud call. Failures are recorded on the job + logs, never thrown raw.
        var stopwatch = Stopwatch.StartNew();
        BusinessCardOcrProviderResult? result = null;
        string? errorCode = null;
        string? errorMessage = null;
        try
        {
            var credential = _credentialResolver.Resolve(config);
            if (string.IsNullOrEmpty(credential))
            {
                errorCode = ApiIntegrationErrorCodes.CredentialRequired;
                errorMessage = "Chưa cấu hình credential cho OCR.";
            }
            else
            {
                result = await _provider.ExtractAsync(
                    new BusinessCardOcrProviderInput
                    {
                        FileBytes = request.FileBytes,
                        MimeType = mime,
                        FileName = fileName,
                    },
                    new OcrProviderRuntimeConfig
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
            errorCode = BusinessCardOcrErrorCodes.ProviderFailed;
            // Keep messages provider-level only; no request payloads/credentials.
            errorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        stopwatch.Stop();

        var processedAt = _clock.VietnamNow;
        if (result is not null)
        {
            job.Status = BusinessCardOcrJob.StatusSucceeded;
            job.ProviderRequestId = result.ProviderRequestId;
            job.RawTextEncrypted = string.IsNullOrEmpty(result.RawText) ? null : _secretProtector.Protect(result.RawText);
            job.ParsedJsonEncrypted = _secretProtector.Protect(JsonSerializer.Serialize(new
            {
                result.FullName, result.Email, result.Phone, result.JobTitle,
                result.DepartmentName, result.Organization, result.WebsiteUrl, result.Address,
            }));
            job.ParsedFullName = Truncate(result.FullName, 150);
            job.ParsedEmail = Truncate(result.Email, 150);
            job.ParsedPhone = Truncate(result.Phone, 50);
            job.ParsedJobTitle = Truncate(result.JobTitle, 150);
            job.ParsedDepartmentName = Truncate(result.DepartmentName, 150);
            job.ParsedOrganization = Truncate(result.Organization, 255);
            job.ParsedWebsiteUrl = Truncate(result.WebsiteUrl, 500);
            job.ParsedAddress = Truncate(result.Address, 500);
            job.ConfidenceScore = result.ConfidenceScore;
            job.ProcessedAt = processedAt;

            // 13) Partner match (skipped when a partner was preselected on the modal).
            if (request.PartnerId is { } preselected)
            {
                job.MatchedPartnerId = preselected;
            }
            else
            {
                var match = await PartnerMatcher.MatchAsync(
                    _db, result.Organization, result.Email, cancellationToken);
                if (match.PartnerId is { } matchedId && match.MatchStatus != "NONE")
                    job.MatchedPartnerId = matchedId;
            }
        }
        else
        {
            job.Status = BusinessCardOcrJob.StatusFailed;
            job.ErrorCode = errorCode;
            job.ErrorMessage = errorMessage;
            job.ProcessedAt = processedAt;
        }

        // 15) Sanitized request log for every cloud attempt.
        _db.ApiRequestLogs.Add(new ApiRequestLog
        {
            ApiConfigId = config.ApiConfigId,
            RequestedBy = userId,
            RelatedType = "BUSINESS_CARD_OCR_JOB",
            RelatedId = job.OcrJobId,
            Endpoint = $"{config.BaseUrl}/v1/.../processors/(process)",
            Method = "POST",
            HttpStatus = result is not null ? 200 : null,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds,
            RequestSizeBytes = request.FileBytes.Length,
            Success = result is not null,
            ErrorCode = result is null ? errorCode : null,
            ErrorMessage = result is null ? errorMessage : null,
            CreatedAt = processedAt,
        });

        // 16) Count the cloud call against the monthly quota (success or failure — both billable attempts).
        quota.UsedCount += 1;
        quota.LastUsedAt = processedAt;
        quota.UpdatedAt = processedAt;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(job);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private BusinessCardOcrJobDto ToDto(BusinessCardOcrJob job)
    {
        var dto = new BusinessCardOcrJobDto
        {
            OcrJobId = job.OcrJobId,
            Status = job.Status,
            ProviderName = job.ProviderName,
            ConfidenceScore = job.ConfidenceScore,
            ScannedCardFileId = job.ScannedCardFileId,
            ConfirmedPartnerId = job.ConfirmedPartnerId,
            ConfirmedContactId = job.ConfirmedContactId,
            SourceVisitInstanceId = job.SourceVisitInstanceId,
            SourceGuestMemberId = job.SourceGuestMemberId,
            SourceMinuteParticipantId = job.SourceMinuteParticipantId,
            ErrorMessage = job.ErrorMessage,
            CreatedAt = job.CreatedAt,
            ProcessedAt = job.ProcessedAt,
            Parsed = new BusinessCardOcrParsedDto
            {
                FullName = job.ParsedFullName,
                Email = job.ParsedEmail,
                Phone = job.ParsedPhone,
                JobTitle = job.ParsedJobTitle,
                DepartmentName = job.ParsedDepartmentName,
                Organization = job.ParsedOrganization,
                WebsiteUrl = job.ParsedWebsiteUrl,
                Address = job.ParsedAddress,
            },
        };

        if (job.MatchedPartnerId is { } matchedPartnerId)
        {
            var partner = _db.Partners.AsNoTracking()
                .Where(p => p.PartnerId == matchedPartnerId)
                .Select(p => new { p.Name, p.ProfileStatus })
                .FirstOrDefault();
            if (partner is not null)
            {
                dto.MatchedPartner = new BusinessCardOcrMatchedPartnerDto
                {
                    PartnerId = matchedPartnerId,
                    PartnerName = partner.Name,
                    ProfileStatus = partner.ProfileStatus,
                    Confidence = job.ConfidenceScore ?? 0,
                    Reason = "Matched by organization/email",
                };
            }
        }

        return dto;
    }
}
