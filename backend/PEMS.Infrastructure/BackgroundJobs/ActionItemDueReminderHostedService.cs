using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Minutes;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Minutes;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Dispatches the once-only "đến hạn" reminder for meeting-minutes action items
/// (minute_action_items). On each tick it picks rows whose due_date has passed, still have an
/// assignee, are not DONE/CANCELLED, and have never been reminded (due_reminder_sent_at IS NULL) —
/// sends an in-app notification + email to the assignee, then stamps due_reminder_sent_at so the
/// same item is never reminded twice. A single failing item never aborts the rest of the batch.
/// Mirrors <see cref="VisitReminderDispatchHostedService"/>.
/// </summary>
public sealed class ActionItemDueReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionItemDueReminderHostedService> _logger;
    private readonly TimeSpan _pollInterval;

    public ActionItemDueReminderHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ActionItemDueReminderHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["Reminders:PollSeconds"], out var s) && s > 0 ? s : 60;
        _pollInterval = TimeSpan.FromSeconds(seconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action item due-reminder dispatch tick failed.");
            }

            try { await Task.Delay(_pollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // due_date is stored as Vietnam wall-clock, so compare against Vietnam-local "now".
        var now = clock.VietnamNow;

        var due = await db.MinuteActionItems
            .Where(a => a.DueDate != null && a.DueDate <= now
                        && a.DueReminderSentAt == null
                        && a.Status != "DONE" && a.Status != "CANCELLED"
                        && a.AssignedToUserId != null)
            .OrderBy(a => a.DueDate)
            .Take(50)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var item in due)
        {
            try
            {
                await DispatchOneAsync(db, email, notificationService, item, now, ct);
                item.DueReminderSentAt = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch due-reminder for action item {ActionItemId}.", item.ActionItemId);
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchOneAsync(
        IApplicationDbContext db, IEmailService email, INotificationService notificationService,
        MinuteActionItem item, DateTime now, CancellationToken ct)
    {
        var minute = await db.Minutes.AsNoTracking()
            .FirstOrDefaultAsync(m => m.MinutesId == item.MinutesId, ct);
        if (minute == null) return;

        var instance = await db.VisitRequestCampuses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VisitInstanceId == minute.VisitInstanceId, ct);
        if (instance == null) return;

        var assignee = await db.Users.AsNoTracking()
            .Where(u => u.UserId == item.AssignedToUserId!.Value)
            .Select(u => new { u.UserId, u.FullName, u.Email })
            .FirstOrDefaultAsync(ct);
        if (assignee == null) return;

        var delegationName = (await VisitInstanceEffectiveName.ForInstancesAsync(db, new[] { instance.VisitInstanceId }, ct))
            .GetValueOrDefault(instance.VisitInstanceId) ?? "Đoàn khách";
        // Same destination as the assignment notification in SaveMinutesCommandHandler — deep-links
        // straight into "Quản lý việc sau tiếp khách" filtered to this one item.
        var actionUrl = $"/dashboard/post-visit-tasks?actionItemId={item.ActionItemId}";

        await notificationService.CreateAsync(new CreateNotificationRequest(
            RecipientUserId: assignee.UserId,
            Title: "Đến hạn hoàn thành công việc",
            Message: $"Công việc \"{item.Title}\" bạn phụ trách (đoàn {delegationName}) đã đến hạn hoàn thành.",
            NotificationType: NotificationTypes.ActionItemDue,
            RelatedType: NotificationRelatedTypes.MinuteActionItem,
            RelatedId: item.ActionItemId,
            Category: NotificationCategories.Reminder,
            VisitInstanceId: instance.VisitInstanceId,
            CampusId: instance.CampusId,
            ActionType: NotificationActionTypes.OpenVisitDetail,
            ActionUrl: actionUrl,
            DedupeKey: $"ACTION_ITEM_DUE_{item.ActionItemId}"
        ), ct);

        if (string.IsNullOrWhiteSpace(assignee.Email)) return;

        var dueDateText = item.DueDate!.Value.ToString("HH:mm dd/MM/yyyy");
        var subject = ActionItemEmailContent.DueReminderSubject(item.Title);
        var body = EmailComposition.BrandedShell(
            ActionItemEmailContent.DueReminderBodyHtml(assignee.FullName, item.Title, dueDateText, delegationName));

        ulong sentEmailId, sentEmailRecipientId;
        await using (var transaction = await db.BeginTransactionAsync(ct))
        {
            var sentEmail = new SentEmail
            {
                RelatedType = NotificationRelatedTypes.MinuteActionItem,
                RelatedId = item.ActionItemId,
                Subject = subject,
                BodySnapshot = body,
                Status = "QUEUED",
                CreatedAt = now,
            };
            db.SentEmails.Add(sentEmail);
            await db.SaveChangesAsync(ct);

            var sentRecipient = new SentEmailRecipient
            {
                SentEmailId = sentEmail.SentEmailId,
                RecipientEmail = assignee.Email,
                RecipientName = assignee.FullName,
                RecipientType = "TO",
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            };
            db.SentEmailRecipients.Add(sentRecipient);
            await db.SaveChangesAsync(ct);

            sentEmailId = sentEmail.SentEmailId;
            sentEmailRecipientId = sentRecipient.SentEmailRecipientId;
            await transaction.CommitAsync(ct);
        }

        try
        {
            await email.SendAsync(assignee.Email, subject, body, ct);
            await UpdateEmailStatusAsync(db, sentEmailId, sentEmailRecipientId, "SENT", now, null, ct);
        }
        catch (Exception ex)
        {
            await UpdateEmailStatusAsync(db, sentEmailId, sentEmailRecipientId, "FAILED", now, ex.Message, ct);
            // Re-throw so the caller does NOT stamp DueReminderSentAt — the item stays eligible and
            // is retried next tick. Safe to retry: the notification call above is DedupeKey-guarded,
            // so a retry never creates a second bell entry, only re-attempts the failed email.
            throw;
        }
    }

    private static async Task UpdateEmailStatusAsync(
        IApplicationDbContext db, ulong sentEmailId, ulong sentEmailRecipientId, string status, DateTime now,
        string? error, CancellationToken ct)
    {
        var sentEmail = await db.SentEmails.FirstOrDefaultAsync(e => e.SentEmailId == sentEmailId, ct);
        if (sentEmail != null)
        {
            sentEmail.Status = status;
            sentEmail.LastAttemptAt = now;
            sentEmail.RetryCount += 1;
            if (status == "SENT") sentEmail.SentAt = now;
            else sentEmail.ErrorMessage = Truncate(error, 1000);
        }
        var rec = await db.SentEmailRecipients.FirstOrDefaultAsync(r => r.SentEmailRecipientId == sentEmailRecipientId, ct);
        if (rec != null)
        {
            rec.DeliveryStatus = status;
            if (status == "SENT") rec.SentAt = now;
            else rec.ErrorMessage = Truncate(error, 1000);
        }
        await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}
