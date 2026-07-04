using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.ApiIntegrations.Common;

public static class ApiIntegrationMapper
{
    public static ApiIntegrationDto ToDto(ApiConfiguration config)
    {
        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        return new ApiIntegrationDto
        {
            ApiConfigId = config.ApiConfigId,
            ApiCode = config.ApiCode,
            Name = config.Name,
            ProviderName = config.ProviderName,
            Purpose = config.Purpose,
            BaseUrl = config.BaseUrl,
            Status = config.Status,
            DataSensitivity = config.DataSensitivity,
            AllowsProviderTraining = config.AllowsProviderTraining,
            RetentionDays = config.RetentionDays,
            RateLimitPerMinute = config.RateLimitPerMinute,
            MonthlyQuota = config.MonthlyQuota,
            TimeoutSeconds = config.TimeoutSeconds,
            LastTestStatus = config.LastTestStatus,
            LastTestedAt = config.LastTestedAt,
            LastTestMessage = config.LastTestMessage,
            HasCredential = !string.IsNullOrEmpty(config.CredentialsJsonEncrypted),
            SecretRef = config.SecretRef,
            ProjectId = settings.ProjectId,
            Location = settings.Location,
            ProcessorId = settings.ProcessorId,
            Endpoint = settings.Endpoint,
            MaxFileSizeMb = settings.MaxFileSizeMb,
            AllowedMimeTypes = settings.AllowedMimeTypes,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
        };
    }
}
