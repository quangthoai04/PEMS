namespace PEMS.Application.Galleries.Tts;

/// <summary>
/// Outcome of an ensure/status call. <see cref="Status"/> is one of <see cref="TtsAudioStatuses"/>;
/// when READY, <see cref="AudioFileId"/> points at the stored <c>files</c> row (callers build the URL
/// appropriate for their audience — public proxy vs authenticated /api/files).
/// </summary>
public sealed record GalleryItemTtsEnsureResult(
    string Status,
    long? AudioFileId = null,
    string? VoiceCode = null,
    string? AudioType = null);

/// <summary>
/// Richer status of an item's narration for the Staff Leader dashboard (see
/// <see cref="TtsManagementStatuses"/>). Unlike the public status it distinguishes STALE (an older
/// READY audio exists but the description/settings changed → hash no longer matches) from NOT_CREATED,
/// and exposes <see cref="CanRegenerate"/> so the "Tạo lại audio" button is disabled when the current
/// description already has matching READY audio (or a job is already running).
/// </summary>
public sealed record GalleryItemTtsManagementStatus(
    string Status,
    bool CanRegenerate,
    long? AudioFileId = null,
    string? VoiceCode = null,
    string? AudioType = null,
    string? ErrorMessage = null);

/// <summary>Item + its current description, fed to the batch management-status lookup (list view).</summary>
public sealed record GalleryTtsItemDescriptor(long GalleryItemId, string Description);

/// <summary>
/// Core service of the EverAI TTS integration for gallery items. One ensure path serves all three
/// triggers (AUTO_GENERATE on create/edit, LAZY_GENERATE on the public speaker icon,
/// MANUAL_REGENERATE from the dashboard); the job/callback methods run in the background worker /
/// webhook. Credit-protection rules (config gates, 1000-char cap, running-job dedupe, failed
/// cooldown) all live here so no caller can bypass them.
/// </summary>
public interface IGalleryItemTtsService
{
    /// <summary>
    /// Guarantees a narration exists (or is being generated) for the item's CURRENT description +
    /// settings. Returns READY (audio already matches), PROCESSING (job running or just created),
    /// TEMPORARILY_UNAVAILABLE (recent failure cooldown), DISABLED or INVALID_DESCRIPTION — it only
    /// throws for a missing / (when <paramref name="requirePublicVisible"/>) non-public item (404).
    /// MANUAL_REGENERATE skips the READY short-circuit (a new job is always created) and, with
    /// <paramref name="bypassFailedCooldown"/>, retries straight through a recent failure.
    /// </summary>
    Task<GalleryItemTtsEnsureResult> EnsureAudioAsync(
        long galleryItemId,
        string triggerSource,
        long? actorUserId,
        bool requirePublicVisible,
        bool bypassFailedCooldown,
        CancellationToken cancellationToken);

    /// <summary>Read-only variant for the public poll endpoint (never creates a job): adds NOT_CREATED.</summary>
    Task<GalleryItemTtsEnsureResult> GetAudioStatusAsync(
        long galleryItemId,
        bool requirePublicVisible,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read-only management status (Staff Leader dashboard). Never creates a job and never applies the
    /// public-visibility gate — the item only has to exist (not soft-deleted). Campus scoping is the
    /// caller's responsibility. Returns 404 (throws) only when the item is missing.
    /// </summary>
    Task<GalleryItemTtsManagementStatus> GetManagementStatusAsync(
        long galleryItemId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Batched variant of <see cref="GetManagementStatusAsync"/> for the gallery list column: computes
    /// the same management status for many items in one pass (a single query over their TTS rows). The
    /// caller owns campus scoping (the list executor already restricts items to the caller's campus).
    /// Every input item id is present in the returned map.
    /// </summary>
    Task<IReadOnlyDictionary<long, GalleryItemTtsManagementStatus>> GetManagementStatusesAsync(
        IReadOnlyCollection<GalleryTtsItemDescriptor> items,
        CancellationToken cancellationToken);

    /// <summary>
    /// Background execution of one queued job: submit to EverAI, then (polling mode) poll until done,
    /// download the audio and store it on Google Drive. Also resumes PROCESSING rows that lost their
    /// poll window (worker restart / attempts exhausted). Never throws for business failures — the row
    /// is marked FAILED with an error code instead.
    /// </summary>
    Task ProcessJobAsync(long ttsAudioId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotent EverAI webhook handler: finds the row by request id; SUCCESS → download + store +
    /// READY (no-op when already READY), FAILURE → FAILED. Unknown request ids are logged and ignored.
    /// </summary>
    Task HandleEverAiCallbackAsync(EverAiTtsCallbackDto callback, CancellationToken cancellationToken);
}
