using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Clock-driven per-campus operational-contact maintenance: expires overdue PENDING invitations and redacts
/// terminal ones past retention. Runs regardless of the v2 feature flags — existing data must keep
/// aging out even if the flags are later switched off; with no v2 rows both sweeps are cheap no-ops.
/// Config: <c>IdentityClaims:PollSeconds</c> (default 600), <c>IdentityClaims:BatchSize</c> (default 200).
/// </summary>
public sealed class OperationalContactMaintenanceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationalContactMaintenanceHostedService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public OperationalContactMaintenanceHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationalContactMaintenanceHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["IdentityClaims:PollSeconds"], out var s) && s > 0 ? s : 600;
        _pollInterval = TimeSpan.FromSeconds(seconds);
        _batchSize = int.TryParse(configuration["IdentityClaims:BatchSize"], out var b) && b > 0 ? b : 200;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the host finishes starting before the first sweep.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<IOperationalContactMaintenanceService>();
                var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
                await maintenance.RunOnceAsync(clock.VietnamNow, _batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the job — next tick retries.
                _logger.LogError(ex, "operational-contact maintenance sweep failed");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
