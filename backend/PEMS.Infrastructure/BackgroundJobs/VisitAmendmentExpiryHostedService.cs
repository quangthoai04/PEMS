using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Clock-driven amendment expiry (plan §16.6): a PENDING_APPROVAL amendment whose window passed
/// (expires_at, i.e. 24h before the instance start) or whose instance already started is marked EXPIRED —
/// the active snapshot always stays authoritative. Batched + idempotent; runs flag-independent (existing
/// v2 data keeps aging out even if the flags are later switched off; with no rows it is a cheap no-op).
/// Config: <c>Amendments:PollSeconds</c> (default 600), <c>Amendments:BatchSize</c> (default 200).
/// </summary>
public sealed class VisitAmendmentExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitAmendmentExpiryHostedService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public VisitAmendmentExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<VisitAmendmentExpiryHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["Amendments:PollSeconds"], out var s) && s > 0 ? s : 600;
        _pollInterval = TimeSpan.FromSeconds(seconds);
        _batchSize = int.TryParse(configuration["Amendments:BatchSize"], out var b) && b > 0 ? b : 200;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var amendments = scope.ServiceProvider.GetRequiredService<IVisitAmendmentService>();
                var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
                await amendments.ExpireDueAsync(clock.VietnamNow, _batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "visit-amendment expiry sweep failed");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
