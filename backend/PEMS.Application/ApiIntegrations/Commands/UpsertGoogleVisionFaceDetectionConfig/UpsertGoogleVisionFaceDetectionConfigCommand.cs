using MediatR;
using PEMS.Application.ApiIntegrations.Common;

namespace PEMS.Application.ApiIntegrations.Commands.UpsertGoogleVisionFaceDetectionConfig;

/// <summary>
/// POST /api/api-integrations/face-detection/google-cloud-vision
/// Create-or-update (by api_code) the Google Cloud Vision config used for Face Detection.
/// ServiceAccountJson, when provided, is encrypted into credentials_json_encrypted and never returned.
/// </summary>
public sealed class UpsertGoogleVisionFaceDetectionConfigCommand : IRequest<ApiIntegrationDto>
{
    public string Name { get; set; } = "Google Cloud Vision - Face Detection";
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us";
    public string Endpoint { get; set; } = "vision.googleapis.com";
    /// <summary>Raw service-account JSON — stored encrypted. Omit to keep the current credential.</summary>
    public string? ServiceAccountJson { get; set; }
    public string? SecretRef { get; set; }
    public uint RateLimitPerMinute { get; set; } = 20;
    public uint MonthlyQuota { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
}
