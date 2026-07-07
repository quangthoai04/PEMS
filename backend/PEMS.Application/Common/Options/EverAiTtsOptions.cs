namespace PEMS.Application.Common.Options;

/// <summary>
/// Configuration bound from the <c>"EverAiTts"</c> section (see <c>appsettings.Development.json</c>).
/// Drives the EverAI Text-To-Speech integration that narrates <c>gallery_items.description</c> for the
/// public VisitFPTU Gallery. The ApiKey is a secret — it must never be logged, echoed in responses or
/// committed inside <c>appsettings.Development.json</c>.
/// </summary>
public sealed class EverAiTtsOptions
{
    public const string SectionName = "EverAiTts";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://www.everai.vn/api/v1";
    public string ApiKey { get; set; } = string.Empty;

    public string DefaultVoiceCode { get; set; } = "vi_female_hoaian_mb";
    public string DefaultAudioType { get; set; } = "mp3";
    public int DefaultBitrate { get; set; } = 128;
    public decimal DefaultSpeedRate { get; set; } = 1.0m;
    public decimal DefaultPitchRate { get; set; } = 1.0m;
    public int DefaultVolume { get; set; } = 100;

    /// <summary>Hard cap for the narrated description (BR: 1000 chars) — protects EverAI credits.</summary>
    public int MaxInputCharacters { get; set; } = 1000;

    /// <summary>Minutes the public lazy-generate must wait after a FAILED attempt (manual regenerate bypasses).</summary>
    public int FailedCooldownMinutes { get; set; } = 30;

    /// <summary>When true EverAI pushes the result to <see cref="CallbackUrl"/>; when false the worker polls.</summary>
    public bool UseCallback { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 3;
    public int PollingMaxAttempts { get; set; } = 20;

    /// <summary>True only when the integration is fully usable (enabled + ApiKey present).</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
