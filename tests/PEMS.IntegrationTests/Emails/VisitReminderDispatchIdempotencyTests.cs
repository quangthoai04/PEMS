using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Reminders;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.IntegrationTests.TestInfrastructure;
using Xunit;

namespace PEMS.IntegrationTests.Emails;

/// <summary>
/// Batch 8 — proof that a reminder is dispatched at most once, against a real MySQL row.
///
/// <para>
/// This is the part of the reminder job that cannot be checked by reading the code: two API instances
/// polling the same second both see the row as PENDING, and the previous job would have had both of them
/// send. A duplicate reminder reaches a real person and cannot be recalled, so the claim is a single
/// conditional UPDATE and the winner is whoever the database says it is. The tests below race two
/// services on two connections and assert exactly one set of messages came out.
/// </para>
/// </summary>
public sealed class VisitReminderDispatchIdempotencyTests : IDisposable
{
    private readonly EmailEvidenceHarness _h = new("batch8-idempotency@partner.example.com");

    private ulong _visitRequestId;
    private ulong _visitInstanceId;
    private ulong _reminderId;

    public void Dispose() => _h.Dispose();

    // ── The whole point ─────────────────────────────────────────────────────

    [Fact]
    public async Task Running_the_same_tick_twice_sends_one_set_of_messages()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST);

            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            Assert.Single(MessagesTo(_h.Marker));

            // Second tick, same clock, same data.
            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            Assert.Single(MessagesTo(_h.Marker));

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Equal(1, await verify.SentEmailRecipients.AsNoTracking()
                .CountAsync(r => r.RecipientEmail == _h.Marker));

            var reminder = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.ReminderSettingId == _reminderId);
            Assert.Equal(VisitReminderStatus.SENT, reminder.Status);
            Assert.NotNull(reminder.LastDispatchedAt);
            Assert.Null(reminder.ErrorMessage);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Two_workers_racing_the_same_reminder_produce_one_set_of_messages()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST);

            using var dbA = EmailEvidenceHarness.NewContext();
            using var dbB = EmailEvidenceHarness.NewContext();

            // Two connections, two services, one row. Whoever's UPDATE matches a PENDING row wins;
            // the loser's rowcount is 0 and it sends nothing.
            await Task.WhenAll(
                Task.Run(() => Service(dbA).DispatchDueAsync()),
                Task.Run(() => Service(dbB).DispatchDueAsync()));

            Assert.Single(MessagesTo(_h.Marker));

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Equal(1, await verify.SentEmailRecipients.AsNoTracking()
                .CountAsync(r => r.RecipientEmail == _h.Marker));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_reminder_that_is_not_due_yet_is_left_alone()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST, scheduledAt: DateTime.Now.AddDays(3));

            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            Assert.Empty(MessagesTo(_h.Marker));

            using var verify = EmailEvidenceHarness.NewContext();
            var reminder = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.ReminderSettingId == _reminderId);
            Assert.Equal(VisitReminderStatus.PENDING, reminder.Status);
            Assert.Null(reminder.LastDispatchedAt);
        }
        finally { await CleanupAsync(); }
    }

    [Theory]
    [InlineData(VisitReminderStatus.SENT)]
    [InlineData(VisitReminderStatus.CANCELLED)]
    [InlineData(VisitReminderStatus.FAILED)]
    public async Task A_reminder_that_is_not_pending_is_never_dispatched(VisitReminderStatus status)
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST, status: status);

            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            Assert.Empty(MessagesTo(_h.Marker));

            using var verify = EmailEvidenceHarness.NewContext();
            // A FAILED reminder is NOT retried: some of its messages may already have gone out, and the
            // schema keeps no record of which — re-running it could duplicate a real email.
            Assert.Equal(status, (await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.ReminderSettingId == _reminderId)).Status);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task A_provider_failure_marks_the_reminder_failed_with_a_safe_message()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST);

            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db, brokenHost: "127.0.0.1").DispatchDueAsync();

            using var verify = EmailEvidenceHarness.NewContext();
            var reminder = await verify.VisitInstanceReminderSettings.AsNoTracking()
                .SingleAsync(r => r.ReminderSettingId == _reminderId);

            Assert.Equal(VisitReminderStatus.FAILED, reminder.Status);
            Assert.NotNull(reminder.LastDispatchedAt);
            Assert.Equal("1/1 email nhắc lịch không gửi được.", reminder.ErrorMessage);
            // No SMTP host, no exception text, no address.
            Assert.DoesNotContain("127.0.0.1", reminder.ErrorMessage!);
            Assert.DoesNotContain(_h.Marker, reminder.ErrorMessage!);

            var stored = await verify.SentEmails.AsNoTracking()
                .SingleAsync(e => e.RelatedId == _visitInstanceId && e.RelatedType == "VISIT_INSTANCE");
            Assert.Equal("FAILED", stored.Status);
            Assert.Null(stored.SentAt);

            // …and a failed reminder is not picked up again on the next tick.
            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            Assert.Empty(MessagesTo(_h.Marker));
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task Every_recipient_of_one_reminder_gets_their_own_message()
    {
        EmailEvidenceHarness.RequireDb();
        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS, withParticipant: true);

            using (var db = EmailEvidenceHarness.NewContext())
                await Service(db).DispatchDueAsync();

            // Two people, two separate MIME messages — never one message addressed to both.
            var mine = MessagesTo(_h.Marker).Concat(MessagesTo(ParticipantAddress)).ToList();
            Assert.Equal(2, mine.Count);
            foreach (var eml in mine)
            {
                Assert.Equal(1, eml.AddressCount("To"));
                Assert.Equal(string.Empty, eml.Header("Cc"));
                Assert.Equal(string.Empty, eml.Header("Bcc"));
            }

            using var verify = EmailEvidenceHarness.NewContext();
            var sent = await verify.SentEmails.AsNoTracking()
                .Where(e => e.RelatedType == "VISIT_INSTANCE" && e.RelatedId == _visitInstanceId)
                .ToListAsync();
            Assert.Equal(2, sent.Count);
            Assert.All(sent, e => Assert.Equal("SENT", e.Status));

            // The Host and the participant were rendered from DIFFERENT templates.
            Assert.Equal(2, sent.Select(e => e.EmailTemplateId).Distinct().Count());
        }
        finally { await CleanupAsync(); }
    }

    // ── Rig ─────────────────────────────────────────────────────────────────

    private const string ParticipantAddress = "batch8-participant@partner.example.com";

    private VisitReminderDispatchService Service(ApplicationDbContext db, string? brokenHost = null)
        => new(db, _h.Dispatcher(db, brokenHost), new NowClock(), new StubUrls(), new NoNotifications());

    /// <summary>
    /// The produced messages addressed to one mailbox. Scoped on purpose: the dispatch loop is global by
    /// design, so a count of "every file in the pickup directory" would be an assertion about whatever
    /// else the shared test database happens to hold, not about this test.
    /// </summary>
    private List<EmlMessage> MessagesTo(string address)
        => _h.Messages()
            .Select(path => new EmlMessage(System.IO.File.ReadAllText(path)))
            .Where(m => m.Header("To").Contains(address, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private async Task SeedAsync(
        VisitReminderTargetGroup target,
        DateTime? scheduledAt = null,
        VisitReminderStatus status = VisitReminderStatus.PENDING,
        bool withParticipant = false)
    {
        using var db = EmailEvidenceHarness.NewContext();

        // The schema only accepts an operational instance whose host is IC Staff of that campus, or the
        // approving Staff Leader themself. Taking the Leader satisfies host AND decider in one person,
        // which is a shape production really produces (self-host approval).
        var leader = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER"
                        && u.Status == "ACTIVE" && u.PrimaryCampusId != null)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, CampusId = u.PrimaryCampusId!.Value })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "pems_pr3_test needs at least one ACTIVE Staff Leader with a primary campus.");

        var campusId = leader.CampusId;
        var hostUserId = leader.UserId;

        // The Host's mailbox is the marker, so cleanup keys on it exactly as the other suites do.
        var host = await db.Users.SingleAsync(u => u.UserId == hostUserId);
        _originalHostEmail = host.Email;
        host.Email = _h.Marker;

        var request = new VisitRequest
        {
            RequestCode = "RMD-" + Guid.NewGuid().ToString("N")[..12],
            RegistrantFullName = "Nguyễn Văn Khách",
            RegistrantNationality = "VN",
            RegistrantOrganization = "Đối tác",
            RegistrantJobTitle = "Trưởng đoàn",
            RegistrantPhone = "0900000000",
            RegistrantEmail = "reminder-guest@partner.example.com",
            ContactPersonFullName = "Đầu mối",
            ContactPersonOrganization = "Đối tác",
            ContactPersonPhone = "0900000001",
            ContactPersonEmail = "reminder-contact@partner.example.com",
            Status = "APPROVED",
            SubmittedAt = DateTime.Now,
            CreatedAt = DateTime.Now,
        };
        db.VisitRequests.Add(request);
        await db.SaveChangesAsync();

        // The schema refuses an operational campus instance that has no official host and no decision
        // metadata (trigger "Approved/operational campus instance requires official host assignment"),
        // so the seed supplies both rather than inserting a shape production could never produce.
        var instance = new VisitRequestCampus
        {
            VisitRequestId = request.VisitRequestId,
            CampusId = campusId,
            PlannedStartAt = DateTime.Now.AddDays(2),
            PlannedEndAt = DateTime.Now.AddDays(2).AddHours(2),
            Status = PEMS.Shared.VisitInstanceStatus.BeforeVisit,
            CurrentHostUserId = hostUserId,
            HostAssignedBy = hostUserId,
            HostAssignedAt = DateTime.Now,
            DecidedBy = hostUserId,
            DecidedAt = DateTime.Now,
            DecisionActorRole = "STAFF_LEADER",
            CreatedAt = DateTime.Now,
            FormDetail = new VisitInstanceFormDetail
            {
                DelegationName = "Đoàn nhắc lịch",
                VisitType = "MEETING",
                Purpose = "Tham quan",
                WorkingContent = "Nội dung",
                OperationalContactFullName = "Đầu mối cơ sở",
                OperationalContactOrganization = "Đối tác",
                OperationalContactPhone = "0900000002",
                OperationalContactEmail = "reminder-op@partner.example.com",
                WorkingLanguage = "EN",
                MediaConsentStatus = "AGREED",
                FormRevision = 1,
                ApprovalRevision = 1,
                CreatedAt = DateTime.Now,
            },
        };
        db.VisitRequestCampuses.Add(instance);
        await db.SaveChangesAsync();

        if (withParticipant)
        {
            var participantUserId = await db.Users.AsNoTracking()
                .Where(u => u.UserId != hostUserId && u.Email != null && u.Email != "")
                .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();

            var participantUser = await db.Users.SingleAsync(u => u.UserId == participantUserId);
            _participantUserId = participantUserId;
            _originalParticipantEmail = participantUser.Email;
            participantUser.Email = ParticipantAddress;

            db.VisitParticipants.Add(new VisitParticipant
            {
                VisitInstanceId = instance.VisitInstanceId,
                UserId = participantUserId,
                ParticipantRole = "IC_SUPPORT",
                IsHost = false,
                Status = "ACCEPTED",
                CreatedAt = DateTime.Now,
            });
        }

        db.VisitInstanceReminderSettings.Add(new VisitInstanceReminderSetting
        {
            VisitInstanceId = instance.VisitInstanceId,
            Channel = VisitReminderChannel.EMAIL,
            TargetGroup = target,
            DaysBefore = 1,
            ReminderTime = new TimeSpan(8, 0, 0),
            ScheduledAt = scheduledAt ?? DateTime.Now.AddMinutes(-5),
            Status = status,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        _visitRequestId = request.VisitRequestId;
        _visitInstanceId = instance.VisitInstanceId;
        _reminderId = await db.VisitInstanceReminderSettings.AsNoTracking()
            .Where(r => r.VisitInstanceId == instance.VisitInstanceId)
            .Select(r => r.ReminderSettingId).SingleAsync();
    }

    private string? _originalHostEmail;
    private string? _originalParticipantEmail;
    private ulong? _participantUserId;

    private async Task CleanupAsync()
    {
        using var db = EmailEvidenceHarness.NewContext();

        // Email history first (it keys on the marker), then the visit rows this test created.
        await _h.CleanupAsync();
        await db.SentEmailRecipients
            .Where(r => r.RecipientEmail == ParticipantAddress).ExecuteDeleteAsync();
        await db.SentEmails
            .Where(e => e.RelatedType == "VISIT_INSTANCE" && e.RelatedId == _visitInstanceId).ExecuteDeleteAsync();

        if (_visitInstanceId != 0)
        {
            await db.VisitInstanceReminderSettings
                .Where(r => r.VisitInstanceId == _visitInstanceId).ExecuteDeleteAsync();
            await db.VisitParticipants
                .Where(p => p.VisitInstanceId == _visitInstanceId).ExecuteDeleteAsync();
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM visit_instance_form_details WHERE visit_instance_id = {0}", _visitInstanceId);
            await db.VisitRequestCampuses
                .Where(c => c.VisitInstanceId == _visitInstanceId).ExecuteDeleteAsync();
        }

        if (_visitRequestId != 0)
            await db.VisitRequests.Where(v => v.VisitRequestId == _visitRequestId).ExecuteDeleteAsync();

        // Put the borrowed mailboxes back exactly as they were.
        if (_originalHostEmail is not null)
        {
            await db.Users.Where(u => u.Email == _h.Marker)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, _originalHostEmail));
            _originalHostEmail = null;
        }
        if (_participantUserId is { } pid && _originalParticipantEmail is not null)
        {
            await db.Users.Where(u => u.UserId == pid)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, _originalParticipantEmail));
            _originalParticipantEmail = null;
            _participantUserId = null;
        }

        _visitInstanceId = 0;
        _visitRequestId = 0;
    }

    private sealed class NowClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => DateTime.Now;
    }

    private sealed class StubUrls : IEmailActionTokenService
    {
        public string GenerateRawToken() => Guid.NewGuid().ToString("N");
        public string Hash(string rawToken) => "h:" + rawToken;
        public string BuildPublicActionUrl(string rawToken) => "https://pems.test/public/" + rawToken;
        public string BuildDepartmentAssignmentUrl(ulong visitInstanceId, ulong participantId) => "https://pems.test/assign";
        public string BuildLogisticsDetailUrl(ulong logisticsItemId) => "https://pems.test/logistics";
        public string BuildVisitInstanceDetailUrl(ulong visitRequestId, ulong visitInstanceId)
            => $"https://pems.test/dashboard/visit/process/{visitRequestId}/{visitInstanceId}";
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType,
            string? relatedType, ulong? relatedId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct)
            => Task.CompletedTask;

        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct)
            => Task.CompletedTask;

        public Task CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
