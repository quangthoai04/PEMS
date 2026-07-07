using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Files;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Common.Storage;
using PEMS.Domain.Entities.Galleries;

namespace PEMS.Application.Galleries.Tts;

/// <inheritdoc cref="IGalleryItemTtsService"/>
public sealed class GalleryItemTtsService : IGalleryItemTtsService
{
    private static readonly string[] RunningStatuses =
    {
        GalleryTtsJobStatuses.Pending, GalleryTtsJobStatuses.Submitted, GalleryTtsJobStatuses.Processing,
    };

    private readonly IApplicationDbContext _db;
    private readonly IEverAiTtsClient _everAi;
    private readonly IGalleryTtsHashService _hash;
    private readonly IGalleryTtsJobQueue _queue;
    private readonly IFileUploadService _fileUpload;
    private readonly IDateTimeService _clock;
    private readonly EverAiTtsOptions _options;
    private readonly GoogleDriveOptions _driveOptions;
    private readonly ILogger<GalleryItemTtsService> _logger;

    public GalleryItemTtsService(
        IApplicationDbContext db,
        IEverAiTtsClient everAi,
        IGalleryTtsHashService hash,
        IGalleryTtsJobQueue queue,
        IFileUploadService fileUpload,
        IDateTimeService clock,
        IOptions<EverAiTtsOptions> options,
        IOptions<GoogleDriveOptions> driveOptions,
        ILogger<GalleryItemTtsService> logger)
    {
        _db = db;
        _everAi = everAi;
        _hash = hash;
        _queue = queue;
        _fileUpload = fileUpload;
        _clock = clock;
        _options = options.Value;
        _driveOptions = driveOptions.Value;
        _logger = logger;
    }

    private bool IsConfigured =>
        _options.IsConfigured && !string.IsNullOrWhiteSpace(_driveOptions.GalleryAudioFolderId);

    public async Task<GalleryItemTtsEnsureResult> EnsureAudioAsync(
        long galleryItemId,
        string triggerSource,
        long? actorUserId,
        bool requirePublicVisible,
        bool bypassFailedCooldown,
        CancellationToken cancellationToken)
    {
        var item = await LoadItemAsync(galleryItemId, requirePublicVisible, cancellationToken);

        if (!IsConfigured)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Disabled);

        var text = _hash.NormalizeDescription(item.Description);
        if (text.Length == 0 || text.Length > _options.MaxInputCharacters)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.InvalidDescription);

        var hash = ComputeCurrentHash(text);
        var itemId = (ulong)galleryItemId;
        var isManual = triggerSource == TtsTriggerSources.ManualRegenerate;

        // Manual regenerate deliberately skips the READY short-circuit: "Tạo lại audio" always makes a
        // fresh generation (the public player then picks the newest READY row for the same hash).
        if (!isManual)
        {
            var ready = await FindReadyAsync(itemId, hash, cancellationToken);
            if (ready is not null)
                return ReadyResult(ready);
        }

        var hasRunning = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
            t.GalleryItemId == itemId &&
            t.SourceTextHash == hash &&
            RunningStatuses.Contains(t.Status),
            cancellationToken);
        if (hasRunning)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Processing);

        if (!bypassFailedCooldown && !isManual)
        {
            var cooldownFloor = _clock.VietnamNow.AddMinutes(-_options.FailedCooldownMinutes);
            var recentlyFailed = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
                t.GalleryItemId == itemId &&
                t.SourceTextHash == hash &&
                t.Status == GalleryTtsJobStatuses.Failed &&
                t.FailedAt != null && t.FailedAt > cooldownFloor,
                cancellationToken);
            if (recentlyFailed)
                return new GalleryItemTtsEnsureResult(TtsAudioStatuses.TemporarilyUnavailable);
        }

        var now = _clock.VietnamNow;
        var job = new GalleryItemTtsAudio
        {
            GalleryItemId = itemId,
            SourceTextHash = hash,
            SourceText = text,
            VoiceCode = _options.DefaultVoiceCode,
            AudioType = _options.DefaultAudioType,
            Bitrate = _options.DefaultBitrate,
            SpeedRate = _options.DefaultSpeedRate,
            PitchRate = _options.DefaultPitchRate,
            Volume = _options.DefaultVolume,
            Status = GalleryTtsJobStatuses.Pending,
            TriggerSource = triggerSource,
            RequestedAt = now,
            CreatedAt = now,
            CreatedBy = actorUserId is { } uid ? (ulong)uid : null,
        };
        _db.GalleryItemTtsAudios.Add(job);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // The DB's unique running_key means several concurrent ensure calls (e.g. many visitors
            // pressing the speaker at once) race and exactly one insert wins — the losers land here.
            _db.GalleryItemTtsAudios.Remove(job); // detach the failed Added entity
            _logger.LogInformation(ex,
                "Concurrent TTS job insert for gallery item {GalleryItemId} lost the running-key race.",
                galleryItemId);
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Processing);
        }

        _queue.Enqueue((long)job.TtsAudioId);
        _logger.LogInformation(
            "Queued TTS job {TtsAudioId} for gallery item {GalleryItemId} (trigger {Trigger}).",
            job.TtsAudioId, galleryItemId, triggerSource);

        return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Processing);
    }

    public async Task<GalleryItemTtsEnsureResult> GetAudioStatusAsync(
        long galleryItemId,
        bool requirePublicVisible,
        CancellationToken cancellationToken)
    {
        var item = await LoadItemAsync(galleryItemId, requirePublicVisible, cancellationToken);

        if (!IsConfigured)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Disabled);

        var text = _hash.NormalizeDescription(item.Description);
        if (text.Length == 0 || text.Length > _options.MaxInputCharacters)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.InvalidDescription);

        var hash = ComputeCurrentHash(text);
        var itemId = (ulong)galleryItemId;

        var ready = await FindReadyAsync(itemId, hash, cancellationToken);
        if (ready is not null)
            return ReadyResult(ready);

        var hasRunning = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
            t.GalleryItemId == itemId &&
            t.SourceTextHash == hash &&
            RunningStatuses.Contains(t.Status),
            cancellationToken);
        if (hasRunning)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.Processing);

        var cooldownFloor = _clock.VietnamNow.AddMinutes(-_options.FailedCooldownMinutes);
        var recentlyFailed = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
            t.GalleryItemId == itemId &&
            t.SourceTextHash == hash &&
            t.Status == GalleryTtsJobStatuses.Failed &&
            t.FailedAt != null && t.FailedAt > cooldownFloor,
            cancellationToken);
        if (recentlyFailed)
            return new GalleryItemTtsEnsureResult(TtsAudioStatuses.TemporarilyUnavailable);

        return new GalleryItemTtsEnsureResult(TtsAudioStatuses.NotCreated);
    }

    public async Task<GalleryItemTtsManagementStatus> GetManagementStatusAsync(
        long galleryItemId, CancellationToken cancellationToken)
    {
        // Management view: no public-visibility gate (a HIDDEN item still shows its audio status).
        var item = await LoadItemAsync(galleryItemId, requirePublicVisible: false, cancellationToken);

        if (!IsConfigured)
            return new GalleryItemTtsManagementStatus(TtsManagementStatuses.Disabled, CanRegenerate: false);

        var text = _hash.NormalizeDescription(item.Description);
        if (text.Length == 0 || text.Length > _options.MaxInputCharacters)
            return new GalleryItemTtsManagementStatus(TtsManagementStatuses.InvalidDescription, CanRegenerate: false);

        var hash = ComputeCurrentHash(text);
        var itemId = (ulong)galleryItemId;

        // Matching READY audio → up to date, nothing to regenerate (this is the "hash giống nhau" case).
        var ready = await FindReadyAsync(itemId, hash, cancellationToken);
        if (ready is not null)
            return new GalleryItemTtsManagementStatus(
                TtsManagementStatuses.Ready, CanRegenerate: false,
                (long)ready.AudioFileId!.Value, ready.VoiceCode, ready.AudioType);

        // A job for the current text is already running → don't offer a duplicate regenerate.
        var hasRunning = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
            t.GalleryItemId == itemId &&
            t.SourceTextHash == hash &&
            RunningStatuses.Contains(t.Status),
            cancellationToken);
        if (hasRunning)
            return new GalleryItemTtsManagementStatus(TtsManagementStatuses.Processing, CanRegenerate: false);

        // The newest FAILED attempt for the CURRENT text (its error message helps the Staff Leader).
        var failed = await _db.GalleryItemTtsAudios.AsNoTracking()
            .Where(t => t.GalleryItemId == itemId && t.SourceTextHash == hash && t.Status == GalleryTtsJobStatuses.Failed)
            .OrderByDescending(t => t.FailedAt).ThenByDescending(t => t.TtsAudioId)
            .Select(t => new { t.ErrorMessage })
            .FirstOrDefaultAsync(cancellationToken);
        if (failed is not null)
            return new GalleryItemTtsManagementStatus(
                TtsManagementStatuses.Failed, CanRegenerate: true, ErrorMessage: failed.ErrorMessage);

        // No current-hash READY/running/failed, but an OLDER READY audio exists (different hash) → the
        // description/settings changed since it was generated, so it's stale and should be regenerated.
        var hasStaleReady = await _db.GalleryItemTtsAudios.AsNoTracking().AnyAsync(t =>
            t.GalleryItemId == itemId &&
            t.Status == GalleryTtsJobStatuses.Ready &&
            t.AudioFileId != null,
            cancellationToken);
        if (hasStaleReady)
            return new GalleryItemTtsManagementStatus(TtsManagementStatuses.Stale, CanRegenerate: true);

        return new GalleryItemTtsManagementStatus(TtsManagementStatuses.NotCreated, CanRegenerate: true);
    }

    public async Task<IReadOnlyDictionary<long, GalleryItemTtsManagementStatus>> GetManagementStatusesAsync(
        IReadOnlyCollection<GalleryTtsItemDescriptor> items, CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, GalleryItemTtsManagementStatus>(items.Count);
        if (items.Count == 0)
            return result;

        // Global gate: with TTS unconfigured every item is simply DISABLED — no per-item work.
        if (!IsConfigured)
        {
            foreach (var it in items)
                result[it.GalleryItemId] = new GalleryItemTtsManagementStatus(TtsManagementStatuses.Disabled, CanRegenerate: false);
            return result;
        }

        // Resolve invalid descriptions up front; the rest get their current hash for row matching.
        var currentHashByItem = new Dictionary<long, string>(items.Count);
        foreach (var it in items)
        {
            var text = _hash.NormalizeDescription(it.Description);
            if (text.Length == 0 || text.Length > _options.MaxInputCharacters)
            {
                result[it.GalleryItemId] = new GalleryItemTtsManagementStatus(
                    TtsManagementStatuses.InvalidDescription, CanRegenerate: false);
                continue;
            }
            currentHashByItem[it.GalleryItemId] = ComputeCurrentHash(text);
        }

        if (currentHashByItem.Count == 0)
            return result;

        // One flat query for all the page's items; the state machine is then evaluated in memory
        // (mirrors GetManagementStatusAsync exactly) so there is no per-item round-trip.
        var itemIds = currentHashByItem.Keys.Select(k => (ulong)k).ToList();
        var rows = await _db.GalleryItemTtsAudios.AsNoTracking()
            .Where(t => itemIds.Contains(t.GalleryItemId))
            .Select(t => new TtsStatusRow
            {
                GalleryItemId = t.GalleryItemId,
                SourceTextHash = t.SourceTextHash,
                Status = t.Status,
                AudioFileId = t.AudioFileId,
                VoiceCode = t.VoiceCode,
                AudioType = t.AudioType,
                ErrorMessage = t.ErrorMessage,
                ReadyAt = t.ReadyAt,
                FailedAt = t.FailedAt,
                TtsAudioId = t.TtsAudioId,
            })
            .ToListAsync(cancellationToken);
        var rowsByItem = rows.ToLookup(r => (long)r.GalleryItemId);

        foreach (var (itemId, hash) in currentHashByItem)
        {
            var itemRows = rowsByItem[itemId];

            var ready = itemRows
                .Where(r => r.SourceTextHash == hash && r.Status == GalleryTtsJobStatuses.Ready && r.AudioFileId != null)
                .OrderByDescending(r => r.ReadyAt).ThenByDescending(r => r.TtsAudioId)
                .FirstOrDefault();
            if (ready is not null)
            {
                result[itemId] = new GalleryItemTtsManagementStatus(
                    TtsManagementStatuses.Ready, CanRegenerate: false,
                    (long)ready.AudioFileId!.Value, ready.VoiceCode, ready.AudioType);
                continue;
            }

            if (itemRows.Any(r => r.SourceTextHash == hash && RunningStatuses.Contains(r.Status)))
            {
                result[itemId] = new GalleryItemTtsManagementStatus(TtsManagementStatuses.Processing, CanRegenerate: false);
                continue;
            }

            var failed = itemRows
                .Where(r => r.SourceTextHash == hash && r.Status == GalleryTtsJobStatuses.Failed)
                .OrderByDescending(r => r.FailedAt).ThenByDescending(r => r.TtsAudioId)
                .FirstOrDefault();
            if (failed is not null)
            {
                result[itemId] = new GalleryItemTtsManagementStatus(
                    TtsManagementStatuses.Failed, CanRegenerate: true, ErrorMessage: failed.ErrorMessage);
                continue;
            }

            if (itemRows.Any(r => r.Status == GalleryTtsJobStatuses.Ready && r.AudioFileId != null))
            {
                result[itemId] = new GalleryItemTtsManagementStatus(TtsManagementStatuses.Stale, CanRegenerate: true);
                continue;
            }

            result[itemId] = new GalleryItemTtsManagementStatus(TtsManagementStatuses.NotCreated, CanRegenerate: true);
        }

        return result;
    }

    private sealed class TtsStatusRow
    {
        public ulong GalleryItemId { get; init; }
        public string SourceTextHash { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public ulong? AudioFileId { get; init; }
        public string? VoiceCode { get; init; }
        public string? AudioType { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime? ReadyAt { get; init; }
        public DateTime? FailedAt { get; init; }
        public ulong TtsAudioId { get; init; }
    }

    public async Task ProcessJobAsync(long ttsAudioId, CancellationToken cancellationToken)
    {
        var row = await _db.GalleryItemTtsAudios
            .FirstOrDefaultAsync(t => t.TtsAudioId == (ulong)ttsAudioId, cancellationToken);
        if (row is null) return;

        switch (row.Status)
        {
            case GalleryTtsJobStatuses.Pending:
                await SubmitAsync(row, cancellationToken);
                break;

            // A PROCESSING row with a request id but no stored audio lost its poll window (worker
            // restart or attempts exhausted) — resume polling instead of resubmitting (no new credits).
            case GalleryTtsJobStatuses.Processing
                when !string.IsNullOrWhiteSpace(row.EverAiRequestId) && row.AudioFileId is null:
                if (_options.UseCallback) return; // the callback owns completion
                await PollUntilStoredAsync(row, cancellationToken);
                break;
        }
    }

    public async Task HandleEverAiCallbackAsync(
        EverAiTtsCallbackDto callback, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callback.RequestId))
        {
            _logger.LogWarning("EverAI TTS callback without request_id was ignored.");
            return;
        }

        var row = await _db.GalleryItemTtsAudios
            .FirstOrDefaultAsync(t => t.EverAiRequestId == callback.RequestId, cancellationToken);
        if (row is null)
        {
            _logger.LogWarning("EverAI TTS callback for unknown request {RequestId} was ignored.",
                callback.RequestId);
            return;
        }

        // Idempotent: a duplicate callback for an already-stored audio must not re-upload anything.
        if (row.Status == GalleryTtsJobStatuses.Ready) return;

        if (string.Equals(callback.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(callback.AudioLink))
            {
                await MarkFailedAsync(row, GalleryTtsErrorCodes.EverAiAudioNotReady,
                    "EverAI callback SUCCESS nhưng không có audio_link.", cancellationToken);
                return;
            }

            row.EverAiAudioLink = callback.AudioLink;
            if (callback.Characters is { } chars) row.Characters = chars;
            await DownloadAndStoreAsync(row, callback.AudioLink, cancellationToken);
            return;
        }

        await MarkFailedAsync(row,
            string.IsNullOrWhiteSpace(callback.ErrorCode)
                ? GalleryTtsErrorCodes.EverAiRequestFailed
                : callback.ErrorCode!,
            callback.ErrorMessage ?? "EverAI báo tạo audio thất bại.",
            cancellationToken);
    }

    // ── job steps ───────────────────────────────────────────────────────────

    private async Task SubmitAsync(GalleryItemTtsAudio row, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            await MarkFailedAsync(row, GalleryTtsErrorCodes.ConfigMissing,
                "EverAI TTS hoặc Google Drive gallery-audio chưa được cấu hình.", cancellationToken);
            return;
        }

        var now = _clock.VietnamNow;
        row.Status = GalleryTtsJobStatuses.Submitted;
        row.SubmittedAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        EverAiCreateTtsResponse response;
        try
        {
            response = await _everAi.CreateAsync(new EverAiCreateTtsRequest
            {
                ResponseType = "indirect",
                CallbackUrl = _options.UseCallback && !string.IsNullOrWhiteSpace(_options.CallbackUrl)
                    ? _options.CallbackUrl
                    : null,
                InputText = row.SourceText,
                VoiceCode = row.VoiceCode,
                AudioType = row.AudioType,
                Bitrate = row.Bitrate ?? _options.DefaultBitrate,
                SpeedRate = row.SpeedRate,
                PitchRate = row.PitchRate,
                Volume = row.Volume,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "EverAI create request failed for TTS job {TtsAudioId}.", row.TtsAudioId);
            await MarkFailedAsync(row, GalleryTtsErrorCodes.EverAiRequestFailed,
                "Không gọi được EverAI TTS.", cancellationToken);
            return;
        }

        if (response.Status != 1 || string.IsNullOrWhiteSpace(response.Result?.RequestId))
        {
            await MarkFailedAsync(row,
                string.IsNullOrWhiteSpace(response.ErrorCode)
                    ? GalleryTtsErrorCodes.EverAiRequestFailed
                    : response.ErrorCode!,
                response.ErrorMessage ?? "EverAI từ chối yêu cầu tạo audio.",
                cancellationToken);
            return;
        }

        now = _clock.VietnamNow;
        row.EverAiRequestId = response.Result!.RequestId;
        row.Characters = response.Result.Characters;
        row.Status = GalleryTtsJobStatuses.Processing;
        row.ProcessingAt = now;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        if (_options.UseCallback) return; // completion arrives via the callback endpoint

        await PollUntilStoredAsync(row, cancellationToken);
    }

    private async Task PollUntilStoredAsync(GalleryItemTtsAudio row, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        var maxAttempts = Math.Max(1, _options.PollingMaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await Task.Delay(interval, cancellationToken);

            EverAiGetTtsResponse response;
            try
            {
                response = await _everAi.GetRequestAsync(row.EverAiRequestId!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "EverAI poll attempt {Attempt} failed for TTS job {TtsAudioId}.",
                    attempt, row.TtsAudioId);
                continue;
            }

            if (response.Status != 1 || response.Result is null)
                continue;

            var result = response.Result;
            if (result.Progress is { } progress)
            {
                row.Progress = progress;
                row.UpdatedAt = _clock.VietnamNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            var lifecycle = result.Status?.Trim().ToLowerInvariant();
            if (lifecycle is "failed" or "failure" or "error")
            {
                await MarkFailedAsync(row, GalleryTtsErrorCodes.EverAiRequestFailed,
                    result.ErrorMessage ?? "EverAI báo tạo audio thất bại.", cancellationToken);
                return;
            }

            if (lifecycle == "done" && !string.IsNullOrWhiteSpace(result.AudioLink))
            {
                if (result.AudioExpired == true)
                {
                    await MarkFailedAsync(row, GalleryTtsErrorCodes.EverAiAudioExpired,
                        "Link audio EverAI đã hết hạn trước khi tải về.", cancellationToken);
                    return;
                }

                row.EverAiAudioLink = result.AudioLink;
                await DownloadAndStoreAsync(row, result.AudioLink!, cancellationToken);
                return;
            }
        }

        // Attempts exhausted with EverAI still working: keep PROCESSING — the worker's periodic sweep
        // re-enqueues this row later and ProcessJobAsync resumes polling (no new EverAI credits spent).
        _logger.LogInformation(
            "TTS job {TtsAudioId} still processing after {Attempts} poll attempts; will re-poll on sweep.",
            row.TtsAudioId, maxAttempts);
    }

    private async Task DownloadAndStoreAsync(
        GalleryItemTtsAudio row, string audioLink, CancellationToken cancellationToken)
    {
        if (row.Status == GalleryTtsJobStatuses.Ready) return; // idempotency guard

        EverAiAudioDownload download;
        try
        {
            download = await _everAi.DownloadAudioAsync(audioLink, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audio download failed for TTS job {TtsAudioId}.", row.TtsAudioId);
            await MarkFailedAsync(row, GalleryTtsErrorCodes.AudioDownloadFailed,
                "Không tải được file audio từ EverAI.", cancellationToken);
            return;
        }

        if (download.Content.Length == 0)
        {
            await MarkFailedAsync(row, GalleryTtsErrorCodes.AudioDownloadFailed,
                "File audio EverAI trả về rỗng.", cancellationToken);
            return;
        }

        // files.uploaded_by is a real-user FK, so attribute the upload to the requester, falling back
        // to whoever created the gallery item (anonymous LAZY_GENERATE has no actor of its own).
        var uploadedBy = row.CreatedBy ?? await _db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == row.GalleryItemId)
            .Select(i => i.CreatedBy)
            .FirstOrDefaultAsync(cancellationToken);
        if (uploadedBy is null)
        {
            await MarkFailedAsync(row, GalleryTtsErrorCodes.AudioUploadFailed,
                "Không xác định được người tải lên cho file audio.", cancellationToken);
            return;
        }

        var extension = string.Equals(row.AudioType, "wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";
        var fileName = $"gallery-item-{row.GalleryItemId}-tts-{row.TtsAudioId}.{extension}";
        var contentType = download.ContentType is { } ct && ct.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            ? ct
            : (extension == "wav" ? "audio/wav" : "audio/mpeg");

        UploadedFileDto uploaded;
        try
        {
            await using var stream = new MemoryStream(download.Content, writable: false);
            uploaded = await _fileUpload.UploadBusinessFileAsync(
                stream, fileName, contentType, download.Content.Length,
                FilePurpose.GalleryAudio, (long)uploadedBy.Value, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Google Drive upload failed for TTS job {TtsAudioId}.", row.TtsAudioId);
            await MarkFailedAsync(row, GalleryTtsErrorCodes.AudioUploadFailed,
                "Không upload được file audio lên Google Drive.", cancellationToken);
            return;
        }

        var now = _clock.VietnamNow;
        row.AudioFileId = (ulong)uploaded.FileId;
        row.Status = GalleryTtsJobStatuses.Ready;
        row.ReadyAt = now;
        row.Progress = 100m;
        row.ErrorCode = null;
        row.ErrorMessage = null;
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TTS job {TtsAudioId} is READY (file {FileId}).", row.TtsAudioId, uploaded.FileId);
    }

    private async Task MarkFailedAsync(
        GalleryItemTtsAudio row, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        var now = _clock.VietnamNow;
        row.Status = GalleryTtsJobStatuses.Failed;
        row.FailedAt = now;
        row.ErrorCode = errorCode;
        row.ErrorMessage = errorMessage.Length <= 1000 ? errorMessage : errorMessage[..1000];
        row.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning("TTS job {TtsAudioId} FAILED ({ErrorCode}).", row.TtsAudioId, errorCode);
    }

    // ── shared lookups ──────────────────────────────────────────────────────

    private sealed record ItemHead(string Description);

    private async Task<ItemHead> LoadItemAsync(
        long galleryItemId, bool requirePublicVisible, CancellationToken cancellationToken)
    {
        var itemId = (ulong)galleryItemId;
        var query = _db.GalleryItems.AsNoTracking()
            .Where(i => i.GalleryItemId == itemId && i.DeletedAt == null);

        // Public callers must never trigger (or even observe) narration for non-public content —
        // HIDDEN items or INACTIVE location/area/campus behave exactly like a missing item (404).
        if (requirePublicVisible)
        {
            query = query.Where(i =>
                i.Status == "PUBLISHED" &&
                i.Location.Status == "ACTIVE" &&
                i.Location.Area.Status == "ACTIVE" &&
                i.Location.Area.Campus.Status == "ACTIVE");
        }

        var head = await query
            .Select(i => new ItemHead(i.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return head ?? throw new NotFoundException("GalleryItem", galleryItemId);
    }

    private string ComputeCurrentHash(string normalizedText) => _hash.ComputeHash(
        normalizedText,
        _options.DefaultVoiceCode,
        _options.DefaultAudioType,
        _options.DefaultBitrate,
        _options.DefaultSpeedRate,
        _options.DefaultPitchRate,
        _options.DefaultVolume);

    private async Task<GalleryItemTtsAudio?> FindReadyAsync(
        ulong itemId, string hash, CancellationToken cancellationToken)
        => await _db.GalleryItemTtsAudios.AsNoTracking()
            .Where(t =>
                t.GalleryItemId == itemId &&
                t.SourceTextHash == hash &&
                t.Status == GalleryTtsJobStatuses.Ready &&
                t.AudioFileId != null)
            .OrderByDescending(t => t.ReadyAt)
            .ThenByDescending(t => t.TtsAudioId)
            .FirstOrDefaultAsync(cancellationToken);

    private static GalleryItemTtsEnsureResult ReadyResult(GalleryItemTtsAudio ready) => new(
        TtsAudioStatuses.Ready,
        (long)ready.AudioFileId!.Value,
        ready.VoiceCode,
        ready.AudioType);
}
