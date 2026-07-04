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

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleDocumentAiOcrConfig;

public sealed class UpsertGoogleDocumentAiOcrConfigCommandHandler
    : IRequestHandler<UpsertGoogleDocumentAiOcrConfigCommand, ApiIntegrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISecretProtector _secretProtector;

    public UpsertGoogleDocumentAiOcrConfigCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IDateTimeService clock, ISecretProtector secretProtector)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _secretProtector = secretProtector;
    }

    public async Task<ApiIntegrationDto> Handle(
        UpsertGoogleDocumentAiOcrConfigCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var endpoint = request.Endpoint.Trim()
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        var baseUrl = $"https://{endpoint}";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            throw new BusinessRuleException("base_url phải là HTTPS.", "API_INTEGRATION_HTTPS_REQUIRED");

        ApiConfiguration? config;
        if (request.ApiConfigId is { } id)
        {
            config = await _db.ApiConfigurations
                .FirstOrDefaultAsync(c => c.ApiConfigId == id && c.DeletedAt == null, cancellationToken)
                ?? throw new NotFoundException("ApiConfiguration", id);

            if (config.Purpose != BusinessCardOcrConstants.Purpose
                || config.ProviderName != BusinessCardOcrConstants.ProviderName)
                throw new BusinessRuleException(
                    "Chỉ được cập nhật cấu hình BUSINESS_CARD_OCR / GOOGLE_DOCUMENT_AI qua endpoint này.",
                    ApiIntegrationErrorCodes.InvalidPurpose);
        }
        else
        {
            // Create-or-update the well-known BUSINESS_CARD_OCR config (api_code is unique).
            config = await _db.ApiConfigurations
                .FirstOrDefaultAsync(c => c.ApiCode == BusinessCardOcrConstants.ApiCode && c.DeletedAt == null,
                    cancellationToken);
        }

        var now = _clock.UtcNow;
        var isNew = config is null;
        if (config is null)
        {
            config = new ApiConfiguration
            {
                ApiCode = BusinessCardOcrConstants.ApiCode,
                ProviderName = BusinessCardOcrConstants.ProviderName,
                Purpose = BusinessCardOcrConstants.Purpose,
                DefaultMethod = "POST",
                AuthType = "CUSTOM",
                RetryEnabled = true,
                MaxRetries = 2,
                Status = ApiIntegrationStatuses.Inactive,
                DataSensitivity = "CONFIDENTIAL",
                AllowsProviderTraining = false,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId,
            };
            _db.ApiConfigurations.Add(config);
        }

        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        settings.ProjectId = request.ProjectId.Trim();
        settings.Location = request.Location.Trim();
        settings.ProcessorId = request.ProcessorId.Trim();
        settings.Endpoint = endpoint;

        config.Name = request.Name.Trim();
        config.BaseUrl = baseUrl;
        config.SettingsJson = settings.ToJson();
        config.RateLimitPerMinute = request.RateLimitPerMinute;
        config.MonthlyQuota = request.MonthlyQuota;
        config.TimeoutSeconds = request.TimeoutSeconds;
        config.RetentionDays = request.RetentionDays;
        config.SecretRef = string.IsNullOrWhiteSpace(request.SecretRef) ? config.SecretRef : request.SecretRef.Trim();
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(request.ServiceAccountJson))
        {
            // Validate that the payload at least parses as a service account JSON.
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
