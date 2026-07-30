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

            // Asserted BEFORE the message count, so a reminder that never entered the batch is reported as
            // exactly that rather than as "0 messages", which is what made the earlier failure opaque.
            // DispatchDueAsync sweeps the newest 50 due reminders globally, so a database holding enough
            // older due rows could leave this one unprocessed — a real possibility on a shared database,
            // and one worth naming in the assertion instead of discovering through an empty directory.
            using (var claimed = EmailEvidenceHarness.NewContext())
            {
                var after = await claimed.VisitInstanceReminderSettings.AsNoTracking()
                    .SingleAsync(r => r.ReminderSettingId == _reminderId);
                Assert.Equal(VisitReminderStatus.SENT, after.Status);
            }

            Assert.Single(MessagesTo(_h.Marker));

            using var verify = EmailEvidenceHarness.NewContext();
            Assert.Equal(1, await verify.SentEmailRecipients.AsNoTracking()
                .CountAsync(r => r.RecipientEmail == _h.Marker));
        }
        finally { await CleanupAsync(); }
    }

    /// <summary>
    /// The race, twenty times over, in one process.
    ///
    /// <para>
    /// A single race that passes proves very little: the two workers have to actually overlap for the
    /// conditional UPDATE to be exercised, and on a fast machine one can finish before the other starts.
    /// Repeating it makes the overlap likely rather than hoped for, and makes a regression that only
    /// shows up occasionally show up here instead of in an unrelated suite three runs later.
    /// </para>
    /// <para>
    /// Each iteration seeds and cleans up its own rows, so a failure names the iteration it happened on
    /// and leaves nothing behind for the next one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Racing_workers_produce_one_set_of_messages_on_every_one_of_twenty_attempts()
    {
        EmailEvidenceHarness.RequireDb();

        const int Attempts = 20;

        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                await SeedAsync(VisitReminderTargetGroup.HOST);

                using var dbA = EmailEvidenceHarness.NewContext();
                using var dbB = EmailEvidenceHarness.NewContext();

                await Task.WhenAll(
                    Task.Run(() => Service(dbA).DispatchDueAsync()),
                    Task.Run(() => Service(dbB).DispatchDueAsync()));

                using (var verify = EmailEvidenceHarness.NewContext())
                {
                    var after = await verify.VisitInstanceReminderSettings.AsNoTracking()
                        .SingleAsync(r => r.ReminderSettingId == _reminderId);
                    Assert.Equal(VisitReminderStatus.SENT, after.Status);

                    Assert.Equal(1, await verify.SentEmailRecipients.AsNoTracking()
                        .CountAsync(r => r.RecipientEmail == _h.Marker));
                }

                // One message, on this attempt, from this iteration's own mailbox — the pickup directory
                // is cleaned between attempts so the count cannot accumulate into a false pass.
                var messages = MessagesTo(_h.Marker);
                Assert.True(messages.Count == 1,
                    $"Attempt {attempt} of {Attempts} produced {messages.Count} messages, expected exactly 1.");
            }
            finally
            {
                await CleanupAsync();
                _h.ClearMessages();
            }
        }
    }

    /// <summary>
    /// The suite writes nothing to a row it did not create.
    ///
    /// <para>
    /// Pinned as a test because the previous fixture DID: it rewrote the first ACTIVE Staff Leader's email
    /// address, and the administrator's, for the duration of each test. Both are rows other suites read.
    /// A comment would not have stopped that coming back.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Seeding_never_changes_a_user_it_did_not_create()
    {
        EmailEvidenceHarness.RequireDb();

        Dictionary<ulong, string> Snapshot(ApplicationDbContext db) => db.Users.AsNoTracking()
            .Select(u => new { u.UserId, u.Email }).ToDictionary(u => u.UserId, u => u.Email);

        Dictionary<ulong, string> before;
        using (var db = EmailEvidenceHarness.NewContext()) before = Snapshot(db);

        try
        {
            await SeedAsync(VisitReminderTargetGroup.HOST_AND_PARTICIPANTS, withParticipant: true);

            using var db = EmailEvidenceHarness.NewContext();
            var after = Snapshot(db);

            foreach (var (userId, email) in before)
            {
                Assert.True(after.TryGetValue(userId, out var now),
                    $"User {userId} disappeared during seeding.");
                Assert.Equal(email, now);
            }
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

        // The approving Staff Leader is READ, never written: it supplies the campus and the decision
        // metadata the schema requires, and nothing about this suite changes it.
        var leader = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == "STAFF" && u.SubRole == "LEADER"
                        && u.Status == "ACTIVE" && u.PrimaryCampusId != null)
            .OrderBy(u => u.UserId)
            .Select(u => new { u.UserId, u.RoleId, u.DepartmentId, CampusId = u.PrimaryCampusId!.Value })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "The disposable database needs at least one ACTIVE Staff Leader with a primary campus.");

        var campusId = leader.CampusId;

        // ── The Host is a user this suite OWNS ───────────────────────────────
        //
        // It used to borrow that Staff Leader and overwrite its email with the marker for the duration
        // of the test. That is a mutation of a row selected by a NON-UNIQUE predicate ("the first ACTIVE
        // Staff Leader") on a database xUnit shares between test classes running in parallel — so for as
        // long as one test here was running, every other suite that read that Staff Leader saw
        // `batch8-idempotency@partner.example.com` as its address. Whether that mattered depended on
        // which classes happened to overlap, which is exactly the shape of a defect that appears once and
        // then hides. Creating our own row removes the shared write entirely; the dispatcher cannot tell
        // the difference, because all it does is read `current_host_user_id`.
        //
        // sub_role STAFF, deliberately NOT LEADER: a campus must have exactly one Staff Leader
        // (BR-86-19/20), and adding a second would make the campus configuration-invalid and break every
        // visit-create test that runs against it. The host-assignment trigger requires only that the host
        // fields are populated, not that the host holds any particular role, so plain staff is both
        // sufficient here and safe for everyone else. sub_role and department_id are both mandatory for
        // STAFF (trg_users_validate_*), and the department is the Leader's own — same campus, IC type.
        var hostUser = new PEMS.Domain.Entities.Users.User
        {
            FullName = "Reminder Host " + Guid.NewGuid().ToString("N")[..8],
            Email = _h.Marker,
            RoleId = leader.RoleId,
            SubRole = "STAFF",
            DepartmentId = leader.DepartmentId,
            Status = "ACTIVE",
            PrimaryCampusId = campusId,
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(hostUser);
        await db.SaveChangesAsync();

        var hostUserId = hostUser.UserId;
        _createdUserIds.Add(hostUserId);

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
            // Assigned and decided BY the Staff Leader — the shape production produces, and it keeps this
            // suite's only write on rows it created itself.
            HostAssignedBy = leader.UserId,
            HostAssignedAt = DateTime.Now,
            DecidedBy = leader.UserId,
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
            // Also owned by this suite. The previous version took "the first user with an email" —
            // `admin@fpt.edu.vn`, user 1 — and rewrote the administrator's address while the test ran.
            var participantUser = new PEMS.Domain.Entities.Users.User
            {
                FullName = "Reminder Participant " + Guid.NewGuid().ToString("N")[..8],
                Email = ParticipantAddress,
                RoleId = leader.RoleId,
                SubRole = "STAFF",
                DepartmentId = leader.DepartmentId,
                Status = "ACTIVE",
                PrimaryCampusId = campusId,
                CreatedAt = DateTime.Now,
            };
            db.Users.Add(participantUser);
            await db.SaveChangesAsync();

            var participantUserId = participantUser.UserId;
            _createdUserIds.Add(participantUserId);

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

    /// <summary>Users this suite created, deleted on the way out. Nothing else is ever written.</summary>
    private readonly List<ulong> _createdUserIds = new();

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

        // Remove the users this suite created. Nothing is "restored", because nothing shared was changed.
        if (_createdUserIds.Count > 0)
        {
            var ids = _createdUserIds.ToList();
            await db.Users.Where(u => ids.Contains(u.UserId)).ExecuteDeleteAsync();
            _createdUserIds.Clear();
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
