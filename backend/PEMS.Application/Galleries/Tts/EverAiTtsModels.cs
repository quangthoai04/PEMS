using System.Text.Json.Serialization;

namespace PEMS.Application.Galleries.Tts;

/// <summary>Body of <c>POST /tts</c> (EverAI create request). Snake_case per the EverAI contract.</summary>
public sealed class EverAiCreateTtsRequest
{
    [JsonPropertyName("response_type")]
    public string ResponseType { get; init; } = "indirect";

    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; init; }

    [JsonPropertyName("input_text")]
    public string InputText { get; init; } = string.Empty;

    [JsonPropertyName("voice_code")]
    public string VoiceCode { get; init; } = "vi_female_hoaian_mb";

    [JsonPropertyName("audio_type")]
    public string AudioType { get; init; } = "mp3";

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; init; } = 128;

    [JsonPropertyName("speed_rate")]
    public decimal SpeedRate { get; init; } = 1.0m;

    [JsonPropertyName("pitch_rate")]
    public decimal PitchRate { get; init; } = 1.0m;

    [JsonPropertyName("volume")]
    public int Volume { get; init; } = 100;
}

/// <summary>Root of every EverAI response: <c>status</c> 1 = ok, 0 = error (+error_code/message).</summary>
public sealed class EverAiCreateTtsResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("result")]
    public EverAiTtsRequestResult? Result { get; init; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

public sealed class EverAiGetTtsResponse
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("result")]
    public EverAiTtsRequestResult? Result { get; init; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// <c>result</c> payload of create/get. Note the nested <c>status</c> is a STRING lifecycle value
/// ("new" / "processing" / "done" / "failed"), unlike the int at the response root.
/// </summary>
public sealed class EverAiTtsRequestResult
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }

    [JsonPropertyName("characters")]
    public int? Characters { get; init; }

    [JsonPropertyName("voice_code")]
    public string? VoiceCode { get; init; }

    [JsonPropertyName("audio_type")]
    public string? AudioType { get; init; }

    [JsonPropertyName("progress")]
    public decimal? Progress { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("audio_link")]
    public string? AudioLink { get; init; }

    [JsonPropertyName("audio_expired")]
    public bool? AudioExpired { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

/// <summary>Payload EverAI POSTs to our callback endpoint. <c>status</c> is SUCCESS / FAILURE.</summary>
public sealed class EverAiTtsCallbackDto
{
    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }

    [JsonPropertyName("characters")]
    public int? Characters { get; init; }

    [JsonPropertyName("voice_code")]
    public string? VoiceCode { get; init; }

    [JsonPropertyName("audio_type")]
    public string? AudioType { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("audio_link")]
    public string? AudioLink { get; init; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

/// <summary>Bytes + content type of an audio file downloaded from an EverAI temporary link.</summary>
public sealed record EverAiAudioDownload(byte[] Content, string? ContentType);
