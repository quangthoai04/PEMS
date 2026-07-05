using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleTranslationConfig;

public sealed class UpsertGoogleTranslationConfigCommandHandler
    : IRequestHandler<UpsertGoogleTranslationConfigCommand, ApiIntegrationDto>
{
    private const string TranslationBaseUrl = "https://translation.googleapis.com";
    private const string TranslationEndpoint = "translation.googleapis.com";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISecretProtector _secretProtector;

    public UpsertGoogleTranslationConfigCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IDateTimeService clock, ISecretProtector secretProtector)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _secretProtector = secretProtector;
    }

    public async Task<ApiIntegrationDto> Handle(
        UpsertGoogleTranslationConfigCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        // One well-known config row per purpose — create-or-update by api_code (unique).
        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiCode == NewsTranslationConstants.ApiCode && c.DeletedAt == null,
                cancellationToken);

        var now = _clock.UtcNow;
        var isNew = config is null;
        if (config is null)
        {
            config = new ApiConfiguration
            {
                ApiCode = NewsTranslationConstants.ApiCode,
                ProviderName = NewsTranslationConstants.ProviderName,
                Purpose = NewsTranslationConstants.Purpose,
                DefaultMethod = "POST",
                AuthType = "CUSTOM",
                RetryEnabled = true,
                MaxRetries = 2,
                Status = ApiIntegrationStatuses.Inactive,
                DataSensitivity = "INTERNAL", // news content is not confidential — it gets published
                AllowsProviderTraining = false,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            };
            _db.ApiConfigurations.Add(config);
        }

        // Reuse the shared settings_json shape (snake_case project_id/location) so the
        // admin list/detail mapper renders this config the same way as the OCR one.
        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        settings.ProjectId = request.ProjectId.Trim();
        settings.Location = string.IsNullOrWhiteSpace(request.Location) ? "global" : request.Location.Trim();
        settings.ProcessorId = string.Empty;
        settings.Endpoint = TranslationEndpoint;

        config.Name = request.Name.Trim();
        config.BaseUrl = TranslationBaseUrl;
        config.SettingsJson = settings.ToJson();
        config.RateLimitPerMinute = request.RateLimitPerMinute;
        config.MonthlyQuota = request.MonthlyQuota;
        config.TimeoutSeconds = request.TimeoutSeconds;
        config.SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? config.SecretRef : request.SecretRef.Trim();
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(request.ServiceAccountJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(request.ServiceAccountJson);
                if (!doc.RootElement.TryGetProperty("private_key", out _)
                    || !doc.RootElement.TryGetProperty("client_email", out _))
                    throw new BusinessRuleException(
                        "Service account JSON không hợp lệ (thiếu private_key/client_email).",
                        ApiIntegrationErrorCodes.CredentialRequired);
            }
            catch (JsonException)
            {
                throw new BusinessRuleException("Service account JSON không hợp lệ.",
                    ApiIntegrationErrorCodes.CredentialRequired);
            }

            config.CredentialsJsonEncrypted = _secretProtector.Protect(request.ServiceAccountJson);
            // Credential changed → the config must be re-tested before (re-)enabling.
            config.LastTestStatus = null;
            config.LastTestedAt = null;
            config.LastTestMessage = null;
        }

        if (string.IsNullOrEmpty(config.CredentialsJsonEncrypted) && string.IsNullOrWhiteSpace(config.SecretRef))
            throw new BusinessRuleException(
                "Phải cung cấp serviceAccountJson hoặc secretRef.",
                ApiIntegrationErrorCodes.CredentialRequired);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = isNew ? "CREATE_API_CONFIGURATION" : "UPDATE_API_CONFIGURATION",
            EntityType = "ApiConfiguration",
            EntityId = isNew ? null : config.ApiConfigId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return ApiIntegrationMapper.ToDto(config);
    }
}
