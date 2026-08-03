using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.ApiIntegrations.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertResendConfig;

public sealed class UpsertResendConfigCommandHandler
    : IRequestHandler<UpsertResendConfigCommand, ApiIntegrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ISecretProtector _secretProtector;

    public UpsertResendConfigCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        ISecretProtector secretProtector)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _secretProtector = secretProtector;
    }

    public async Task<ApiIntegrationDto> Handle(
        UpsertResendConfigCommand request, CancellationToken cancellationToken)
    {
        ApiIntegrationAccess.EnsureManage(_currentUser);

        var config = await _db.ApiConfigurations
            .FirstOrDefaultAsync(c => c.ApiCode == ResendEmailConstants.ApiCode && c.DeletedAt == null, cancellationToken);

        var now = _clock.VietnamNow;
        var isNew = config is null;

        if (config is null)
        {
            config = new ApiConfiguration
            {
                ApiCode = ResendEmailConstants.ApiCode,
                ProviderName = ResendEmailConstants.ProviderName,
                Purpose = ResendEmailConstants.Purpose,
                BaseUrl = ResendEmailConstants.BaseUrl,
                DefaultMethod = "POST",
                AuthType = ResendEmailConstants.AuthType,
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

        var settings = new ResendProviderSettings
        {
            FromEmail = request.FromEmail.Trim(),
            FromName = request.FromName.Trim(),
            ReplyToEmail = string.IsNullOrWhiteSpace(request.ReplyToEmail) ? null : request.ReplyToEmail.Trim(),
            ReplyToName = string.IsNullOrWhiteSpace(request.ReplyToName) ? null : request.ReplyToName.Trim(),
        };

        config.Name = request.Name.Trim();
        config.BaseUrl = ResendEmailConstants.BaseUrl;
        config.SettingsJson = settings.ToJson();
        config.RateLimitPerMinute = request.RateLimitPerMinute;
        config.MonthlyQuota = request.MonthlyQuota;
        config.TimeoutSeconds = request.TimeoutSeconds;
        config.UpdatedAt = now;
        config.UpdatedBy = _currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            config.BearerTokenEncrypted = _secretProtector.Protect(request.ApiKey.Trim());
            // Credential changed -> reset connection test status
            config.LastTestStatus = null;
            config.LastTestedAt = null;
            config.LastTestMessage = null;
        }

        if (string.IsNullOrEmpty(config.BearerTokenEncrypted))
        {
            throw new BusinessRuleException(
                "Chưa cấu hình Resend API key. Vui lòng nhập API Key.",
                ApiIntegrationErrorCodes.CredentialRequired);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _currentUser.UserId,
            Action = isNew ? "CREATE_API_CONFIGURATION" : "UPDATE_API_CONFIGURATION",
            EntityType = "ApiConfiguration",
            EntityId = isNew ? null : config.ApiConfigId,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(cancellationToken);
        return ApiIntegrationMapper.ToDto(config, _currentUser);
    }
}
