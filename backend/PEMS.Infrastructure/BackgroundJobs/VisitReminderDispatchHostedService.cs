using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Infrastructure.BackgroundJobs;

/// <summary>
/// Dispatches scheduled visit reminders (visit_instance_reminder_settings). On each tick it picks
/// PENDING rows whose scheduled_at has passed and, per row independently, inserts in-app
/// notifications and/or renders + sends emails to the resolved recipients. A single failing reminder
/// is marked FAILED and never aborts the rest of the batch. Nothing here is triggered by the save
/// API — only by the clock.
/// </summary>
public sealed class VisitReminderDispatchHostedService : BackgroundService
{
    private const string HostTemplateCode = "VISIT_REMINDER_HOST";
    private const string ParticipantsTemplateCode = "VISIT_REMINDER_PARTICIPANTS";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitReminderDispatchHostedService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly string _frontendBaseUrl;

    public VisitReminderDispatchHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<VisitReminderDispatchHostedService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = int.TryParse(configuration["Reminders:PollSeconds"], out var s) && s > 0 ? s : 60;
        _pollInterval = TimeSpan.FromSeconds(seconds);
        _frontendBaseUrl = (configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
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
                await DispatchDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Visit reminder dispatch tick failed.");
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

        // scheduled_at is stored as Vietnam wall-clock, so compare against Vietnam-local "now"
        // (never UtcNow — that would fire reminders ~7h off).
        var now = clock.VietnamNow;

        var due = await db.VisitInstanceReminderSettings
            .Where(r => r.Status == VisitReminderStatus.PENDING && r.ScheduledAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .Take(50)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var reminder in due)
        {
            try
            {
                await DispatchOneAsync(db, email, notificationService, reminder, now, ct);
                reminder.Status = VisitReminderStatus.SENT;
                reminder.LastDispatchedAt = now;
                reminder.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                reminder.Status = VisitReminderStatus.FAILED;
                reminder.LastDispatchedAt = now;
                reminder.ErrorMessage = Truncate(ex.Message, 1000);
                _logger.LogError(ex, "Failed to dispatch reminder {ReminderId}.", reminder.ReminderSettingId);
            }

            reminder.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchOneAsync(
        IApplicationDbContext db, IEmailService email, INotificationService notificationService,
        Domain.Entities.Delegations.VisitInstanceReminderSetting reminder,
        DateTime now, CancellationToken ct)
    {
        var instance = await db.VisitRequestCampuses
            .Include(c => c.VisitRequest)
            .Include(c => c.FormDetail)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == reminder.VisitInstanceId, ct);
        if (instance == null)
            throw new InvalidOperationException($"Visit instance {reminder.VisitInstanceId} not found.");

        // The reminder targets ONE campus instance → its own per-campus delegation name.
        var delegationName = instance.FormDetail?.DelegationName ?? "FPT University";
        var campusName = await db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? "FPT University";
        var plannedStartText = instance.PlannedStartAt.ToString("HH:mm dd/MM/yyyy");
        var detailUrl = $"{_frontendBaseUrl}/dashboard/visit/process/{instance.VisitRequestId}/{instance.VisitInstanceId}";

        var recipients = await ResolveRecipientsAsync(db, instance, reminder.TargetGroup, ct);
        if (recipients.Count == 0) return; // nothing to send → treated as SENT (no-op)

        if (reminder.Channel == VisitReminderChannel.IN_APP)
        {
            var requests = recipients.Select(r => new CreateNotificationRequest(
                RecipientUserId: r.UserId,
                Title: "Nhắc lịch tiếp khách",
                Message: $"Đoàn {delegationName} tại {campusName} sẽ diễn ra vào {plannedStartText}.",
                NotificationType: PEMS.Application.Notifications.Common.NotificationTypes.VisitReminder,
                RelatedType: PEMS.Application.Notifications.Common.NotificationRelatedTypes.VisitInstance,
                RelatedId: instance.VisitInstanceId,
                Category: NotificationCategories.Reminder,
                VisitInstanceId: instance.VisitInstanceId,
                CampusId: instance.CampusId,
                ActionType: NotificationActionTypes.OpenVisitDetail,
                ActionUrl: $"/dashboard/visit/process/{instance.VisitInstanceId}",
                DedupeKey: $"REMINDER_{reminder.ReminderSettingId}_USER_{r.UserId}"
            )).ToList();

            await notificationService.CreateManyAsync(requests, ct);
            return;
        }

        // EMAIL channel — render per recipient (host vs participant template) and send.
        var hostTemplate = await db.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateCode == HostTemplateCode, ct);
        var participantsTemplate = await db.EmailTemplates.FirstOrDefaultAsync(t => t.TemplateCode == ParticipantsTemplateCode, ct);

        var anyFailure = false;
        string? firstError = null;

        foreach (var r in recipients)
        {
            var template = r.IsHost ? (hostTemplate ?? participantsTemplate) : (participantsTemplate ?? hostTemplate);
            if (template == null)
                throw new InvalidOperationException("Reminder email template is missing.");

            var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hostName"] = r.FullName,
                ["recipientName"] = r.FullName,
                ["DelegationName"] = delegationName,
                ["CampusName"] = campusName,
                ["plannedStartAt"] = plannedStartText,
                ["detailUrl"] = detailUrl,
            };

            var subject = Render(template.SubjectVi ?? template.SubjectEn ?? "Nhắc lịch tiếp khách", context);
            var body = Render(template.BodyVi ?? template.BodyEn ?? string.Empty, context);

            var sentEmail = new SentEmail
            {
                EmailTemplateId = template.EmailTemplateId,
                RelatedType = "VISIT_INSTANCE",
                RelatedId = instance.VisitInstanceId,
                Subject = subject,
                BodySnapshot = body,
                Status = "QUEUED",
                CreatedAt = now,
            };
            db.SentEmails.Add(sentEmail);
            await db.SaveChangesAsync(ct);

            var recipientRow = new SentEmailRecipient
            {
                SentEmailId = sentEmail.SentEmailId,
                RecipientEmail = r.Email,
                RecipientName = r.FullName,
                RecipientType = "TO",
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            };
            db.SentEmailRecipients.Add(recipientRow);
            await db.SaveChangesAsync(ct);

            try
            {
                await email.SendAsync(r.Email, subject, body, ct);
                sentEmail.Status = "SENT";
                sentEmail.SentAt = now;
                sentEmail.LastAttemptAt = now;
                recipientRow.DeliveryStatus = "SENT";
                recipientRow.SentAt = now;
            }
            catch (Exception ex)
            {
                anyFailure = true;
                firstError ??= ex.Message;
                sentEmail.Status = "FAILED";
                sentEmail.LastAttemptAt = now;
                sentEmail.RetryCount += 1;
                sentEmail.ErrorMessage = Truncate(ex.Message, 1000);
                recipientRow.DeliveryStatus = "FAILED";
                recipientRow.ErrorMessage = Truncate(ex.Message, 1000);
            }
            await db.SaveChangesAsync(ct);
        }

        if (anyFailure)
            throw new InvalidOperationException($"One or more reminder emails failed: {firstError}");
    }

    private static async Task<List<ReminderRecipient>> ResolveRecipientsAsync(
        IApplicationDbContext db, Domain.Entities.Delegations.VisitRequestCampus instance,
        VisitReminderTargetGroup targetGroup, CancellationToken ct)
    {
        var result = new List<ReminderRecipient>();
        var seen = new HashSet<ulong>();

        bool includeHost = targetGroup is VisitReminderTargetGroup.HOST or VisitReminderTargetGroup.HOST_AND_PARTICIPANTS;
        bool includeParticipants = targetGroup is VisitReminderTargetGroup.PARTICIPANTS or VisitReminderTargetGroup.HOST_AND_PARTICIPANTS;

        if (includeHost && instance.CurrentHostUserId.HasValue)
        {
            var host = await db.Users
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .FirstOrDefaultAsync(ct);
            if (host != null && !string.IsNullOrWhiteSpace(host.Email) && seen.Add(host.UserId))
                result.Add(new ReminderRecipient(host.UserId, host.Email, host.FullName, true));
        }

        if (includeParticipants)
        {
            // Only ACCEPTED / ASSIGNED participants — never INVITED / DECLINED / REMOVED.
            var parts = await (
                from p in db.VisitParticipants
                join u in db.Users on p.UserId equals u.UserId
                where p.VisitInstanceId == instance.VisitInstanceId
                      && !p.IsHost
                      && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned)
                select new { u.UserId, u.FullName, u.Email }).ToListAsync(ct);

            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p.Email) && seen.Add(p.UserId))
                    result.Add(new ReminderRecipient(p.UserId, p.Email, p.FullName, false));
            }
        }

        return result;
    }

    private static string Render(string template, Dictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(template)) return template;
        return Regex.Replace(template, @"\{\{\s*([\w]+)\s*\}\}", m =>
            context.TryGetValue(m.Groups[1].Value, out var v) ? v ?? string.Empty : m.Value);
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

    private sealed record ReminderRecipient(ulong UserId, string Email, string FullName, bool IsHost);
}
