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
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Minutes;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Dispatches the once-only "đến hạn" reminder for meeting-minutes action items
/// (minute_action_items). On each tick it picks rows whose due_date has passed, still have an
/// assignee, are not DONE/CANCELLED, and have never been reminded (due_reminder_sent_at IS NULL),
/// claims each one with a conditional UPDATE before doing anything else, then sends an in-app
/// notification + email to the assignee. A single failing item never aborts the rest of the batch, and
/// (unlike the at-most-once policy in <see cref="PEMS.Application.Delegations.Reminders.VisitReminderDispatchService"/>)
/// a failed dispatch releases its claim so a later tick retries it.
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

    /// <summary>Internal (not private) so the claim-before-send regression tests can drive one tick
    /// directly, same as PEMS.Infrastructure's other InternalsVisibleTo("PEMS.IntegrationTests") seams —
    /// see this project's own .csproj for the existing precedent.</summary>
    internal async Task DispatchDueRemindersAsync(CancellationToken ct)
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

        // Batch: everything DispatchOneAsync needs to resolve per item, fetched once for the whole
        // `due` batch instead of once per item (same filters/joins as before, see the 4 lookups below).
        // The per-item write side (claim, dispatch, try/catch) still runs exactly once per item, in
        // order — see the loop below for its claim-before-send discipline (DB-TXN-003).
        var minutesIds = due.Select(a => a.MinutesId).Distinct().ToList();
        var minutesById = await db.Minutes.AsNoTracking()
            .Where(m => minutesIds.Contains(m.MinutesId))
            .ToDictionaryAsync(m => m.MinutesId, ct);

        var instanceIds = minutesById.Values.Select(m => m.VisitInstanceId).Distinct().ToList();
        var instancesById = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => instanceIds.Contains(c.VisitInstanceId))
            .ToDictionaryAsync(c => c.VisitInstanceId, ct);

        var assigneeIds = due.Where(a => a.AssignedToUserId.HasValue)
            .Select(a => a.AssignedToUserId!.Value).Distinct().ToList();
        var assigneesById = await db.Users.AsNoTracking()
            .Where(u => assigneeIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName, u.Email })
            .ToDictionaryAsync(x => x.UserId, x => (x.FullName, x.Email), ct);

        var delegationNamesByInstance = await VisitInstanceEffectiveName.ForInstancesAsync(
            db, instancesById.Keys.ToList(), ct);

        foreach (var item in due)
        {
            // Claim before doing anything (DB-TXN-003): two overlapping instances of this job — or an
            // overlapping tick of this same one — used to both see the row as unclaimed for the whole
            // dispatch, since due_reminder_sent_at was only stamped AFTER the email attempt succeeded.
            // A single conditional UPDATE re-checks the SAME conditions the SELECT above used, evaluated
            // against the row's CURRENT state rather than the in-memory copy the SELECT returned a
            // moment earlier — the same discipline VisitReminderDispatchService.ClaimAsync documents —
            // so only one caller's UPDATE matches and the other moves on.
            var claimed = await db.MinuteActionItems
                .Where(a => a.ActionItemId == item.ActionItemId
                            && a.DueDate != null && a.DueDate <= now
                            && a.DueReminderSentAt == null
                            && a.Status != "DONE" && a.Status != "CANCELLED"
                            && a.AssignedToUserId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.DueReminderSentAt, now), ct);
            if (claimed != 1) continue;

            try
            {
                await DispatchOneAsync(
                    db, email, notificationService, item, now,
                    minutesById, instancesById, assigneesById, delegationNamesByInstance, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch due-reminder for action item {ActionItemId}.", item.ActionItemId);
                // Unlike VisitReminderDispatchService's at-most-once policy, this job's own retry
                // contract predates this fix (see DispatchOneAsync's remarks: "safe to retry" via the
                // notification's DedupeKey) and is preserved as-is — undo the claim so a later tick
                // tries again, exactly as a failure left the row before this change.
                await db.MinuteActionItems
                    .Where(a => a.ActionItemId == item.ActionItemId)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.DueReminderSentAt, (DateTime?)null), ct);
            }
        }
    }

    private async Task DispatchOneAsync(
        IApplicationDbContext db, IEmailService email, INotificationService notificationService,
        MinuteActionItem item, DateTime now,
        IReadOnlyDictionary<ulong, Minute> minutesById,
        IReadOnlyDictionary<ulong, VisitRequestCampus> instancesById,
        IReadOnlyDictionary<ulong, (string FullName, string Email)> assigneesById,
        IReadOnlyDictionary<ulong, string?> delegationNamesByInstance,
        CancellationToken ct)
    {
        if (!minutesById.TryGetValue(item.MinutesId, out var minute)) return;

        if (!instancesById.TryGetValue(minute.VisitInstanceId, out var instance)) return;

        if (item.AssignedToUserId is null
            || !assigneesById.TryGetValue(item.AssignedToUserId.Value, out var assignee)) return;

        var delegationName = delegationNamesByInstance.GetValueOrDefault(instance.VisitInstanceId) ?? "Đoàn khách";
        // Same destination as the assignment notification in SaveMinutesCommandHandler — deep-links
        // straight into "Quản lý việc sau tiếp khách" filtered to this one item.
        var actionUrl = $"/dashboard/post-visit-tasks?actionItemId={item.ActionItemId}";

        await notificationService.CreateAsync(new CreateNotificationRequest(
            RecipientUserId: item.AssignedToUserId.Value,
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
            DedupeKey: $"ACTION_ITEM_DUE_{item.ActionItemId}",
            MetadataJson: NotificationEventKeys.BuildMetadata(
                NotificationEventKeys.ActionItemDue,
                new { delegationName, title = item.Title })
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
