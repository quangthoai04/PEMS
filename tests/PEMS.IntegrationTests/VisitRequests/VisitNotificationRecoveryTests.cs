using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.VisitNotifications;
using PEMS.Application.Emails.Common;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Email;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Post-commit notifications that survive a mail failure (repair v3 §9, §10, §17 "Notification
/// reliability").
///
/// <para>
/// Both notifications here have the same shape and the same hole. The business transition commits, the
/// message is attempted afterwards, and if that attempt fails there is no way to ask for it again:
/// rejecting the campus a second time is refused because it is already REJECTED, and re-running the
/// expiry sweep finds nothing because it selects PENDING rows and the invitation is EXPIRED. The fix
/// is not to retry the ACTION but to ask a different question — "did this transition ever produce a
/// successful message?" — against the email history the dispatcher writes before it hands anything to
/// a provider.
/// </para>
/// <para>
/// These tests use the real dispatcher, so the <c>sent_emails</c> rows they assert on are the same rows
/// production writes. A fake dispatcher would have skipped exactly the mechanism under test.
/// </para>
/// </summary>
public sealed class VisitNotificationRecoveryTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id) => UserId = id;
        public ulong? UserId { get; }
        public string? Email => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? RoleId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// A mail service that can fail in the two ways that matter to recovery, because they are not the
    /// same failure (plan §40). Every attempt is counted, including the failed ones — "no duplicate on a
    /// successful retry" is a statement about how many messages actually left, so the count has to
    /// include the ones that did not.
    ///
    /// <para>
    /// <see cref="Broken"/> is a configuration refusal — decided before a socket was opened, so no copy
    /// of the message exists anywhere and retrying is provably safe. <see cref="Ambiguous"/> is the SMTP
    /// client throwing, which cannot tell "the server refused it" apart from "the server took it and the
    /// acknowledgement was lost", and therefore must never be retried automatically.
    /// </para>
    /// </summary>
    private sealed class FlakyEmail : IEmailService
    {
        public bool Broken { get; set; }
        public bool Ambiguous { get; set; }
        public List<string> Attempts { get; } = new();
        public List<string> Delivered { get; } = new();

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken ct = default)
        {
            var to = message.To.Count > 0 ? message.To[0].Email : string.Empty;
            Attempts.Add(to);
            if (Ambiguous)
                return Task.FromResult(EmailDeliveryResult.Failed(
                    EmailDeliveryCodes.SmtpSendFailed, "Email delivery failed."));
            if (Broken)
                return Task.FromResult(EmailDeliveryResult.Failed(
                    EmailDeliveryCodes.SmtpMisconfigured, "Email service is not configured."));
            Delivered.Add(to);
            return Task.FromResult(EmailDeliveryResult.Sent());
        }

        public Task SendAsync(OutboundEmail message, CancellationToken ct = default)
            => TrySendAsync(message, ct);

        public Task<EmailDeliveryResult> TrySendAsync(
            string toEmail, string subject, string htmlBody, CancellationToken ct = default)
            => Task.FromResult(EmailDeliveryResult.Sent());
    }

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static ISystemEmailDispatcher Dispatcher(ApplicationDbContext db, FlakyEmail email)
        => new SystemEmailDispatcher(db, new EmailTemplateRenderer(db), email);

    /// <summary>
    /// The retry backoff after one failed attempt, plus margin. A sweep running at the same instant as
    /// the attempt it is meant to repair is correctly told to wait, so every test that asserts a REPAIR
    /// runs the sweep once that wait has elapsed — as production does, where the worker ticks long after
    /// the command that failed.
    /// </summary>
    private static readonly DateTime AfterBackoff = Now.AddHours(1);

    private static RecoverableVisitEmailSender Mailer(
        ApplicationDbContext db, FlakyEmail email, DateTime? at = null)
        => new(db, Dispatcher(db, email), new GrantingLock(), new StoppedClock(at ?? Now),
            NullLogger<RecoverableVisitEmailSender>.Instance);

    private sealed class StoppedClock : IDateTimeService
    {
        private readonly DateTime _now;
        public StoppedClock(DateTime now) => _now = now;
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => _now;
    }

    /// <summary>
    /// Always grants. These tests run one sweep at a time; what they exercise is the ledger, and the
    /// real MySQL claim would only narrow a race none of them create.
    /// </summary>
    private sealed class GrantingLock : IEmailRecoveryLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken ct)
            => Task.FromResult<IAsyncDisposable?>(new Handle());

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static VisitNotificationRecoveryService Recovery(
        ApplicationDbContext db, FlakyEmail email, DateTime? at = null)
        => new(db, new CampusRejectionEmail(db), new ContactInvitationExpiryEmail(db),
            Mailer(db, email, at), NullLogger<VisitNotificationRecoveryService>.Instance);

    private static OperationalContactMaintenanceService Sweeper(ApplicationDbContext db, FlakyEmail email)
        => new(db, NullLogger<OperationalContactMaintenanceService>.Instance,
            new ContactInvitationExpiryEmail(db), Mailer(db, email));

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code, string contactEmail)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn phục hồi", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng Hợp tác", "+8492", contactEmail),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "NR" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task<string> ContactEmailAsync()
    {
        using var db = NewContext();
        return (await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Select(u => u.Email).FirstAsync())!;
    }

    /// <summary>The <c>sent_emails.related_type</c> a rejection notice is filed under.</summary>
    private const string RejectionRelated = "VisitCampusRejectionEvent";

    /// <summary>
    /// Rejects one campus the way the command does — the row AND the immutable audit event that is the
    /// notification's identity. Returns the campus and that event's id.
    ///
    /// <para>
    /// Driven by SQL rather than through the handler because these tests are about the notification, but
    /// the audit row is written all the same: keyed on the event, a rejection without one owes nothing,
    /// and a test that skipped it would be exercising a state the application never produces.
    /// </para>
    /// </summary>
    private static async Task<(ulong InstanceId, ulong EventId)> RejectAsync(
        ulong requestId, string note, DateTime decidedAt, string? campusCode = null)
    {
        using var db = NewContext();
        var target = await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitRequestId == requestId && (campusCode == null || site.CampusCode == campusCode)
            orderby c.VisitInstanceId
            select new { c.VisitInstanceId, c.CampusId }).FirstAsync();

        var instanceId = target.VisitInstanceId;
        var campusId = target.CampusId;

        var leaderId = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                        && u.Status == UserStatuses.Active && u.PrimaryCampusId == campusId)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'REJECTED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_note = {3} WHERE visit_instance_id = {0}",
            instanceId, leaderId, decidedAt, note);

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO audit_logs (actor_user_id, campus_id, action, entity_type, entity_id, " +
            "visit_request_id, visit_instance_id, source_type, reason, created_at) " +
            "VALUES ({0}, {1}, 'REJECT_CAMPUS_INSTANCE', 'VisitRequestCampus', {2}, {3}, {2}, " +
            "'CAMPUS_DECISION', 'decision=REJECTED', {4})",
            leaderId, campusId, instanceId, requestId, decidedAt);

        var eventId = await db.AuditLogs.AsNoTracking()
            .Where(a => a.VisitInstanceId == instanceId && a.Action == "REJECT_CAMPUS_INSTANCE")
            .OrderByDescending(a => a.AuditLogId).Select(a => a.AuditLogId).FirstAsync();

        return (instanceId, eventId);
    }

    /// <summary>Messages recorded against one object for one template, whatever their outcome.</summary>
    private static async Task<List<string>> HistoryAsync(string templateCode, string relatedType, ulong relatedId)
    {
        using var db = NewContext();
        return await (
            from e in db.SentEmails.AsNoTracking()
            join t in db.EmailTemplates.AsNoTracking() on e.EmailTemplateId equals t.EmailTemplateId
            where t.TemplateCode == templateCode && e.RelatedType == relatedType && e.RelatedId == relatedId
            orderby e.SentEmailId
            select e.Status).ToListAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        // sent_emails first — the history rows these tests create point at the campus/invitation rows.
        await Del("DELETE ser FROM sent_email_recipients ser JOIN sent_emails se ON se.sent_email_id = ser.sent_email_id WHERE se.related_type = 'VisitCampusRejectionEvent' AND se.related_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
        await Del("DELETE FROM sent_emails WHERE related_type = 'VisitCampusRejectionEvent' AND related_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
        await Del("DELETE ser FROM sent_email_recipients ser JOIN sent_emails se ON se.sent_email_id = ser.sent_email_id WHERE se.related_type = 'VisitRequestIdentityChange' AND se.related_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM sent_emails WHERE related_type = 'VisitRequestIdentityChange' AND related_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── §10: contact-invitation expiry ────────────────────────────────────────────

    /// <summary>
    /// TC-RECOVERY-01. The expiry commits, the mail fails, and the sweep can never see the row again —
    /// it takes PENDING rows and this one is EXPIRED. The recovery pass finds it anyway, sends once, and
    /// the invitation is never dragged back to PENDING to make that possible.
    /// </summary>
    [Fact]
    public async Task An_expiry_notice_that_failed_is_re_sent_later_without_reverting_the_expiry()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", await ContactEmailAsync()));
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE visit_request_id = {1} AND status = 'PENDING'",
                    Now.AddMinutes(-1), requestId);

            var mail = new FlakyEmail { Broken = true };
            using (var db = NewContext())
                await Sweeper(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);

            ulong changeId;
            using (var db = NewContext())
            {
                var change = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .SingleAsync(c => c.VisitRequestId == requestId);
                changeId = change.IdentityChangeId;
                Assert.Equal(IdentityChangeStatuses.Expired, change.Status);   // the transition stands
            }
            Assert.Single(mail.Attempts);
            Assert.Empty(mail.Delivered);
            Assert.Equal(new[] { "FAILED" },
                await HistoryAsync(SystemEmailTemplates.VisitContactInvitationExpired,
                    "VisitRequestIdentityChange", changeId));

            // Re-running the sweep alone recovers NOTHING: this is the hole the recovery pass fills.
            using (var db = NewContext())
                await Sweeper(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);
            Assert.Single(mail.Attempts);

            // The mail server comes back. The recovery pass sends exactly one message.
            mail.Broken = false;
            using (var db = NewContext())
            {
                var result = await Recovery(db, mail, AfterBackoff).RunOnceAsync(
                    AfterBackoff, 100, CancellationToken.None);
                Assert.Equal(1, result.ContactExpiries);
            }
            Assert.Equal(2, mail.Attempts.Count);
            Assert.Single(mail.Delivered);
            Assert.Equal(V2SeedActor.Email(Registrant), mail.Delivered[0]);

            using (var db = NewContext())
                Assert.Equal(IdentityChangeStatuses.Expired,
                    (await db.VisitRequestIdentityChanges.AsNoTracking()
                        .SingleAsync(c => c.VisitRequestId == requestId)).Status);

            // TC-RECOVERY-02: and it stops. A sweep that re-sent every tick would be worse than one
            // that never sent at all.
            using (var db = NewContext())
            {
                var again = await Recovery(db, mail, AfterBackoff.AddMinutes(10)).RunOnceAsync(
                    AfterBackoff.AddMinutes(10), 100, CancellationToken.None);
                Assert.Equal(0, again.ContactExpiries);
            }
            Assert.Single(mail.Delivered);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-RECOVERY-03. When the first attempt SUCCEEDS, the recovery pass must find nothing at all —
    /// otherwise every expired invitation would be notified twice, once by the sweep and once by the
    /// pass that exists to cover for it.
    /// </summary>
    [Fact]
    public async Task A_delivered_expiry_notice_is_never_sent_a_second_time()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", await ContactEmailAsync()));
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE visit_request_id = {1} AND status = 'PENDING'",
                    Now.AddMinutes(-1), requestId);

            var mail = new FlakyEmail();
            using (var db = NewContext())
                await Sweeper(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);
            Assert.Single(mail.Delivered);

            using (var db = NewContext())
            {
                var result = await Recovery(db, mail).RunOnceAsync(
                    Now.AddMinutes(30), 100, CancellationToken.None);
                Assert.Equal(0, result.ContactExpiries);
            }
            Assert.Single(mail.Delivered);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §9: campus rejection ──────────────────────────────────────────────────────

    /// <summary>
    /// TC-RECOVERY-04. The rejection commits and the mail fails. A repeat Reject cannot be the retry —
    /// the campus is REJECTED and the command refuses — so the recovery pass sends it, once, from the
    /// campus's own stored decision rather than from anything the command still held in memory.
    /// </summary>
    [Fact]
    public async Task A_rejection_email_that_failed_is_re_sent_without_replaying_the_rejection()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));

            // The campus is rejected. Driven directly, because what is under test is the NOTIFICATION —
            // the decision itself has its own suite, and putting a Staff Leader session in here would
            // only add ways for this test to fail for unrelated reasons.
            var (instanceId, eventId) = await RejectAsync(
                requestId, "Cơ sở không thu xếp được", Now.AddMinutes(-30));

            var mail = new FlakyEmail { Broken = true };
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(
                    new CampusRejectionEmail(db), eventId, CancellationToken.None);

            Assert.Single(mail.Attempts);
            Assert.Empty(mail.Delivered);
            Assert.Equal(new[] { "FAILED" },
                await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, eventId));

            mail.Broken = false;
            using (var db = NewContext())
            {
                var result = await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None);
                Assert.Equal(1, result.Rejections);
            }

            var delivered = Assert.Single(mail.Delivered);
            Assert.Equal(V2SeedActor.Email(Registrant), delivered);

            // TC-RECOVERY-05: no duplicate once it has gone out.
            using (var db = NewContext())
                Assert.Equal(0, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);
            Assert.Single(mail.Delivered);

            // The rejection itself was never touched by any of this.
            using (var db = NewContext())
            {
                var campus = await db.VisitRequestCampuses.AsNoTracking()
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal(VisitInstanceStatuses.Rejected, campus.Status);
                Assert.Equal("Cơ sở không thu xếp được", campus.DecisionNote);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §38. The same campus is rejected twice, and the SECOND notification fails.
    ///
    /// <para>
    /// This is the case the campus-keyed ledger got wrong. A campus can be rejected, resubmitted and
    /// rejected again; keyed on the campus, the first rejection's delivered email answered "has this
    /// been notified?" with yes forever, and the second rejection — the one the registrant had not
    /// heard about — was never retried. Keyed on the rejection EVENT, the two are separate debts.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_delivered_rejection_does_not_suppress_a_later_rejection_of_the_same_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));

            // ── Reject #1 → delivered ──
            var mail = new FlakyEmail();
            var (instanceId, firstEvent) = await RejectAsync(
                requestId, "Lần một: hết chỗ", Now.AddMinutes(-90));
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), firstEvent, CancellationToken.None);
            Assert.Single(mail.Delivered);

            // ── Resubmitted and rejected again → the mail fails this time ──
            var (_, secondEvent) = await RejectAsync(
                requestId, "Lần hai: trùng lịch", Now.AddMinutes(-30));
            Assert.NotEqual(firstEvent, secondEvent);

            mail.Broken = true;
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), secondEvent, CancellationToken.None);
            Assert.Single(mail.Delivered);      // still only #1
            Assert.Equal(new[] { "FAILED" },
                await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, secondEvent));

            // ── The sweep owes #2 and settles it, without sending #1 again ──
            mail.Broken = false;
            using (var db = NewContext())
                Assert.Equal(1, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);

            Assert.Equal(2, mail.Delivered.Count);
            Assert.Equal(new[] { "SENT" },
                await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, firstEvent));
            Assert.Equal(new[] { "FAILED", "SENT" },
                await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, secondEvent));

            // A further sweep owes nothing at all.
            using (var db = NewContext())
                Assert.Equal(0, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);
            Assert.Equal(2, mail.Delivered.Count);

            // The superseded event stays superseded: asked directly, it declines to say anything,
            // because its reason is no longer the campus's reason.
            using (var db = NewContext())
            {
                var outcome = await Mailer(db, mail).SendOnceAsync(
                    new CampusRejectionEmail(db), firstEvent, CancellationToken.None);
                Assert.Equal(RecoverableEmailOutcome.AlreadySent, outcome);
                var campus = await db.VisitRequestCampuses.AsNoTracking()
                    .SingleAsync(c => c.VisitInstanceId == instanceId);
                Assert.Equal("Lần hai: trùng lịch", campus.DecisionNote);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §38. Two campuses of one request are rejected. Each owes its own notification, and settling one
    /// leaves the other outstanding — the sibling isolation that the per-campus decision model promises.
    /// </summary>
    [Fact]
    public async Task Rejections_of_two_campuses_are_independent_notifications()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(
                Campus("HN", V2SeedActor.Email(Registrant)),
                Campus("DN", V2SeedActor.Email(Registrant)));

            var (_, hn) = await RejectAsync(requestId, "HN từ chối", Now.AddMinutes(-40), "HN");
            var (_, dn) = await RejectAsync(requestId, "DN từ chối", Now.AddMinutes(-40), "DN");
            Assert.NotEqual(hn, dn);

            // HN's message gets out; DN's fails.
            var mail = new FlakyEmail();
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), hn, CancellationToken.None);

            mail.Broken = true;
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), dn, CancellationToken.None);
            Assert.Single(mail.Delivered);

            mail.Broken = false;
            using (var db = NewContext())
                Assert.Equal(1, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);

            Assert.Equal(2, mail.Delivered.Count);
            Assert.Equal(new[] { "SENT" }, await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, hn));
            Assert.Equal(new[] { "FAILED", "SENT" }, await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, dn));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §42. An attempt whose delivery cannot be established is never resent automatically. The row is
    /// left QUEUED with no recorded outcome — the crash window between writing it and hearing back —
    /// and the recipient may be holding that copy.
    /// </summary>
    [Fact]
    public async Task An_attempt_with_an_unprovable_outcome_is_never_resent_automatically()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));
            var (_, eventId) = await RejectAsync(requestId, "Không rõ kết quả gửi", Now.AddMinutes(-30));

            var mail = new FlakyEmail { Broken = true };
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), eventId, CancellationToken.None);

            // Rewritten to look like a process that died mid-send: QUEUED, and nothing saying why.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE sent_emails SET status = 'QUEUED', error_message = NULL " +
                    "WHERE related_type = {0} AND related_id = {1}", RejectionRelated, eventId);

            mail.Broken = false;
            using (var db = NewContext())
            {
                var outcome = await Mailer(db, mail).SendOnceAsync(
                    new CampusRejectionEmail(db), eventId, CancellationToken.None);
                Assert.Equal(RecoverableEmailOutcome.OutcomeUnknown, outcome);

                Assert.Equal(0, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);
            }

            Assert.Empty(mail.Delivered);
            Assert.Single(await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, eventId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §41. An SMTP client that threw is NOT a clean failure. The provider may have taken the message
    /// before the connection broke, so this FAILED row — unlike a configuration refusal — buys no retry.
    /// </summary>
    [Fact]
    public async Task A_failed_send_that_may_have_reached_the_provider_is_not_retried()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));
            var (_, eventId) = await RejectAsync(requestId, "SMTP ném lỗi", Now.AddMinutes(-30));

            var mail = new FlakyEmail { Ambiguous = true };
            using (var db = NewContext())
                await Mailer(db, mail).SendOnceAsync(new CampusRejectionEmail(db), eventId, CancellationToken.None);
            Assert.Single(mail.Attempts);

            mail.Ambiguous = false;
            using (var db = NewContext())
                Assert.Equal(0, (await Recovery(db, mail, AfterBackoff).RunOnceAsync(AfterBackoff, 100, CancellationToken.None)).Rejections);

            Assert.Empty(mail.Delivered);
            Assert.Single(mail.Attempts);   // the sweep did not even try
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §13.4 EXHAUSTION-01. Five failed attempts is the cap. There is no sixth automatic send, and the
    /// event does not vanish: the five FAILED rows stay in the email history, which is exactly what the
    /// runbook's exhaustion query looks for.
    /// </summary>
    [Fact]
    public async Task After_five_failed_attempts_no_sixth_is_sent_and_the_event_stays_findable()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));
            var (_, eventId) = await RejectAsync(requestId, "Máy chủ mail hỏng", Now.AddMinutes(-30));

            // A configuration failure — provably pre-outbound, so each attempt is legitimately retried
            // until the cap. Each sweep runs later than the last so backoff never masks the cap.
            var mail = new FlakyEmail { Broken = true };
            var at = AfterBackoff;
            for (var i = 0; i < RecoverableVisitEmailSender.MaxAttempts; i++)
            {
                using var db = NewContext();
                await Mailer(db, mail, at).SendOnceAsync(
                    new CampusRejectionEmail(db), eventId, CancellationToken.None);
                at = at.AddHours(12);
            }

            Assert.Equal(RecoverableVisitEmailSender.MaxAttempts, mail.Attempts.Count);
            var history = await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, eventId);
            Assert.Equal(RecoverableVisitEmailSender.MaxAttempts, history.Count);
            Assert.All(history, s => Assert.Equal("FAILED", s));

            // The cap holds even once the mail server is healthy again: a person has to look at it.
            mail.Broken = false;
            using (var db = NewContext())
            {
                var outcome = await Mailer(db, mail, at).SendOnceAsync(
                    new CampusRejectionEmail(db), eventId, CancellationToken.None);
                Assert.Equal(RecoverableEmailOutcome.Exhausted, outcome);

                Assert.Equal(0, (await Recovery(db, mail, at).RunOnceAsync(at, 100, CancellationToken.None)).Rejections);
            }

            Assert.Equal(RecoverableVisitEmailSender.MaxAttempts, mail.Attempts.Count);   // no sixth
            Assert.Empty(mail.Delivered);

            // Findable exactly the way the runbook says: attempts >= cap and nothing SENT.
            using (var db = NewContext())
            {
                var exhausted = await (
                    from e in db.SentEmails.AsNoTracking()
                    join t in db.EmailTemplates.AsNoTracking() on e.EmailTemplateId equals t.EmailTemplateId
                    where t.TemplateCode == SystemEmailTemplates.VisitCampusRejected
                          && e.RelatedType == RejectionRelated && e.RelatedId == eventId
                    group e by e.RelatedId into g
                    where g.Count() >= RecoverableVisitEmailSender.MaxAttempts
                          && !g.Any(x => x.Status == "SENT")
                    select g.Key).ToListAsync();

                Assert.Equal(new ulong?[] { eventId }, exhausted);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §13.5 CONCURRENCY-01. Two workers reach for the same event at the same moment; the MySQL
    /// advisory claim is exclusive, so only one of them may dispatch.
    ///
    /// <para>
    /// Exercised against the real database rather than by reading the source: the first worker's claim
    /// is held open while the second asks for it, and the second is refused. The sender's own use of
    /// that claim is proven by the second half — a sender whose claim is already held sends nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Two_workers_cannot_both_dispatch_the_same_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));
            var (_, eventId) = await RejectAsync(requestId, "Hai worker cùng lúc", Now.AddMinutes(-30));

            var key = $"pems:visit-notify:{RejectionRelated}:{eventId}";

            using var holderDb = NewContext();
            using var rivalDb = NewContext();
            var holderLock = new MySqlEmailRecoveryLock(holderDb, NullLogger<MySqlEmailRecoveryLock>.Instance);
            var rivalLock = new MySqlEmailRecoveryLock(rivalDb, NullLogger<MySqlEmailRecoveryLock>.Instance);

            // Worker 1 takes the claim and keeps it.
            var claim = await holderLock.TryAcquireAsync(key, CancellationToken.None);
            Assert.NotNull(claim);

            try
            {
                // Worker 2, on its own connection, is refused without waiting.
                Assert.Null(await rivalLock.TryAcquireAsync(key, CancellationToken.None));

                // …and a sender using that refused claim sends nothing at all.
                var mail = new FlakyEmail();
                var rival = new RecoverableVisitEmailSender(
                    rivalDb, Dispatcher(rivalDb, mail), rivalLock, new StoppedClock(AfterBackoff),
                    NullLogger<RecoverableVisitEmailSender>.Instance);

                var outcome = await rival.SendOnceAsync(
                    new CampusRejectionEmail(rivalDb), eventId, CancellationToken.None);

                Assert.Equal(RecoverableEmailOutcome.Deferred, outcome);
                Assert.Empty(mail.Attempts);
                Assert.Empty(await HistoryAsync(SystemEmailTemplates.VisitCampusRejected, RejectionRelated, eventId));
            }
            finally { await claim!.DisposeAsync(); }

            // Once released, the work is still owed — deferring never loses it.
            using (var db = NewContext())
            {
                var mail = new FlakyEmail();
                var outcome = await Mailer(db, mail, AfterBackoff).SendOnceAsync(
                    new CampusRejectionEmail(db), eventId, CancellationToken.None);
                Assert.Equal(RecoverableEmailOutcome.Sent, outcome);
                Assert.Single(mail.Delivered);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-RECOVERY-06. A transition the inline attempt has not had a chance at yet is left alone. Without
    /// the settle window the sweep would race the command that caused it and send the message twice —
    /// the ledger can only prevent a duplicate once the FIRST attempt has been recorded.
    /// </summary>
    [Fact]
    public async Task A_transition_younger_than_the_settle_window_is_left_to_its_own_send()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));

            await RejectAsync(requestId, "vừa mới từ chối", Now);   // decided just now

            // Swept at that same instant, so the settle window is what has to hold it back — not the
            // backoff, which has nothing to back off from yet.
            var mail = new FlakyEmail();
            using (var db = NewContext())
            {
                var result = await Recovery(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);
                Assert.Equal(0, result.Total);
            }
            Assert.Empty(mail.Attempts);
        }
        finally { await CleanupAsync(requestId); }
    }
}
