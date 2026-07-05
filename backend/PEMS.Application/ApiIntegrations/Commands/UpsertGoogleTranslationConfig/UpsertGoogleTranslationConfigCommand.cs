using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleTranslationConfig;

/// <summary>
/// POST /api/api-integrations/news-translation/google-cloud-translation
/// Create-or-update (by api_code) the Google Cloud Translation config used by the
/// News auto-translate feature. ServiceAccountJson, when provided, is encrypted into
/// credentials_json_encrypted and never returned.
/// </summary>
public sealed class UpsertGoogleTranslationConfigCommand : IRequest<ApiIntegrationDto>
{
    public string Name { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    /// <summary>Cloud Translation location — "global" unless a regional endpoint is required.</summary>
    public string Location { get; set; } = "global";
    /// <summary>Raw service-account JSON — stored encrypted. Omit to keep the current credential.</summary>
    public string? ServiceAccountJson { get; set; }
    public string? SecretRef { get; set; }
    public uint RateLimitPerMinute { get; set; } = 60;
    public uint MonthlyQuota { get; set; } = 10000;
    public int TimeoutSeconds { get; set; } = 30;
}
