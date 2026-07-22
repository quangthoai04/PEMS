using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>Typed view over api_configurations.settings_json for the Google Vision face-detection provider.</summary>
public sealed class FaceDetectionProviderSettings
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "vision.googleapis.com";

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("feature_type")]
    public string FeatureType { get; set; } = "FACE_DETECTION";

    [JsonPropertyName("max_faces_per_image")]
    public int MaxFacesPerImage { get; set; } = 50;

    [JsonPropertyName("max_file_size_mb")]
    public int MaxFileSizeMb { get; set; } = 8;

    [JsonPropertyName("allowed_mime_types")]
    public List<string> AllowedMimeTypes { get; set; } = new()
    {
        "image/jpeg", "image/png", "image/webp",
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static FaceDetectionProviderSettings Parse(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return new FaceDetectionProviderSettings();
        try
        {
            return JsonSerializer.Deserialize<FaceDetectionProviderSettings>(settingsJson, Options)
                   ?? new FaceDetectionProviderSettings();
        }
        catch (JsonException)
        {
            return new FaceDetectionProviderSettings();
        }
    }
}

/// <summary>
/// Everything Infrastructure needs to call Google Cloud Vision for one request. CredentialJson is
/// the RAW service account JSON — resolved just-in-time, never logged.
/// </summary>
public sealed class FaceDetectionRuntimeConfig
{
    public ulong ApiConfigId { get; set; }
    public FaceDetectionProviderSettings Settings { get; set; } = new();
    public string CredentialJson { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
