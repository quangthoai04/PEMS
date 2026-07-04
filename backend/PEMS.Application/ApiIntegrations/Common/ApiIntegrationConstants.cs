namespace PEMS.Application.ApiIntegrations.Common;

public static class BusinessCardOcrConstants
{
    public const string ApiCode = "BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI";
    public const string ProviderName = "GOOGLE_DOCUMENT_AI";
    public const string Purpose = "BUSINESS_CARD_OCR";
    /// <summary>Default env/secret-manager reference for the service account.</summary>
    public const string DefaultSecretRef = "GOOGLE_DOCUMENT_AI_SERVICE_ACCOUNT";
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
