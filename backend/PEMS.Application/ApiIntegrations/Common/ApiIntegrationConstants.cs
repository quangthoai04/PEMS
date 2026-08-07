namespace PEMS.Application.ApiIntegrations.Common;

public static class BusinessCardOcrConstants
{
    public const string ApiCode = "BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI";
    public const string ProviderName = "GOOGLE_DOCUMENT_AI";
    public const string Purpose = "BUSINESS_CARD_OCR";
    /// <summary>Default env/secret-manager reference for the service account.</summary>
    public const string DefaultSecretRef = "GOOGLE_DOCUMENT_AI_SERVICE_ACCOUNT";
}

public static class NewsTranslationConstants
{
    /// <summary>api_configurations.api_code of the Google Cloud Translation config row.</summary>
    public const string ApiCode = "NEWS_TRANSLATION_GOOGLE_CLOUD";
    public const string ProviderName = "GOOGLE_CLOUD_TRANSLATION";
    public const string Purpose = "NEWS_TRANSLATION";
    /// <summary>Default env/secret-manager reference for the service account.</summary>
    public const string DefaultSecretRef = "GOOGLE_TRANSLATION_SERVICE_ACCOUNT";
    /// <summary>Env fallback for the Google Cloud project when no DB config row exists.</summary>
    public const string ProjectIdEnvVar = "GOOGLE_TRANSLATION_PROJECT_ID";
}

public static class FaceDetectionConstants
{
    /// <summary>api_configurations.api_code of the Google Cloud Vision face-detection config row.</summary>
    public const string ApiCode = "FACE_DETECTION_GOOGLE_VISION";
    public const string ProviderName = "GOOGLE_CLOUD_VISION";
    public const string Purpose = "FACE_DETECTION";
    /// <summary>Default env/secret-manager reference for the service account.</summary>
    public const string DefaultSecretRef = "GOOGLE_VISION_SERVICE_ACCOUNT";
}

/// <summary>
/// The well-known <c>api_configurations</c> row that holds the Google Drive OAuth refresh token.
///
/// <para>
/// <see cref="ApiCode"/> — not <c>purpose</c> — is the identity used everywhere, because that is what the
/// canonical seed pins (<c>uq_api_config_code</c>) and what the row already in every deployment carries.
/// The seed's <c>purpose</c> column for this row holds a human sentence ("Lưu metadata và file business
/// của documents/gallery/news."), not a machine value, so matching on it would find nothing. New rows this
/// module creates get <see cref="Purpose"/> so the column is meaningful going forward; an existing row's
/// description is left alone rather than rewritten under an operator.
/// </para>
/// </summary>
public static class GoogleDriveIntegrationConstants
{
    public const string ApiCode = "GOOGLE_DRIVE_STORAGE";
    public const string ProviderName = "Google Drive";
    public const string Purpose = "GOOGLE_DRIVE_STORAGE";
    public const string Name = "Google Drive Storage";
    public const string BaseUrl = "https://www.googleapis.com/drive/v3";
    public const string TokenUrl = "https://oauth2.googleapis.com/token";
    public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    /// <summary>
    /// Full Drive scope. Matches what the existing refresh token was minted with — narrowing it here would
    /// silently reduce what a reconnect can do compared with the token it replaces.
    /// </summary>
    public const string Scope = "https://www.googleapis.com/auth/drive";

    /// <summary>Audit <c>action</c> values. First-ever connect and a re-consent are told apart on purpose.</summary>
    public const string AuditConnect = "CONNECT_GOOGLE_DRIVE";
    public const string AuditReconnect = "RECONNECT_GOOGLE_DRIVE";
    public const string AuditDisconnect = "DISCONNECT_GOOGLE_DRIVE";
    public const string AuditEntityType = "ApiConfiguration";
}

public static class ApiIntegrationStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
    public const string Disabled = "DISABLED";
}

public static class ApiIntegrationErrorCodes
{
    public const string Forbidden = "API_INTEGRATION_FORBIDDEN";
    public const string NotFound = "API_INTEGRATION_NOT_FOUND";
    public const string CodeDuplicated = "API_INTEGRATION_CODE_DUPLICATED";
    public const string CredentialRequired = "API_INTEGRATION_CREDENTIAL_REQUIRED";
    public const string TestRequiredBeforeEnable = "API_INTEGRATION_TEST_REQUIRED";
    public const string InvalidPurpose = "API_INTEGRATION_INVALID_PURPOSE";
}
