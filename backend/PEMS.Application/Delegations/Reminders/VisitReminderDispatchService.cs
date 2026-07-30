using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;

namespace PEMS.Application.Delegations.Reminders;

/// <summary>
/// Sends the scheduled visit reminders. Split out of the hosted service so the decisions it makes —
/// who is reminded, from which template, and how "already sent" is decided — can be tested without a
/// running host, and so the background job is left doing only what a background job should: waking up.
/// </summary>
public interface IVisitReminderDispatchService
{
    /// <summary>Claims and dispatches every reminder that is due. Returns how many it dispatched.</summary>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches ONE already-claimed reminder. Public for the tests that need to assert on what a
    /// single reminder produces without going through the claim.
    /// </summary>
    Task<ReminderDispatchOutcome> DispatchOneAsync(
        VisitInstanceReminderSetting reminder, CancellationToken cancellationToken = default);
}

/// <summary>What one reminder produced. <paramref name="SafeError"/> is null when nothing failed.</summary>
public sealed record ReminderDispatchOutcome(int Messages, string? SafeError)
{
    public bool Succeeded => SafeError is null;
    public static readonly ReminderDispatchOutcome Nothing = new(0, null);
}

/// <inheritdoc />
public sealed class VisitReminderDispatchService : IVisitReminderDispatchService
{
    /// <summary>How many due reminders one tick will look at. Matches the previous behaviour.</summary>
    private const int BatchSize = 50;

    private readonly IApplicationDbContext _db;
    private readonly ISystemEmailDispatcher _dispatcher;
    private readonly IDateTimeService _clock;
    private readonly IEmailActionTokenService _urls;
    private readonly INotificationService _notifications;

    public VisitReminderDispatchService(
        IApplicationDbContext db,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock,
        IEmailActionTokenService urls,
        INotificationService notifications)
    {
        _db = db;
        _dispatcher = dispatcher;
        _clock = clock;
        _urls = urls;
        _notifications = notifications;
    }

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken = default)
    {
        // scheduled_at is stored as Vietnam wall-clock, so compare against Vietnam-local "now"
        // (never UtcNow — that would fire reminders ~7h off).
        var now = _clock.VietnamNow;

        var due = await _db.VisitInstanceReminderSettings
            .AsNoTracking()
            .Where(r => r.Status == VisitReminderStatus.PENDING && r.ScheduledAt <= now)
            .OrderBy(r => r.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var dispatched = 0;

        foreach (var reminder in due)
        {
            // Claim before doing anything. See ClaimAsync for why the claim moves the row to SENT
            // rather than to an intermediate state.
            if (!await ClaimAsync(reminder.ReminderSettingId, now, cancellationToken)) continue;

            ReminderDispatchOutcome outcome;
            try
            {
                outcome = await DispatchOneAsync(reminder, cancellationToken);
            }
            catch (Exception)
            {
                // The exception text is not recorded: a template or SMTP fault can name an internal
                // host, a file path or the message itself, and error_message is shown to operators.
                outcome = new ReminderDispatchOutcome(0, "Không gửi được nhắc lịch cho chuyến tiếp khách này.");
            }

            if (!outcome.Succeeded)
                await MarkFailedAsync(reminder.ReminderSettingId, outcome.SafeError!, now, cancellationToken);
            else
                dispatched++;
        }

        return dispatched;
    }

    /// <summary>
    /// Takes ownership of a reminder with a single conditional UPDATE, and reports whether this caller
    /// won. Two processes polling the same second both see the row as PENDING; only the one whose UPDATE
    /// matches a PENDING row gets a rowcount of 1, and the other simply moves on.
    ///
    /// <para>
    /// The claim moves the row straight to SENT — deliberately at-most-once. If this process dies
    /// between the claim and the send, the reminder is not sent and is not retried. The alternative
    /// (claim after sending, or retry anything still PENDING) risks the case where SMTP accepted the
    /// message but the status write never landed, and re-sending then delivers a duplicate reminder to a
    /// real person. A missed reminder is recoverable by a human; a duplicate is not recallable, and the
    /// schema has no state that would let us tell the two apart afterwards.
    /// </para>
    /// </summary>
    private async Task<bool> ClaimAsync(ulong reminderSettingId, DateTime now, CancellationToken ct)
    {
        var claimed = await _db.VisitInstanceReminderSettings
            .Where(r => r.ReminderSettingId == reminderSettingId
                        && r.Status == VisitReminderStatus.PENDING)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, VisitReminderStatus.SENT)
                    .SetProperty(r => r.LastDispatchedAt, now)
                    .SetProperty(r => r.ErrorMessage, (string?)null)
                    .SetProperty(r => r.UpdatedAt, now),
                ct);

        return claimed == 1;
    }

    private Task MarkFailedAsync(ulong reminderSettingId, string safeError, DateTime now, CancellationToken ct)
        => _db.VisitInstanceReminderSettings
            .Where(r => r.ReminderSettingId == reminderSettingId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, VisitReminderStatus.FAILED)
                    .SetProperty(r => r.LastDispatchedAt, now)
                    .SetProperty(r => r.ErrorMessage, safeError)
                    .SetProperty(r => r.UpdatedAt, now),
                ct);

    public async Task<ReminderDispatchOutcome> DispatchOneAsync(
        VisitInstanceReminderSetting reminder, CancellationToken cancellationToken = default)
    {
        var instance = await _db.VisitRequestCampuses
            .AsNoTracking()
            .Include(c => c.FormDetail)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == reminder.VisitInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Visit instance {reminder.VisitInstanceId} not found.");

        var recipients = await ResolveRecipientsAsync(instance, reminder.TargetGroup, cancellationToken);
        if (recipients.Count == 0) return ReminderDispatchOutcome.Nothing;

        // The reminder targets ONE campus instance, so every value comes from THAT instance — never
        // from a sibling campus of the same request, which would tell people the wrong time and place.
        var delegationName = instance.FormDetail?.DelegationName ?? "FPT University";
        var campusName = await _db.Campuses
            .Where(c => c.CampusId == instance.CampusId).Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "FPT University";

        if (reminder.Channel == VisitReminderChannel.IN_APP)
        {
            await SendInAppAsync(instance, reminder, recipients, delegationName, campusName, cancellationToken);
            return new ReminderDispatchOutcome(recipients.Count, null);
        }

        return await SendEmailsAsync(
            instance, recipients, delegationName, campusName, cancellationToken);
    }

    private Task SendInAppAsync(
        VisitRequestCampus instance, VisitInstanceReminderSetting reminder,
        IReadOnlyList<ReminderRecipient> recipients, string delegationName, string campusName,
        CancellationToken ct)
    {
        var requests = recipients.Select(r => new CreateNotificationRequest(
            RecipientUserId: r.UserId,
            Title: "Nhắc lịch tiếp khách",
            Message: $"Đoàn {delegationName} tại {campusName} sẽ diễn ra vào {Moment(instance.PlannedStartAt)}.",
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

        return _notifications.CreateManyAsync(requests, ct);
    }

    /// <summary>
    /// One message per person, always. A reminder that named the other participants would tell each of
    /// them who else is on the visit and how many — which is neither theirs to know nor anything the
    /// reminder needs to say.
    /// </summary>
    private async Task<ReminderDispatchOutcome> SendEmailsAsync(
        VisitRequestCampus instance, IReadOnlyList<ReminderRecipient> recipients,
        string delegationName, string campusName, CancellationToken ct)
    {
        var detailBlock = new Dictionary<string, string>
        {
            [EmailTrustedBlocks.ActionBlock] = EmailComposition.VisitDetailBlock(
                _urls.BuildVisitInstanceDetailUrl(instance.VisitRequestId, instance.VisitInstanceId)),
        };

        var plannedStart = Moment(instance.PlannedStartAt);
        var plannedEnd = Moment(instance.PlannedEndAt);

        var failures = 0;

        foreach (var r in recipients)
        {
            var result = await _dispatcher.SendAsync(
                new SystemEmailRequest(
                    r.IsHost
                        ? SystemEmailTemplates.VisitReminderHost
                        : SystemEmailTemplates.VisitReminderParticipants,
                    new EmailRecipient(r.Email, r.FullName),
                    r.IsHost
                        ? new Dictionary<string, string>
                        {
                            ["hostName"] = r.FullName,
                            ["delegationName"] = delegationName,
                            ["campusName"] = campusName,
                            ["plannedStart"] = plannedStart,
                            ["plannedEnd"] = plannedEnd,
                        }
                        : new Dictionary<string, string>
                        {
                            ["recipientName"] = r.FullName,
                            ["delegationName"] = delegationName,
                            ["campusName"] = campusName,
                            ["plannedStart"] = plannedStart,
                            ["plannedEnd"] = plannedEnd,
                        },
                    TrustedBlocks: detailBlock,
                    RelatedType: ReminderRelatedType,
                    RelatedId: instance.VisitInstanceId),
                ct);

            // Skipped is SMTP being off outside production. It is neither success nor failure, and it
            // must not turn a whole reminder into FAILED — nothing went wrong.
            if (result.Delivery.Status == EmailDeliveryStatus.Failed) failures++;
        }

        // Partial failure has no state of its own: the schema knows PENDING/SENT/CANCELLED/FAILED and
        // nothing else. A reminder where some messages failed is reported FAILED so an operator sees it,
        // and the ones that DID go out are never re-sent — a FAILED reminder is not picked up again.
        return failures == 0
            ? new ReminderDispatchOutcome(recipients.Count, null)
            : new ReminderDispatchOutcome(
                recipients.Count - failures,
                $"{failures}/{recipients.Count} email nhắc lịch không gửi được.");
    }

    /// <summary>The <c>sent_emails.related_type</c> a reminder is filed under (unchanged).</summary>
    public const string ReminderRelatedType = "VISIT_INSTANCE";

    private async Task<List<ReminderRecipient>> ResolveRecipientsAsync(
        VisitRequestCampus instance, VisitReminderTargetGroup targetGroup, CancellationToken ct)
    {
        var result = new List<ReminderRecipient>();
        var seenUsers = new HashSet<ulong>();
        // …and mailboxes, not just user ids: two accounts can share one address, and the person behind
        // it should not get the same reminder twice because of how the system models them.
        var seenMailboxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Add(ulong userId, string? email, string fullName, bool isHost)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var address = email!.Trim();
            if (!seenUsers.Add(userId)) return false;
            if (!seenMailboxes.Add(address)) return false;

            result.Add(new ReminderRecipient(userId, address, fullName, isHost));
            return true;
        }

        var includeHost = targetGroup is VisitReminderTargetGroup.HOST or VisitReminderTargetGroup.HOST_AND_PARTICIPANTS;
        var includeParticipants = targetGroup is VisitReminderTargetGroup.PARTICIPANTS or VisitReminderTargetGroup.HOST_AND_PARTICIPANTS;

        // The Host goes first, so that a Host who also holds a participant row is reminded as the Host —
        // the role that carries the responsibility the reminder is about.
        if (includeHost && instance.CurrentHostUserId.HasValue)
        {
            var host = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == instance.CurrentHostUserId.Value)
                .Select(u => new { u.UserId, u.FullName, u.Email })
                .FirstOrDefaultAsync(ct);

            if (host is not null) Add(host.UserId, host.Email, host.FullName, isHost: true);
        }

        if (includeParticipants)
        {
            // Only ACCEPTED / ASSIGNED participants of THIS instance — never INVITED / DECLINED /
            // REMOVED, and never a sibling campus's people.
            var parts = await (
                from p in _db.VisitParticipants.AsNoTracking()
                join u in _db.Users.AsNoTracking() on p.UserId equals u.UserId
                where p.VisitInstanceId == instance.VisitInstanceId
                      && !p.IsHost
                      && (p.Status == ParticipantStatuses.Accepted || p.Status == ParticipantStatuses.Assigned)
                select new { u.UserId, u.FullName, u.Email }).ToListAsync(ct);

            foreach (var p in parts) Add(p.UserId, p.Email, p.FullName, isHost: false);
        }

        return result;
    }

    /// <summary>One formatter for both templates, so Host and participant read the same wall clock.</summary>
    private static string Moment(DateTime value) => value.ToString("HH:mm dd/MM/yyyy");

    private sealed record ReminderRecipient(ulong UserId, string Email, string FullName, bool IsHost);
}
