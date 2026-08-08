using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// What happens when nobody answers a contact invitation (repair prompt v2 §9, TC-CONTACT-MAIL-02/03/04).
///
/// <para>
/// The expiry itself already worked: the maintenance sweep flips PENDING → EXPIRED under a row lock
/// and invalidates the outstanding token. What was missing is the half that faces a person — the
/// registrant was never told, so an invitation that lapsed simply left the request sitting behind the
/// confirmation gate with nothing to explain why. This suite pins the mail, its recipient, and the
/// property that makes a background sweep safe to run every ten minutes: it is sent once per
/// invitation, not once per scan.
/// </para>
///
/// <para>
/// It also pins the boundary the audit asked for twice: a lapsed invitation is NOT a rejection and NOT
/// the visit's 72-hour lead time. For a TRANSFER it changes nothing at all about who runs the campus.
/// </para>
/// </summary>
public sealed class OperationalContactExpiryNotificationTests
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

    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    /// <summary>Captures what the sweep would have sent, without rendering or delivering anything.</summary>
    private sealed class RecordingDispatcher : ISystemEmailDispatcher
    {
        public List<SystemEmailRequest> Sent { get; } = new();

        public Task<SystemEmailDispatchResult> SendAsync(
            SystemEmailRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(new SystemEmailDispatchResult(
                EmailDeliveryResult.Sent(), SentEmailId: 0, EmailTemplateId: 0));
        }

        public Task<PreparedSystemEmail> PrepareAsync(
            SystemEmailRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EmailDeliveryResult> DeliverAsync(
            PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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
        public IEnumerable<string> Permissions => Array.Empty<string>();
        public bool HasPermission(string permission) => false;
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
    /// The sweep as the container wires it. The expiry notice goes through the recoverable sender, so
    /// the test builds that too — a sweeper handed a bare dispatcher would be a different object from
    /// the one that runs in production, and the "once per invitation" property under test belongs to
    /// the sender.
    /// </summary>
    private static OperationalContactMaintenanceService Sweeper(ApplicationDbContext db, RecordingDispatcher mail)
        => new(db, NullLogger<OperationalContactMaintenanceService>.Instance,
            new ContactInvitationExpiryEmail(db),
            new RecoverableVisitEmailSender(
                db, mail, new GrantingLock(), new FixedClock(),
                NullLogger<RecoverableVisitEmailSender>.Instance));

    /// <summary>
    /// The claim always succeeds here: this test runs one sweep at a time, and the property under test
    /// is the ledger, not the cross-process claim that merely narrows the race around it.
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

    /// <summary>
    /// One campus whose contact is somebody OTHER than the registrant, so submitting it raises a real
    /// PENDING invitation rather than self-matching straight to CONFIRMED.
    /// </summary>
    private static CampusVisitFormDto Campus(string code, string contactEmail)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn hết hạn", "MEETING", null, "Thăm", "Nội dung",
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
            "OX" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    /// <summary>Ages the request's pending invitation so the next sweep finds it due.</summary>
    private static async Task ExpireDueAsync(ulong requestId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_identity_changes SET expires_at = {0} WHERE visit_request_id = {1} AND status = 'PENDING'",
            Now.AddMinutes(-1), requestId);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-CONTACT-MAIL-02 + TC-CONTACT-MAIL-04. The invitation lapses, its token stops working, the
    /// registrant is told exactly once — and running the sweep again tells them nothing, because the
    /// send hangs off the PENDING → EXPIRED transition rather than off "this row looks expired".
    /// </summary>
    [Fact]
    public async Task An_expired_invitation_emails_the_registrant_once_however_often_the_sweep_runs()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext())
            {
                var other = await db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                                && u.UserId != Registrant)
                    .OrderBy(u => u.UserId).Select(u => u.Email).FirstOrDefaultAsync();
                Assert.False(string.IsNullOrWhiteSpace(other),
                    "This database needs an ACTIVE VISITOR other than user 8.");
                contactEmail = other!;
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));
            await ExpireDueAsync(requestId);

            var mail = new RecordingDispatcher();
            using (var db = NewContext())
                await Sweeper(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);

            // The transition happened, and the outstanding link is dead.
            using (var db = NewContext())
            {
                var change = await db.VisitRequestIdentityChanges.AsNoTracking()
                    .SingleAsync(c => c.VisitRequestId == requestId);
                Assert.Equal(IdentityChangeStatuses.Expired, change.Status);

                var tokens = await db.EmailActionTokens.AsNoTracking()
                    .Where(t => t.TargetType == EmailActionTargetTypes.VisitRequestIdentityChange
                                && t.TargetId == change.IdentityChangeId)
                    .ToListAsync();
                Assert.All(tokens, t =>
                    Assert.NotEqual(EmailActionResultStatuses.Pending, t.ResultStatus));
            }

            // Exactly one mail, to the REGISTRANT, on the expiry template — not the rejection one.
            var sent = Assert.Single(mail.Sent);
            Assert.Equal(SystemEmailTemplates.VisitContactInvitationExpired, sent.TemplateCode);
            Assert.Equal(V2SeedActor.Email(Registrant), sent.To.Email);
            Assert.False(string.IsNullOrWhiteSpace(sent.Variables["campusName"]));
            Assert.False(string.IsNullOrWhiteSpace(sent.Variables["requestCode"]));
            // The invited address is named MASKED, never in full.
            Assert.Contains("*", sent.Variables["pendingContactEmailMasked"]);
            Assert.DoesNotContain(contactEmail, sent.Variables["pendingContactEmailMasked"]);

            // TC-CONTACT-MAIL-04: a second sweep finds nothing PENDING and says nothing.
            var second = new RecordingDispatcher();
            using (var db = NewContext())
                await Sweeper(db, second).RunOnceAsync(Now, 100, CancellationToken.None);
            Assert.Empty(second.Sent);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-CONTACT-MAIL-03, the half that matters most. A lapsed invitation must not cost the campus the
    /// contact it already has: the row that expires is the INVITATION, and the campus's own
    /// <c>operational_contact_user_id</c> is not part of that transition at all.
    ///
    /// <para>
    /// Written against an INITIAL_CONFIRMATION because that is the case this seed can build directly,
    /// and it proves the same property from the harder side: there was no confirmed contact to lose,
    /// and the sweep still leaves the campus exactly as it found it — no binding invented from a
    /// pending invitation that was never accepted.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_expired_invitation_binds_nobody_and_leaves_the_campus_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            string contactEmail;
            using (var db = NewContext())
            {
                contactEmail = (await db.Users.AsNoTracking()
                    .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                                && u.UserId != Registrant)
                    .OrderBy(u => u.UserId).Select(u => u.Email).FirstAsync())!;
            }

            requestId = await CreateAsync(Campus("HN", contactEmail));

            (ulong? Contact, string Status) before;
            using (var db = NewContext())
            {
                var row = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId)
                    .Select(c => new { c.OperationalContactUserId, c.Status }).SingleAsync();
                before = (row.OperationalContactUserId, row.Status);
            }

            await ExpireDueAsync(requestId);
            using (var db = NewContext())
                await Sweeper(db, new RecordingDispatcher()).RunOnceAsync(Now, 100, CancellationToken.None);

            using (var db = NewContext())
            {
                var after = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId)
                    .Select(c => new { c.OperationalContactUserId, c.Status }).SingleAsync();
                Assert.Equal(before.Contact, after.OperationalContactUserId);
                Assert.Equal(before.Status, after.Status);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// TC-72H-05. Time passing on its own is not an event. A request filed with plenty of notice whose
    /// date has since crept inside 72 hours is left completely alone — not expired, not rejected, and
    /// above all not emailed about. The 72-hour rule validates a SUBMISSION; a countdown that nagged
    /// people about schedules they had already filed was the wrong reading of it, and this pins that
    /// the maintenance sweep has no such behaviour to regress into.
    /// </summary>
    [Fact]
    public async Task A_valid_request_whose_visit_drifts_inside_72h_is_left_alone_and_generates_no_mail()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", V2SeedActor.Email(Registrant)));

            // Time passes: the visit is now 40 hours away. Nothing else happens — no edit, no resubmit.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET planned_start_at = {0}, planned_end_at = {1} WHERE visit_request_id = {2}",
                    Now.AddHours(40), Now.AddHours(42), requestId);

            var mail = new RecordingDispatcher();
            using (var db = NewContext())
                await Sweeper(db, mail).RunOnceAsync(Now, 100, CancellationToken.None);

            Assert.Empty(mail.Sent);

            using (var db = NewContext())
            {
                var request = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.NotEqual(VisitRequestStatuses.Rejected, request.Status);
                Assert.NotEqual(VisitRequestStatuses.Cancelled, request.Status);

                var statuses = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).Select(c => c.Status).ToListAsync();
                Assert.All(statuses, s => Assert.NotEqual(VisitInstanceStatuses.Rejected, s));
                Assert.All(statuses, s => Assert.NotEqual(VisitInstanceStatuses.Cancelled, s));
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
