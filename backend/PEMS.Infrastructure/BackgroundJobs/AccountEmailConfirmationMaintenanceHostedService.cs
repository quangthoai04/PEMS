using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Accounts.Common;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Clock-driven pending-account confirmation maintenance (P0 #1): expires overdue confirmation tokens and
/// auto-cancels pending accounts that have sat unconfirmed past the grace period, releasing any reserved
/// Head slot. With no pending accounts the sweep is a cheap no-op.
/// Config: <c>AccountEmailConfirmation:PollSeconds</c> (default 3600).
/// </summary>
public sealed class AccountEmailConfirmationMaintenanceHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountEmailConfirmationMaintenanceHostedService> _logger;
    private readonly TimeSpan _pollInterval;

    public AccountEmailConfirmationMaintenanceHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountEmailConfirmationMaintenanceHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["AccountEmailConfirmation:PollSeconds"], out var s) && s > 0 ? s : 3600;
        _pollInterval = TimeSpan.FromSeconds(seconds);
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
                var maintenance = scope.ServiceProvider.GetRequiredService<IAccountEmailConfirmationMaintenance>();
                var result = await maintenance.RunAsync(stoppingToken);
                if (result.TokensExpired > 0 || result.AccountsCancelled > 0)
                    _logger.LogInformation(
                        "account email-confirmation sweep: {Expired} token(s) expired, {Cancelled} pending account(s) auto-cancelled, {Released} reservation(s) released.",
                        result.TokensExpired, result.AccountsCancelled, result.ReservationsReleased);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the job — next tick retries.
                _logger.LogError(ex, "account email-confirmation maintenance sweep failed");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
