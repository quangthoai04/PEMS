using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Delegations.Reminders;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Wakes up on a timer and asks <see cref="IVisitReminderDispatchService"/> to send whatever is due.
///
/// <para>
/// It deliberately contains no business logic at all. Deciding who is reminded, from which template,
/// and whether a reminder has already gone out are questions with real consequences — a duplicate
/// reminder reaches a real person and cannot be recalled — so they live in a service that can be tested,
/// not in a background loop that only runs in a hosted process. This class owns exactly two things: the
/// poll interval, and the rule that one bad tick must never stop the timer.
/// </para>
/// </summary>
public sealed class VisitReminderDispatchHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitReminderDispatchHostedService> _logger;
    private readonly TimeSpan _pollInterval;

    public VisitReminderDispatchHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<VisitReminderDispatchHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["Reminders:PollSeconds"], out var s) && s > 0 ? s : 60;
        _pollInterval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the host finishes starting before the first poll.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reminders = scope.ServiceProvider.GetRequiredService<IVisitReminderDispatchService>();
                await reminders.DispatchDueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Logged without any reminder detail: the exception is enough to investigate with, and
                // the alternative leaks recipients and message content into the application log.
                _logger.LogError(ex, "Visit reminder dispatch tick failed.");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
