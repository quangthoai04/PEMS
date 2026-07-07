namespace PEMS.Application.Galleries.Tts;

/// <summary>gallery_item_tts_audios.status values (DB ENUM).</summary>
public static class GalleryTtsJobStatuses
{
    public const string Pending = "PENDING";
    public const string Submitted = "SUBMITTED";
    public const string Processing = "PROCESSING";
    public const string Ready = "READY";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>gallery_item_tts_audios.trigger_source values (DB ENUM). No BACKFILL — out of scope.</summary>
public static class TtsTriggerSources
{
    /// <summary>Fired automatically after a Staff Leader creates/edits a gallery item.</summary>
    public const string AutoGenerate = "AUTO_GENERATE";

    /// <summary>Fired when a public visitor presses the speaker icon and no current audio exists.</summary>
    public const string LazyGenerate = "LAZY_GENERATE";

    /// <summary>Fired by the Staff Leader "Tạo lại audio" action (bypasses the failed cooldown).</summary>
    public const string ManualRegenerate = "MANUAL_REGENERATE";
}

/// <summary>
/// API-facing status of a gallery item's narration audio (ensure / poll endpoints). The public ensure
/// contract only surfaces READY / PROCESSING / TEMPORARILY_UNAVAILABLE — the controller folds
/// DISABLED / INVALID_DESCRIPTION into TEMPORARILY_UNAVAILABLE; the poll endpoint returns all of them.
/// </summary>
public static class TtsAudioStatuses
{
    public const string Ready = "READY";
    public const string Processing = "PROCESSING";
    public const string NotCreated = "NOT_CREATED";
    public const string TemporarilyUnavailable = "TEMPORARILY_UNAVAILABLE";
    public const string Disabled = "DISABLED";
    public const string InvalidDescription = "INVALID_DESCRIPTION";

    /// <summary>Regenerate no-op: the current description already has matching READY audio.</summary>
    public const string UpToDate = "UP_TO_DATE";
}

/// <summary>
/// Management-dashboard status of an item's narration. Superset of the public statuses with STALE
/// ("Cần tạo lại": an older READY audio exists but the description/settings changed).
/// </summary>
public static class TtsManagementStatuses
{
    public const string Ready = "READY";
    public const string Processing = "PROCESSING";
    public const string Failed = "FAILED";
    public const string Stale = "STALE";
    public const string NotCreated = "NOT_CREATED";
    public const string Disabled = "DISABLED";
    public const string InvalidDescription = "INVALID_DESCRIPTION";
}

/// <summary>Stable internal error codes stored in gallery_item_tts_audios.error_code / thrown to clients.</summary>
public static class GalleryTtsErrorCodes
{
    public const string Disabled = "TTS_DISABLED";
    public const string ConfigMissing = "TTS_CONFIG_MISSING";
    public const string InvalidDescription = "TTS_INVALID_DESCRIPTION";
    public const string JobAlreadyRunning = "TTS_JOB_ALREADY_RUNNING";
    public const string RecentFailureCooldown = "TTS_RECENT_FAILURE_COOLDOWN";
    public const string EverAiAuthFailed = "TTS_EVERAI_AUTH_FAILED";
    public const string EverAiRequestFailed = "TTS_EVERAI_REQUEST_FAILED";
    public const string EverAiAudioNotReady = "TTS_EVERAI_AUDIO_NOT_READY";
    public const string EverAiAudioExpired = "TTS_EVERAI_AUDIO_EXPIRED";
    public const string AudioDownloadFailed = "TTS_AUDIO_DOWNLOAD_FAILED";
    public const string AudioUploadFailed = "TTS_AUDIO_UPLOAD_FAILED";
    public const string AudioNotFound = "TTS_AUDIO_NOT_FOUND";
}
