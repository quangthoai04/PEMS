using System.Text.Json.Serialization;

namespace PEMS.Application.Galleries.Tts;

/// <summary>
/// API response for the TTS ensure / poll / regenerate endpoints. <c>Status</c> is one of
/// <see cref="TtsAudioStatuses"/>; <c>AudioUrl</c> is only present when READY and always points at a
/// PEMS-served route (never EverAI's temporary link).
/// </summary>
public sealed class GalleryItemTtsAudioResponse
{
    public string Status { get; init; } = TtsAudioStatuses.NotCreated;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AudioUrl { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VoiceCode { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AudioType { get; init; }
}

/// <summary>User-facing Vietnamese messages of the public TTS endpoints (single source).</summary>
public static class GalleryTtsMessages
{
    public const string Processing = "Giọng đọc đang được tạo, vui lòng chờ trong giây lát.";
    public const string TemporarilyUnavailable = "Chưa thể tạo giọng đọc cho nội dung này. Vui lòng thử lại sau.";
    public const string Regenerating = "Giọng đọc đang được tạo lại.";
    public const string UpToDate = "Giọng đọc đã là bản mới nhất cho mô tả hiện tại, không cần tạo lại.";
}
