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
        FaceDetectionConstants.Purpose,
        ResendEmailConstants.Purpose,
    };

    public static ApiIntegrationDto ToDto(ApiConfiguration config, ICurrentUserService? currentUser = null)
    {
        var settings = OcrProviderSettings.Parse(config.SettingsJson);
        var resendSettings = config.Purpose == ResendEmailConstants.Purpose
            ? ResendProviderSettings.Parse(config.SettingsJson)
            : null;

        // Google Drive is identified by api_code, not purpose: the seeded row's purpose column holds a
        // human description rather than a machine value (see GoogleDriveIntegrationConstants).
        var isGoogleDrive = config.ApiCode == GoogleDriveIntegrationConstants.ApiCode;
        var isDbManaged = config.Purpose != null && DatabaseManagedPurposes.Contains(config.Purpose);

        // Capabilities mirror the command-side gates: only ADMIN, and managed purposes.
        // Everything else (SMTP, env-configured providers) is surfaced read-only.
        var isAdmin = currentUser != null && ApiIntegrationAccess.CanManage(currentUser);
        var manageable = isDbManaged && isAdmin;

        var hasCredential = !string.IsNullOrEmpty(config.CredentialsJsonEncrypted)
                            || !string.IsNullOrEmpty(config.BearerTokenEncrypted);

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
            HasCredential = hasCredential,
            SecretRef = config.SecretRef,
            ProjectId = settings.ProjectId,
            Location = settings.Location,
            ProcessorId = settings.ProcessorId,
            Endpoint = settings.Endpoint,
            FromEmail = resendSettings?.FromEmail,
            FromName = resendSettings?.FromName,
            ReplyToEmail = resendSettings?.ReplyToEmail,
            ReplyToName = resendSettings?.ReplyToName,
            MaxFileSizeMb = settings.MaxFileSizeMb,
            AllowedMimeTypes = settings.AllowedMimeTypes,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,

            ManagementSource = isGoogleDrive ? "HYBRID" : isDbManaged ? "DATABASE" : "ENVIRONMENT",

            // Drive is never edited through the generic provider form: it has no project/processor/endpoint
            // to fill in, and its one editable secret is obtained by consent, not by typing.
            CanEdit = manageable,
            CanTest = manageable || (isGoogleDrive && isAdmin),
            // Enable/disable and quota belong to configuration this console does not own for Drive: the
            // integration is switched on by GoogleDrive:Enabled on the server, and its limits are Google's.
            CanToggleStatus = manageable,
            CanConfigureQuota = manageable,
            CanConnectOAuth = isGoogleDrive && isAdmin,
            CanDisconnectOAuth = isGoogleDrive && isAdmin && !string.IsNullOrEmpty(config.CredentialsJsonEncrypted),

            CredentialStatus = !hasCredential
                ? ApiIntegrationCredentialStatuses.NotConfigured
                : config.LastTestStatus == "FAILED"
                    ? ApiIntegrationCredentialStatuses.Error
                    : ApiIntegrationCredentialStatuses.Connected,
        };
    }
}
