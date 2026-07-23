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
