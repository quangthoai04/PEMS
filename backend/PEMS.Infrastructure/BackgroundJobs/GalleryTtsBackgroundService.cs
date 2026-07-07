using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Galleries.Tts;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Single background worker for gallery TTS jobs. Normally it just drains the in-process
/// <see cref="IGalleryTtsJobQueue"/> (ids enqueued by ensure/regenerate) and runs
/// <see cref="IGalleryItemTtsService.ProcessJobAsync"/> per job in its own DI scope. When the queue is
/// idle it periodically sweeps the DB for work the queue lost: PENDING rows never picked up (API
/// restart) and PROCESSING rows whose poll window expired — so no job is ever stranded. One failing
/// job never kills the worker.
/// </summary>
public sealed class GalleryTtsBackgroundService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PendingSweepAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessingRepollAge = TimeSpan.FromMinutes(2);
    private const int SweepBatchSize = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGalleryTtsJobQueue _queue;
    private readonly IOptions<EverAiTtsOptions> _options;
    private readonly ILogger<GalleryTtsBackgroundService> _logger;

    public GalleryTtsBackgroundService(
        IServiceScopeFactory scopeFactory,
        IGalleryTtsJobQueue queue,
        IOptions<EverAiTtsOptions> options,
        ILogger<GalleryTtsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the host finishes starting before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            long ttsAudioId;
            try
            {
                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                waitCts.CancelAfter(SweepInterval);
                ttsAudioId = await _queue.DequeueAsync(waitCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                await SweepAsync(stoppingToken); // queue idle → look for stranded DB jobs
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessAsync(ttsAudioId, stoppingToken);
        }
    }

    private async Task ProcessAsync(long ttsAudioId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tts = scope.ServiceProvider.GetRequiredService<IGalleryItemTtsService>();
            await tts.ProcessJobAsync(ttsAudioId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // host shutting down — the sweep resumes this job on the next start
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS job {TtsAudioId} crashed in the background worker.", ttsAudioId);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        if (!_options.Value.IsConfigured) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
            var now = clock.VietnamNow;

            var pendingFloor = now - PendingSweepAge;
            var repollFloor = now - ProcessingRepollAge;

            var stranded = await db.GalleryItemTtsAudios.AsNoTracking()
                .Where(t =>
                    (t.Status == GalleryTtsJobStatuses.Pending && t.RequestedAt < pendingFloor)
                    || (t.Status == GalleryTtsJobStatuses.Processing
                        && t.EverAiRequestId != null
                        && t.AudioFileId == null
                        && (t.UpdatedAt ?? t.ProcessingAt ?? t.RequestedAt) < repollFloor))
                .OrderBy(t => t.TtsAudioId)
                .Take(SweepBatchSize)
                .Select(t => t.TtsAudioId)
                .ToListAsync(ct);

            foreach (var id in stranded)
                _queue.Enqueue((long)id);

            if (stranded.Count > 0)
                _logger.LogInformation("TTS sweep re-enqueued {Count} stranded job(s).", stranded.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS sweep tick failed.");
        }
    }
}
