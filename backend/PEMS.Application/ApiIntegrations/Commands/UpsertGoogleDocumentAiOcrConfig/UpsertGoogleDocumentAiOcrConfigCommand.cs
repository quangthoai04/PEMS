using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleDocumentAiOcrConfig;

/// <summary>
/// POST /api/api-integrations/business-card-ocr/google-document-ai (ApiConfigId null → create/update by api_code)
/// PUT  /api/api-integrations/{apiConfigId} (ApiConfigId set → update)
/// ServiceAccountJson, when provided, is encrypted into credentials_json_encrypted and never returned.
/// </summary>
public sealed class UpsertGoogleDocumentAiOcrConfigCommand : IRequest<ApiIntegrationDto>
{
    public ulong? ApiConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us";
    public string ProcessorId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "us-documentai.googleapis.com";
    /// <summary>Raw service-account JSON — stored encrypted. Omit to keep the current credential.</summary>
    public string? ServiceAccountJson { get; set; }
    public string? SecretRef { get; set; }
    public uint RateLimitPerMinute { get; set; } = 20;
    public uint MonthlyQuota { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 60;
    public uint RetentionDays { get; set; } = 30;
}
