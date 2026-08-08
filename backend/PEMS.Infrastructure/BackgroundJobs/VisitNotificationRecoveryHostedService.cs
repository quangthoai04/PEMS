using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Clock-driven recovery for visit notifications whose business transition committed but whose message
/// never got out — a campus rejection, a lapsed contact invitation.
///
/// <para>
/// Deliberately a separate job from the maintenance sweep it repairs after. Folding it in would make one
/// pass responsible both for expiring invitations and for chasing the mail of invitations expired days
/// ago, and a failure in the second half would take the first down with it — which is the shape of the
/// problem this exists to fix.
/// </para>
/// <para>
/// Config: <c>VisitNotificationRecovery:PollSeconds</c> (default 900),
/// <c>VisitNotificationRecovery:BatchSize</c> (default 100).
/// </para>
/// </summary>
public sealed class VisitNotificationRecoveryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitNotificationRecoveryHostedService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public VisitNotificationRecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<VisitNotificationRecoveryHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["VisitNotificationRecovery:PollSeconds"], out var s) && s > 0 ? s : 900;
        _pollInterval = TimeSpan.FromSeconds(seconds);
        _batchSize = int.TryParse(configuration["VisitNotificationRecovery:BatchSize"], out var b) && b > 0 ? b : 100;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Longer initial delay than the maintenance job: on a restart the transitions worth chasing are
        // minutes old at least, and starting both sweeps at once would have them contend for nothing.
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recovery = scope.ServiceProvider.GetRequiredService<IVisitNotificationRecoveryService>();
                var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
                await recovery.RunOnceAsync(clock.VietnamNow, _batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the job — next tick retries.
                _logger.LogError(ex, "visit notification recovery sweep failed");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
