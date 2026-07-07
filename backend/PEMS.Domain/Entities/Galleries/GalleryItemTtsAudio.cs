using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PEMS.Domain.Entities.Galleries;

/// <summary>
/// One EverAI TTS request/attempt for a gallery item's description (gallery_items 1 - n rows: FAILED
/// attempts, regenerates and edited descriptions all keep their history). The "current" public audio is
/// the newest READY row whose <see cref="SourceTextHash"/> matches the item's present description +
/// voice/audio settings. The DB's STORED generated column <c>running_key</c> (deliberately not mapped —
/// EF must never write it) uniquely blocks two concurrent running jobs for the same item+hash+config.
/// </summary>
[Table("gallery_item_tts_audios")]
public class GalleryItemTtsAudio
{
    [Key]
    [Column("tts_audio_id")]
    public ulong TtsAudioId { get; set; }

    [Column("gallery_item_id")]
    public ulong GalleryItemId { get; set; }

    /// <summary>SHA-256 (lowercase hex) of the normalized description + voice/audio settings.</summary>
    [Column("source_text_hash")]
    public string SourceTextHash { get; set; } = null!;

    /// <summary>Snapshot of the description at request time (what is actually sent to EverAI).</summary>
    [Column("source_text")]
    public string SourceText { get; set; } = null!;

    [Column("voice_code")]
    public string VoiceCode { get; set; } = null!;

    /// <summary>mp3 | wav.</summary>
    [Column("audio_type")]
    public string AudioType { get; set; } = "mp3";

    [Column("bitrate")]
    public int? Bitrate { get; set; }

    [Column("speed_rate")]
    public decimal SpeedRate { get; set; } = 1.0m;

    [Column("pitch_rate")]
    public decimal PitchRate { get; set; } = 1.0m;

    [Column("volume")]
    public int Volume { get; set; } = 100;

    /// <summary>PENDING | SUBMITTED | PROCESSING | READY | FAILED | CANCELLED.</summary>
    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("everai_request_id")]
    public string? EverAiRequestId { get; set; }

    /// <summary>EverAI's temporary audio link — internal download-once only, never served to clients.</summary>
    [Column("everai_audio_link")]
    public string? EverAiAudioLink { get; set; }

    /// <summary>files.file_id of the narration uploaded to Google Drive (folder gallery-audio).</summary>
    [Column("audio_file_id")]
    public ulong? AudioFileId { get; set; }

    /// <summary>AUTO_GENERATE | LAZY_GENERATE | MANUAL_REGENERATE.</summary>
    [Column("trigger_source")]
    public string TriggerSource { get; set; } = null!;

    [Column("characters")]
    public int? Characters { get; set; }

    [Column("progress")]
    public decimal? Progress { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [Column("processing_at")]
    public DateTime? ProcessingAt { get; set; }

    [Column("ready_at")]
    public DateTime? ReadyAt { get; set; }

    [Column("failed_at")]
    public DateTime? FailedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public virtual GalleryItem GalleryItem { get; set; } = null!;
}
