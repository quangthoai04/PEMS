using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.ApiIntegrations;

namespace PEMS.Application.ApiIntegrations.Common;

public static class ApiIntegrationMapper
{
    /// <summary>Purposes whose lifecycle (edit/test/enable/quota) is managed through this console.</summary>
    private static readonly string[] DatabaseManagedPurposes =
    {
        BusinessCardOcrConstants.Purpose,
        NewsTranslationConstants.Purpose,
    };

    public static ApiIntegrationDto ToDto(ApiConfiguration config, ICurrentUserService? currentUser = null)
    {
        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        var isDbManaged = config.Purpose != null && DatabaseManagedPurposes.Contains(config.Purpose);
        // Capabilities mirror the command-side gates: only ADMIN, and only the two
        // DB-managed purposes. Everything else (SMTP, Drive, env-configured providers)
        // is surfaced read-only.
        var manageable = isDbManaged && currentUser != null && ApiIntegrationAccess.CanManage(currentUser);
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
            ManagementSource = isDbManaged ? "DATABASE" : "ENVIRONMENT",
            CanEdit = manageable,
            CanTest = manageable,
            CanToggleStatus = manageable,
            CanConfigureQuota = manageable,
        };
    }
}
