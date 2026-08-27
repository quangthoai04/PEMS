using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Shared;

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

/// <summary>
/// Why a due reminder was cancelled instead of sent. These codes are written into
/// <c>visit_instance_reminder_settings.error_message</c>, which is one free-text column — so the code
/// goes in front of the sentence, machine-readable prefix first, rather than in a column of its own.
/// </summary>
public static class ReminderCancelReasons
{
    /// <summary>Nobody was left to remind: no Host, no eligible participant, no usable address.</summary>
    public const string NoEligibleRecipients = "NO_ELIGIBLE_RECIPIENTS";

    public const string NoEligibleRecipientsMessageVi =
        "Đã hủy nhắc lịch vì không còn người nhận đủ điều kiện.";

    public const string NoEligibleRecipientsMessageEn =
        "The reminder was cancelled because no eligible recipients remained.";

    /// <summary>
    /// The instance is no longer BEFORE_VISIT at the moment this reminder came due — cancelled,
    /// rejected, or already advanced to during/after/closed. A "chuyến thăm sắp diễn ra" reminder has
    /// nothing left to say once that is true, however it got that way.
    /// </summary>
    public const string VisitNoLongerEligible = "VISIT_NO_LONGER_ELIGIBLE";

    public const string VisitNoLongerEligibleMessageVi =
        "Đã hủy nhắc lịch vì chuyến thăm không còn ở giai đoạn chuẩn bị.";

    /// <summary>
    /// The instance's planned start moved after this reminder was scheduled, and the offset against
    /// the NEW start has already passed. See <see cref="VisitReminderLifecycleSync"/>.
    /// </summary>
    public const string ScheduleNoLongerValid = "SCHEDULE_NO_LONGER_VALID";

    public const string ScheduleNoLongerValidMessageVi =
        "Đã hủy nhắc lịch vì thời gian chuyến thăm đã đổi và mốc nhắc mới đã qua.";

    /// <summary>What the row stores: <c>CODE: message</c>, so an operator and a parser both read it.</summary>
    public static string Record(string code) => code switch
    {
        NoEligibleRecipients => $"{NoEligibleRecipients}: {NoEligibleRecipientsMessageVi}",
        VisitNoLongerEligible => $"{VisitNoLongerEligible}: {VisitNoLongerEligibleMessageVi}",
        ScheduleNoLongerValid => $"{ScheduleNoLongerValid}: {ScheduleNoLongerValidMessageVi}",
        _ => code,
    };
}

/// <summary>
/// What one reminder produced. <paramref name="SafeError"/> is null when nothing failed;
/// <paramref name="CancelReasonCode"/> is set when there was nothing to send and the reminder must
/// NOT be left claiming it went out.
/// </summary>
public sealed record ReminderDispatchOutcome(int Messages, string? SafeError, string? CancelReasonCode = null)
{
    public bool Succeeded => SafeError is null;

    /// <summary>Nothing failed, but nothing was sent either — the row belongs in CANCELLED.</summary>
    public bool Cancelled => CancelReasonCode is not null;

    public static readonly ReminderDispatchOutcome Nothing = new(0, null);

    public static readonly ReminderDispatchOutcome NoEligibleRecipients =
        new(0, null, ReminderCancelReasons.NoEligibleRecipients);

    public static readonly ReminderDispatchOutcome VisitNoLongerEligible =
        new(0, null, ReminderCancelReasons.VisitNoLongerEligible);
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
    private readonly ILogger<VisitReminderDispatchService>? _logger;

    public VisitReminderDispatchService(
        IApplicationDbContext db,
        ISystemEmailDispatcher dispatcher,
        IDateTimeService clock,
        IEmailActionTokenService urls,
        INotificationService notifications,
        ILogger<VisitReminderDispatchService>? logger = null)
    {
        _db = db;
        _dispatcher = dispatcher;
        _clock = clock;
        _urls = urls;
        _notifications = notifications;
        _logger = logger;
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

        // Oldest-first with a bounded batch means no reminder starves — a backlog bigger than one
        // batch is worked off across successive ticks, in the same order, never left behind. This is
        // only a visibility signal for an operator, not a correctness concern.
        if (due.Count == BatchSize)
            _logger?.LogWarning(
                "Visit reminder backlog at capacity ({BatchSize}) this tick; more may still be due.", BatchSize);

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
            {
                await MarkFailedAsync(reminder.ReminderSettingId, outcome.SafeError!, now, cancellationToken);
            }
            else if (outcome.Cancelled)
            {
                // Nothing failed and nothing was sent. Leaving the claim's SENT in place would have the
                // row assert a delivery that never happened, so it is moved to CANCELLED with the reason.
                await MarkCancelledAsync(
                    reminder.ReminderSettingId, outcome.CancelReasonCode!, now, cancellationToken);
            }
            else
            {
                dispatched++;
            }
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
    ///
    /// <para>
    /// <c>r.ScheduledAt &lt;= now</c> is repeated here on purpose, even though the caller's own SELECT
    /// already filtered on it: this UPDATE's WHERE is evaluated against the row's CURRENT state at the
    /// database, not against the in-memory copy the SELECT returned a moment earlier. Without it, a
    /// concurrent Save that pushed this same row's schedule into the future (Host reschedules while the
    /// tick is mid-flight) would still satisfy "id + PENDING" and the claim would win — sending the
    /// STALE reminder for a time the row no longer represents. Re-checking the live schedule here means
    /// that race loses the claim instead: the reminder is left PENDING for a later tick to pick up
    /// against whatever schedule actually stands by then.
    /// </para>
    /// </summary>
    private async Task<bool> ClaimAsync(ulong reminderSettingId, DateTime now, CancellationToken ct)
    {
        var claimed = await _db.VisitInstanceReminderSettings
            .Where(r => r.ReminderSettingId == reminderSettingId
                        && r.Status == VisitReminderStatus.PENDING
                        && r.ScheduledAt <= now)
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

    /// <summary>
    /// Records a reminder that had nothing to send.
    ///
    /// <para>
    /// The row is left alone once it is already CANCELLED, so a second pass cannot rewrite the reason
    /// or the timestamp of the first one. It cannot be picked up again in any case — the due query
    /// only ever looks at PENDING rows — so this is belt-and-braces against a caller that dispatches a
    /// reminder directly. No email is sent, no <c>sent_emails</c> row is written, and nothing is
    /// retried: there is no failure here to retry.
    /// </para>
    /// </summary>
    private Task MarkCancelledAsync(ulong reminderSettingId, string reasonCode, DateTime now, CancellationToken ct)
    {
        // No recipient, template or message content is logged — the reminder id is enough to
        // investigate with, and the alternative puts mailboxes in the application log.
        _logger?.LogInformation(
            "Visit reminder {ReminderSettingId} cancelled: {ReasonCode}.", reminderSettingId, reasonCode);

        return _db.VisitInstanceReminderSettings
            .Where(r => r.ReminderSettingId == reminderSettingId
                        && r.Status != VisitReminderStatus.CANCELLED)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, VisitReminderStatus.CANCELLED)
                    .SetProperty(r => r.LastDispatchedAt, now)
                    .SetProperty(r => r.ErrorMessage, ReminderCancelReasons.Record(reasonCode))
                    .SetProperty(r => r.UpdatedAt, now),
                ct);
    }

    public async Task<ReminderDispatchOutcome> DispatchOneAsync(
        VisitInstanceReminderSetting reminder, CancellationToken cancellationToken = default)
    {
        var instance = await _db.VisitRequestCampuses
            .AsNoTracking()
            .Include(c => c.FormDetail)
            .FirstOrDefaultAsync(c => c.VisitInstanceId == reminder.VisitInstanceId, cancellationToken)
            ?? throw new InvalidOperationException($"Visit instance {reminder.VisitInstanceId} not found.");

        // Layer 2 of the eligibility defence (Layer 1 is VisitReminderLifecycleSync, called from the
        // lifecycle commands themselves). The row is claimed already, so this is the last chance to
        // refuse to send: a PENDING row that Layer 1 did not catch — a write path added later, a direct
        // DB edit, a status change that landed between the claim and here — must still never fire once
        // the instance is no longer BEFORE_VISIT. "Chuyến thăm sắp diễn ra" has nothing to say to a
        // cancelled, rejected, or already-under-way visit, however it got that way.
        if (instance.Status != VisitInstanceStatus.BeforeVisit)
            return ReminderDispatchOutcome.VisitNoLongerEligible;

        var recipients = await ResolveRecipientsAsync(instance, reminder.TargetGroup, reminder.Channel, cancellationToken);
        // Nobody left to remind — the Host was cleared, the participants withdrew, or none of them has
        // a usable address. The provider is never called and no sent_emails row is written, because
        // there was no send attempt to record; the caller moves the row to CANCELLED with the reason.
        if (recipients.Count == 0) return ReminderDispatchOutcome.NoEligibleRecipients;

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
        // Relation-aware routing (plan RC-05/RM-01..03): a STAFF recipient can be the current Host of
        // THIS instance, or merely an accepted participant of it (e.g. Host of a different delegation
        // reminded here only as support) — one shared process URL for everyone silently walked a
        // non-Host participant into the Host-only operational screen. The Host gets the real Host
        // Process entry; every other recipient gets their own contribution screen instead, never the
        // other's page.
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
            ActionType: r.IsHost ? NotificationActionTypes.OpenHostProcess : NotificationActionTypes.OpenContribution,
            ActionUrl: r.IsHost
                ? $"/dashboard/visit/process/{instance.VisitInstanceId}"
                : $"/dashboard/visit/contribution/{instance.VisitInstanceId}",
            DedupeKey: $"REMINDER_{reminder.ReminderSettingId}_USER_{r.UserId}",
            MetadataJson: NotificationEventKeys.BuildMetadata(
                NotificationEventKeys.VisitReminder,
                new { delegationName, campusName, isHost = r.IsHost })
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
        var plannedStart = Moment(instance.PlannedStartAt);
        var plannedEnd = Moment(instance.PlannedEndAt);

        var failures = 0;
        var skipped = 0;

        foreach (var r in recipients)
        {
            // Per-recipient, not built once outside the loop: a Host and a participant read the same
            // reminder template family but must land on their OWN screen — the Host on the operational
            // Host Process page, everyone else on their own contribution page. One shared URL here used
            // to silently walk every participant into the Host-only screen.
            var detailBlock = new Dictionary<string, string>
            {
                [EmailTrustedBlocks.ActionBlock] = EmailComposition.VisitDetailBlock(
                    r.IsHost
                        ? _urls.BuildHostVisitProcessUrl(instance.VisitInstanceId)
                        : _urls.BuildVisitContributionUrl(instance.VisitInstanceId)),
            };

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
                    RelatedId: instance.VisitInstanceId)
                {
                },
                ct);

            // Skipped is SMTP being off outside production. It is neither success nor failure, and it
            // must not turn a whole reminder into FAILED — nothing went wrong.
            if (result.Delivery.Status == EmailDeliveryStatus.Failed) failures++;
            else if (result.Delivery.Status == EmailDeliveryStatus.Skipped) skipped++;
        }

        // The reminder row still ends SENT below when nothing FAILED — that is unchanged production
        // behaviour (Skipped is not a failure and must not be reported as one). This log is the only
        // place the distinction survives: without it, a SENT row after a local run with SMTP disabled
        // reads identically to a real delivery, and nothing else records that no message actually left
        // the process.
        if (skipped > 0)
            _logger?.LogInformation(
                "{Skipped}/{Total} visit reminder emails were SKIPPED (SMTP disabled) for instance {VisitInstanceId} — recorded as SENT per existing policy, but no message was actually delivered.",
                skipped, recipients.Count, instance.VisitInstanceId);

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
        VisitRequestCampus instance, VisitReminderTargetGroup targetGroup, VisitReminderChannel channel,
        CancellationToken ct)
    {
        var result = new List<ReminderRecipient>();
        // One notification/email per USER regardless of channel — a Host who is also an accepted
        // participant is reminded once, as the Host.
        var seenUsers = new HashSet<ulong>();
        // Mailbox dedupe is an EMAIL-only concern: two different accounts sharing one address must each
        // still get their OWN in-app notification (their identity is the UserId, not the mailbox), but
        // must not receive the same email twice — the person behind the shared inbox reads it once.
        var seenMailboxes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Add(ulong userId, string? email, string fullName, bool isHost)
        {
            if (!seenUsers.Add(userId)) return false;

            if (channel == VisitReminderChannel.EMAIL)
            {
                // IN_APP never reaches this branch, so an EMAIL reminder still correctly requires a
                // usable address and still dedupes by mailbox — unchanged from before this split.
                if (string.IsNullOrWhiteSpace(email)) return false;
                var address = email!.Trim();
                if (!seenMailboxes.Add(address)) return false;
                result.Add(new ReminderRecipient(userId, address, fullName, isHost));
                return true;
            }

            // IN_APP identity is the UserId alone. SendInAppAsync never reads ReminderRecipient.Email,
            // so a missing address is not a reason to skip this person — an account with no email on
            // file (or one it shares with another account) must still see the notification.
            result.Add(new ReminderRecipient(userId, email?.Trim() ?? string.Empty, fullName, isHost));
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
