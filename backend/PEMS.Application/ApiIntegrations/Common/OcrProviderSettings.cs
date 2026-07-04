using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PEMS.Application.ApiIntegrations.Common;

/// <summary>Typed view over api_configurations.settings_json for the Document AI provider.</summary>
public sealed class OcrProviderSettings
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = "us";

    [JsonPropertyName("processor_id")]
    public string ProcessorId { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "us-documentai.googleapis.com";

    [JsonPropertyName("max_file_size_mb")]
    public int MaxFileSizeMb { get; set; } = 10;

    [JsonPropertyName("allowed_mime_types")]
    public List<string> AllowedMimeTypes { get; set; } = new()
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf",
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static OcrProviderSettings Parse(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return new OcrProviderSettings();
        try
        {
            return JsonSerializer.Deserialize<OcrProviderSettings>(settingsJson, Options) ?? new OcrProviderSettings();
        }
        catch (JsonException)
        {
            return new OcrProviderSettings();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Options);
}

/// <summary>
/// Everything Infrastructure needs to call Google Document AI for one request.
/// CredentialJson is the RAW service account JSON — resolved just-in-time, never logged.
/// </summary>
public sealed class OcrProviderRuntimeConfig
{
    public ulong ApiConfigId { get; set; }
    public OcrProviderSettings Settings { get; set; } = new();
    public string CredentialJson { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
}
