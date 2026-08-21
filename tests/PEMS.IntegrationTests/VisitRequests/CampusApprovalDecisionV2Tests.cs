using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.RejectCampusInstance;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using NotificationTypes = PEMS.Application.Notifications.Common.NotificationTypes;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 2D — per-campus APPROVE (+ assign host) and REJECT against a disposable copy of pems_pr3_test.
///
/// These two commands had no executing test at all: the only files that named them lived in
/// tests/PEMS.ApplicationTests, a directory with no .csproj that is not in PEMS.slnx and therefore has
/// never compiled. Everything asserted here is the runtime behaviour, not a reading of the handler.
///
/// The invariant under test is the one Pure V2 stands on: a decision is scoped to ONE campus instance.
/// It must not touch a sibling's status, host, participants or form detail, and the text it emits must
/// come from the deciding instance's own detail — which is the only place a delegation name exists,
/// since visit_requests carries none.
/// </summary>
public sealed class CampusApprovalDecisionV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    // Seed actors (canonical seed). Campus 1 = HN, 2 = HCM, 3 = DN.
    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;      // STAFF/LEADER, campus 1
    private const ulong LeaderHcm = 9;     // STAFF/LEADER, campus 2
    private const ulong LeaderDn = 11;     // STAFF/LEADER, campus 3
    private const ulong IcStaffHn = 101;   // STAFF/STAFF, IC, campus 1
    private const ulong IcStaffHcm = 103;  // STAFF/STAFF, IC, campus 2
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;
    private const ulong CampusDn = 3;

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
        public FakeUser(ulong id, string roleCode = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            UserId = id;
            RoleCode = roleCode;
            SubRole = subRole;
            PrimaryCampusId = campusId;
        }

        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Leader(ulong id, ulong campusId)
        => new(id, RoleCodes.Staff, UserSubRoles.Leader, campusId);

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<CreateNotificationRequest> Sent { get; } = new();
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct)
        {
            Sent.AddRange(requests);
            return Task.CompletedTask;
        }
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static ApproveCampusInstanceCommandHandler ApproveHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications, IDateTimeService? clock = null)
        => new(db, actor, clock ?? new FixedClock(),
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), notifications,
                new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                NullLogger<CampusApprovalExecutor>.Instance));

    private static RejectCampusInstanceCommandHandler RejectHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications,
        RecordingDispatcher? dispatcher = null, IDateTimeService? clock = null)
        => new(db, actor, clock ?? new FixedClock(), new VisitRequestAggregateStatusService(db), notifications,
            new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
            // The rejection email is built from the campus and sent through the recoverable sender —
            // the same two objects the container wires, so what these tests exercise is what runs.
            new PEMS.Application.Delegations.VisitNotifications.CampusRejectionEmail(db),
            new PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender(
                db, dispatcher ?? new RecordingDispatcher(), new GrantingLock(), clock ?? new FixedClock(),
                NullLogger<PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender>.Instance));

    private static ResubmitRejectedVisitInstanceV2CommandHandler ResubmitHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications, IDateTimeService? clock = null)
        => new(db, actor, clock ?? new FixedClock(),
            new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db)),
            notifications, NullLogger<ResubmitRejectedVisitInstanceV2CommandHandler>.Instance, WriteOn);

    private static GetVisitRequestHistoryQueryHandler HistoryHandler(ApplicationDbContext db, FakeUser actor)
        => new(db, actor, ReadOn);

    private static GetVisitHistoryDetailQueryHandler DetailHandler(ApplicationDbContext db, FakeUser actor)
        => new(db, actor, ReadOn);

    /// <summary>Strictly increasing timestamps across successive handler calls in one test — needed to
    /// assert chronological ordering (Fix Group B cases B3/B4), unlike the shared <see cref="FixedClock"/>
    /// every other test in this file uses, which intentionally freezes "now" at one instant.</summary>
    private sealed class SteppingClock : IDateTimeService
    {
        private readonly DateTime _start;
        private int _ticks;
        public SteppingClock(DateTime start) => _start = start;
        public DateTime UtcNow => _start.AddMinutes(_ticks++);
        public DateTime VietnamNow => _start.AddMinutes(_ticks++);
    }

    /// <summary>Resubmit payload for ONE previously-rejected instance — same shape as <c>Content</c> in
    /// the per-campus pending-edit suite, built from the lightweight <see cref="InstanceState"/> read
    /// projection this file already uses instead of a tracked entity.</summary>
    private static CampusVisitEditV2Dto ResubmitContent(InstanceState state, CampusVisitFormDto content)
        => new(state.VisitInstanceId, state.RowVersion,
            content.CampusId, content.PlannedStartAt, content.PlannedEndAt,
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers, content.OperationalContact,
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus,
            content.Notes);

    /// <summary>Always grants: these tests send one message at a time.</summary>
    private sealed class GrantingLock : PEMS.Application.Delegations.VisitNotifications.IEmailRecoveryLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken ct)
            => Task.FromResult<IAsyncDisposable?>(new Handle());

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Captures what the rejection email would have been, without rendering or sending one. The
    /// dispatcher is called AFTER the transaction commits, so for the cases that only care about the
    /// decision it just has to not throw; the two cases that DO care read <see cref="Sent"/>.
    /// </summary>
    internal sealed class RecordingDispatcher : PEMS.Application.Emails.Common.ISystemEmailDispatcher
    {
        public List<PEMS.Application.Emails.Common.SystemEmailRequest> Sent { get; } = new();

        public Task<PEMS.Application.Emails.Common.SystemEmailDispatchResult> SendAsync(
            PEMS.Application.Emails.Common.SystemEmailRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(new PEMS.Application.Emails.Common.SystemEmailDispatchResult(
                PEMS.Application.Common.Interfaces.EmailDeliveryResult.Sent(), SentEmailId: 0, EmailTemplateId: 0));
        }

        public Task<PEMS.Application.Emails.Common.PreparedSystemEmail> PrepareAsync(
            PEMS.Application.Emails.Common.SystemEmailRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PEMS.Application.Common.Interfaces.EmailDeliveryResult> DeliverAsync(
            PEMS.Application.Emails.Common.PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// One campus with content that is unique to it, so a leaked sibling value is unmistakable.
    ///
    /// <para>
    /// The operational contact is the REGISTRANT'S own address, which self-matches at submit: the campus
    /// is confirmed on the spot with no invitation, and the request opens the confirmation gate
    /// immediately. That is deliberate — this suite's subject is the Staff Leader decision, which only
    /// happens after the gate opens, so seeding a contact who still has to confirm would only mean every
    /// test here spent its first act clearing a gate it is not testing.
    /// </para>
    /// </summary>
    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích của {delegationName}", $"Nội dung của {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "AP" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    private sealed record InstanceState(
        ulong VisitInstanceId, ulong CampusId, string Status, ulong? HostUserId,
        string? DecisionNote, ulong? DecidedBy, string? DelegationName, uint FormRevision, int RowVersion);

    private static async Task<Dictionary<ulong, InstanceState>> StateAsync(ulong requestId)
    {
        using var db = NewContext();
        var rows = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .Select(c => new InstanceState(
                c.VisitInstanceId, c.CampusId, c.Status, c.CurrentHostUserId,
                c.DecisionNote, c.DecidedBy,
                c.FormDetail != null ? c.FormDetail.DelegationName : null,
                c.FormDetail != null ? c.FormDetail.FormRevision : 0u,
                c.RowVersion))
            .ToListAsync();
        return rows.ToDictionary(r => r.CampusId);
    }

    private static async Task<string> RequestStatusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.Status).SingleAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Approve: target-only effect ───────────────────────────────────────────────

    [Fact]
    public async Task Approving_one_campus_assigns_its_host_and_leaves_every_sibling_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn Hà Nội"),
                Campus("HCM", start.AddDays(1), "Đoàn Sài Gòn"),
                Campus("DN", start.AddDays(2), "Đoàn Đà Nẵng"));

            var before = await StateAsync(requestId);
            Assert.Equal(3, before.Count);
            Assert.All(before.Values, s => Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, s.Status));

            var target = before[CampusHn];
            var notifications = new RecordingNotifications();
            using (var db = NewContext())
            {
                var res = await ApproveHandler(db, Leader(LeaderHn, CampusHn), notifications).Handle(
                    new ApproveCampusInstanceCommand(requestId, target.VisitInstanceId, IcStaffHn, "Đồng ý tiếp đoàn",
                        target.RowVersion),
                    CancellationToken.None);
                Assert.Equal(VisitInstanceStatus.Assigned, res.CampusStatus);
                Assert.Equal(IcStaffHn, res.HostUserId);
            }

            var after = await StateAsync(requestId);

            // Target instance carries the whole decision.
            Assert.Equal(VisitInstanceStatus.Assigned, after[CampusHn].Status);
            Assert.Equal(IcStaffHn, after[CampusHn].HostUserId);
            Assert.Equal(LeaderHn, after[CampusHn].DecidedBy);
            Assert.Equal("Đồng ý tiếp đoàn", after[CampusHn].DecisionNote);

            // Siblings are byte-for-byte what they were: status, host, decision, row version, form revision.
            foreach (var campusId in new[] { CampusHcm, CampusDn })
            {
                Assert.Equal(before[campusId], after[campusId]);
                Assert.Null(after[campusId].HostUserId);
                Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, after[campusId].Status);
            }

            // Aggregate: one approved, two still pending → PARTIALLY_APPROVED, never APPROVED.
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));

            // Exactly one host participant row, on the target instance only.
            using (var db = NewContext())
            {
                var participants = await db.VisitParticipants.AsNoTracking()
                    .Where(p => after.Values.Select(v => v.VisitInstanceId).Contains(p.VisitInstanceId))
                    .ToListAsync();
                var participant = Assert.Single(participants);
                Assert.Equal(target.VisitInstanceId, participant.VisitInstanceId);
                Assert.Equal(IcStaffHn, participant.UserId);
                Assert.Equal(ParticipantRoles.IcHost, participant.ParticipantRole);
                Assert.True(participant.IsHost);
            }

            // The host notification names the DECIDING campus's delegation — a sibling's name leaking in
            // here is the exact failure Pure V2 exists to prevent.
            var hostNote = Assert.Single(notifications.Sent, n => n.RecipientUserId == IcStaffHn);
            Assert.Contains("Đoàn Hà Nội", hostNote.Message);
            Assert.DoesNotContain("Đoàn Sài Gòn", hostNote.Message);
            Assert.DoesNotContain("Đoàn Đà Nẵng", hostNote.Message);
            Assert.Equal(target.VisitInstanceId, hostNote.VisitInstanceId);
            Assert.Equal(CampusHn, hostNote.CampusId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Approval_audit_is_filed_under_the_deciding_campus_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(21);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn kiểm toán HN"),
                Campus("HCM", start.AddDays(1), "Đoàn kiểm toán HCM"));
            var state = await StateAsync(requestId);

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, state[CampusHn].VisitInstanceId, IcStaffHn, null,
                        state[CampusHn].RowVersion),
                    CancellationToken.None);
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, state[CampusHcm].VisitInstanceId, "Trùng lịch",
                        state[CampusHcm].RowVersion),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                // A campus-scoped audit query is the natural Pure V2 scope; both decisions must answer it.
                // Filtered to the DECISION source, because a campus instance now also carries the
                // contact-confirmation audit the self-matched submit files against it.
                var hn = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == state[CampusHn].VisitInstanceId
                                && a.EntityType == "VisitRequestCampus"
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .ToListAsync());
                Assert.Equal(CampusHn, hn.CampusId);
                Assert.Equal(requestId, hn.VisitRequestId);
                Assert.Equal(CampusDecisionAudit.SourceType, hn.SourceType);
                Assert.Equal(LeaderHn, hn.ActorUserId);
                Assert.Contains("APPROVE_CAMPUS_INSTANCE", hn.Action);

                var hcm = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == state[CampusHcm].VisitInstanceId
                                && a.EntityType == "VisitRequestCampus"
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .ToListAsync());
                Assert.Equal(CampusHcm, hcm.CampusId);
                Assert.Equal("REJECT_CAMPUS_INSTANCE", hcm.Action);
                Assert.Equal(LeaderHcm, hcm.ActorUserId);
                // The staff-authored reason stays on the instance; audit carries a structured summary only.
                Assert.DoesNotContain("Trùng lịch", hcm.Reason ?? string.Empty);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Approve: authorization and host eligibility leave no partial data ─────────

    [Fact]
    public async Task A_leader_of_another_campus_cannot_decide_and_changes_nothing()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(22);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn A"),
                Campus("HCM", start.AddDays(1), "Đoàn B"));
            var before = await StateAsync(requestId);

            // HCM's leader reaching for HN's instance — approve and reject alike.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    ApproveHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                        new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null,
                            before[CampusHn].RowVersion),
                        CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                        new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Không tiếp",
                            before[CampusHn].RowVersion),
                        CancellationToken.None));

            // A plain IC Staff is not a decider at all, even on their own campus.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    ApproveHandler(db, new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null,
                            before[CampusHn].RowVersion),
                            CancellationToken.None));

            Assert.Equal(before, await StateAsync(requestId));
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task An_ineligible_host_is_refused_and_the_instance_stays_pending()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(23);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn host"));
            var before = await StateAsync(requestId);
            var instanceId = before[CampusHn].VisitInstanceId;

            async Task Refuses(ulong hostUserId)
            {
                using var db = NewContext();
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new ApproveCampusInstanceCommand(requestId, instanceId, hostUserId, null,
                            before[CampusHn].RowVersion),
                        CancellationToken.None));
            }

            await Refuses(IcStaffHcm); // IC Staff of a different campus
            await Refuses(LeaderDn);   // another Staff Leader — never a valid host, self-host only

            // No host, no participant row, no status move, no row-version churn.
            Assert.Equal(before, await StateAsync(requestId));
            using (var db = NewContext())
                Assert.Empty(await db.VisitParticipants.AsNoTracking()
                    .Where(p => p.VisitInstanceId == instanceId).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Self_host_is_allowed_and_does_not_notify_the_approver_about_their_own_assignment()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(24), "Đoàn tự nhận"));
            var state = await StateAsync(requestId);
            var notifications = new RecordingNotifications();

            using (var db = NewContext())
            {
                var res = await ApproveHandler(db, Leader(LeaderHn, CampusHn), notifications).Handle(
                    new ApproveCampusInstanceCommand(requestId, state[CampusHn].VisitInstanceId, LeaderHn, null,
                    state[CampusHn].RowVersion),
                    CancellationToken.None);
                Assert.Equal(LeaderHn, res.HostUserId);
            }

            var after = await StateAsync(requestId);
            Assert.Equal(LeaderHn, after[CampusHn].HostUserId);
            Assert.Equal(VisitRequestStatuses.Approved, await RequestStatusAsync(requestId)); // sole campus
            Assert.DoesNotContain(notifications.Sent,
                n => n.RecipientUserId == LeaderHn && n.NotificationType == NotificationTypes.HostAssigned);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Deciding_twice_is_refused_and_never_overwrites_the_first_decision()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(25), "Đoàn một lần"));
            var state = await StateAsync(requestId);
            var instanceId = state[CampusHn].VisitInstanceId;

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, instanceId, IcStaffHn, "Lần đầu",
                        state[CampusHn].RowVersion),
                    CancellationToken.None);
            var afterFirst = await StateAsync(requestId);

            // Re-approving with a different host, and rejecting after approval, are both 409s.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new ApproveCampusInstanceCommand(requestId, instanceId, 102, "Lần hai",
                            afterFirst[CampusHn].RowVersion),
                        CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new RejectCampusInstanceCommand(requestId, instanceId, "Đổi ý",
                            afterFirst[CampusHn].RowVersion), CancellationToken.None));

            Assert.Equal(afterFirst, await StateAsync(requestId));
            using (var db = NewContext())
            {
                Assert.Single(await db.VisitParticipants.AsNoTracking()
                    .Where(p => p.VisitInstanceId == instanceId).ToListAsync());
                // Exactly one DECISION audit: the second approve and the reject both refused, so neither
                // filed one. Scoped to the decision source so the self-matched submit's own
                // contact-confirmation audit on this instance does not count as a decision.
                Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceId && a.EntityType == "VisitRequestCampus"
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .ToListAsync());
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Reject: the registrant's email (repair prompt §7) ─────────────────────────

    /// <summary>
    /// A rejection reaches the registrant by EMAIL, not only as a dashboard notification, and it says
    /// which campus refused and why (TC-REJECT-MAIL-01).
    ///
    /// <para>
    /// One message per rejection: this handler is the only writer of REJECTED and refuses unless the
    /// campus is still waiting, so there is no second path that could send a duplicate — and the
    /// aggregate recompute that follows sends nothing of its own (TC-REJECT-MAIL-03).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Rejecting_a_campus_emails_the_registrant_once_with_the_campus_and_the_reason()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(26);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn HN riêng"));
            var before = await StateAsync(requestId);
            var dispatcher = new RecordingDispatcher();

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications(), dispatcher)
                    .Handle(new RejectCampusInstanceCommand(
                        requestId, before[CampusHn].VisitInstanceId, "Cơ sở đang sửa chữa",
                        before[CampusHn].RowVersion), CancellationToken.None);

            var mail = Assert.Single(dispatcher.Sent);
            Assert.Equal(
                PEMS.Application.Emails.Common.SystemEmailTemplates.VisitCampusRejected, mail.TemplateCode);
            // TO is the REGISTRANT — the one who can edit and resubmit.
            Assert.Equal(V2SeedActor.Email(Registrant), mail.To.Email);
            Assert.Equal("Cơ sở đang sửa chữa", mail.Variables["reason"]);
            Assert.False(string.IsNullOrWhiteSpace(mail.Variables["campusName"]));
            Assert.False(string.IsNullOrWhiteSpace(mail.Variables["requestCode"]));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// With a sibling still pending, the mail is about ONE campus and never claims the request as a
    /// whole was refused (TC-REJECT-MAIL-02). The variables are the assertion: the body is built from
    /// them, so a campus-scoped variable set cannot render a request-wide sentence.
    /// </summary>
    [Fact]
    public async Task The_rejection_email_names_one_campus_while_a_sibling_is_still_pending()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(26);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN riêng"),
                Campus("DN", start.AddDays(2), "Đoàn DN riêng"));
            var before = await StateAsync(requestId);
            var dispatcher = new RecordingDispatcher();

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderDn, CampusDn), new RecordingNotifications(), dispatcher)
                    .Handle(new RejectCampusInstanceCommand(
                        requestId, before[CampusDn].VisitInstanceId, "Trùng lịch",
                        before[CampusDn].RowVersion), CancellationToken.None);

            // The request itself is still PENDING_APPROVAL — HN has not been decided.
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            var mail = Assert.Single(dispatcher.Sent);

            // Filed against THIS rejection's audit row, not against the campus: the same campus can be
            // rejected again after a resubmit, and each of those owes its own message (plan §37).
            Assert.Equal("VisitCampusRejectionEvent", mail.RelatedType);
            using (var db = NewContext())
            {
                var evt = await db.AuditLogs.AsNoTracking()
                    .SingleAsync(a => a.AuditLogId == mail.RelatedId);
                Assert.Equal("REJECT_CAMPUS_INSTANCE", evt.Action);
                Assert.Equal(before[CampusDn].VisitInstanceId, evt.VisitInstanceId);
            }

            // The delegation named is DN's own, never HN's, and never a request-level value.
            Assert.Equal("Đoàn DN riêng", mail.Variables["delegationName"]);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Reject: target-only, aggregate stays honest ───────────────────────────────

    [Fact]
    public async Task Rejecting_one_campus_of_three_leaves_the_request_pending_and_the_siblings_intact()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(26);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN riêng"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM riêng"),
                Campus("DN", start.AddDays(2), "Đoàn DN riêng"));
            var before = await StateAsync(requestId);
            var notifications = new RecordingNotifications();

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderDn, CampusDn), notifications).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusDn].VisitInstanceId, "Cơ sở đang sửa chữa",
                        before[CampusDn].RowVersion),
                    CancellationToken.None);

            var after = await StateAsync(requestId);
            Assert.Equal(VisitInstanceStatus.Rejected, after[CampusDn].Status);
            Assert.Equal("Cơ sở đang sửa chữa", after[CampusDn].DecisionNote);
            Assert.Null(after[CampusDn].HostUserId); // reject never assigns a host
            Assert.Equal(before[CampusHn], after[CampusHn]);
            Assert.Equal(before[CampusHcm], after[CampusHcm]);

            // Two campuses still pending → the request as a whole is NOT rejected.
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            // The visitor is told which campus declined, named from THAT campus's own detail.
            var visitorNote = Assert.Single(notifications.Sent,
                n => n.NotificationType == NotificationTypes.VisitRequestRejected);
            Assert.Equal(before[CampusDn].VisitInstanceId, visitorNote.VisitInstanceId);
            Assert.Equal(CampusDn, visitorNote.CampusId);
            Assert.Contains("Cơ sở đang sửa chữa", visitorNote.Message);

            // Approving a second campus now moves the aggregate to PARTIALLY_APPROVED, not APPROVED.
            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null,
                            before[CampusHn].RowVersion),
                    CancellationToken.None);
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));

            // Last campus rejected → nothing pending, one approved → APPROVED (not REJECTED).
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Không sắp xếp được",
                        before[CampusHcm].RowVersion),
                    CancellationToken.None);
            Assert.Equal(VisitRequestStatuses.Approved, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Every_campus_rejected_rejects_the_request_and_a_missing_reason_changes_nothing()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(27);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn từ chối HN"),
                Campus("HCM", start.AddDays(1), "Đoàn từ chối HCM"));
            var before = await StateAsync(requestId);

            // A blank reason is refused before anything is written.
            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "   ",
                            before[CampusHn].RowVersion),
                        CancellationToken.None));
            Assert.Equal(before, await StateAsync(requestId));

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Lý do HN",
                        before[CampusHn].RowVersion),
                    CancellationToken.None);
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Lý do HCM",
                        before[CampusHcm].RowVersion),
                    CancellationToken.None);

            // Only once EVERY campus has refused does the request itself become REJECTED.
            Assert.Equal(VisitRequestStatuses.Rejected, await RequestStatusAsync(requestId));
            var after = await StateAsync(requestId);
            Assert.Equal("Lý do HN", after[CampusHn].DecisionNote);
            Assert.Equal("Lý do HCM", after[CampusHcm].DecisionNote);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Decisions never touch form content ────────────────────────────────────────

    [Fact]
    public async Task A_decision_writes_no_form_content_on_any_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(28);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn nội dung HN"),
                Campus("HCM", start.AddDays(1), "Đoàn nội dung HCM"));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null,
                            before[CampusHn].RowVersion),
                    CancellationToken.None);
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Từ chối",
                        before[CampusHcm].RowVersion),
                    CancellationToken.None);

            var after = await StateAsync(requestId);
            foreach (var campusId in new[] { CampusHn, CampusHcm })
            {
                // Each campus still answers with its OWN name, and no revision was burned by a decision.
                Assert.Equal(before[campusId].DelegationName, after[campusId].DelegationName);
                Assert.Equal(1u, after[campusId].FormRevision);
            }
            Assert.NotEqual(after[CampusHn].DelegationName, after[CampusHcm].DelegationName);

            using (var db = NewContext())
            {
                // No form-revision history rows: an approval is not a form edit.
                Assert.Empty(await db.VisitInstanceFormRevisionHistories.AsNoTracking()
                    .Where(r => r.VisitRequestId == requestId && r.SourceType != "CREATE").ToListAsync());
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── VISIT_HISTORY_INTEGRITY plan, Fix Group B — immutable campus decision history ────────────
    //
    // These drive the REAL command handlers (reject / approve / resubmit), never hand-built entities,
    // so what is asserted is the actual writer+reader contract rather than a reading of the handler.

    [Fact]
    public async Task B1_Rejecting_a_campus_writes_an_immutable_audit_and_one_history_entry_with_the_reason()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(40);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn B1"));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "A",
                        before[CampusHn].RowVersion),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                    .Where(a => a.VisitInstanceId == before[CampusHn].VisitInstanceId
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .ToListAsync());
                Assert.Equal(requestId, audit.VisitRequestId);
                Assert.Equal(before[CampusHn].VisitInstanceId, audit.VisitInstanceId);
                Assert.Equal(CampusHn, audit.CampusId);
                Assert.Equal(LeaderHn, audit.ActorUserId);
                Assert.Equal(CampusDecisionAudit.Rejected, audit.Action);
                var statusChange = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
                Assert.Equal(VisitInstanceStatus.Rejected, statusChange.NewValueText);
                var noteChange = Assert.Single(audit.Changes.Where(c => c.FieldName == "decision_note"));
                Assert.Equal("A", noteChange.NewValueText);
            }

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected));
            Assert.Equal("A", entry.Reason);
            Assert.Equal(before[CampusHn].VisitInstanceId, entry.VisitInstanceId);
            Assert.False(string.IsNullOrWhiteSpace(entry.ActorName));
            Assert.False(string.IsNullOrWhiteSpace(entry.CampusName));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B2_Rejection_survives_a_resubmit_even_though_the_current_row_is_cleared()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(41);
            const string delegation = "Đoàn B2";
            requestId = await CreateAsync(Campus("HN", start, delegation));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Lý do B2",
                        before[CampusHn].RowVersion),
                    CancellationToken.None);
            var afterReject = await StateAsync(requestId);

            using (var db = NewContext())
                await ResubmitHandler(db, new FakeUser(Registrant), new RecordingNotifications()).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, afterReject[CampusHn].VisitInstanceId,
                        ResubmitContent(afterReject[CampusHn], Campus("HN", start, delegation))),
                    CancellationToken.None);

            var afterResubmit = await StateAsync(requestId);
            // Current row genuinely cleared — this is the exact clearing that used to erase history.
            Assert.Null(afterResubmit[CampusHn].DecidedBy);
            Assert.Null(afterResubmit[CampusHn].DecisionNote);
            Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, afterResubmit[CampusHn].Status);

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var rejected = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected));
            Assert.Equal("Lý do B2", rejected.Reason);
            Assert.False(string.IsNullOrWhiteSpace(rejected.ActorName));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B3_Reject_then_resubmit_then_approve_preserves_all_three_events_in_order()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(42);
            const string delegation = "Đoàn B3";
            requestId = await CreateAsync(Campus("HN", start, delegation));
            var before = await StateAsync(requestId);
            var clock = new SteppingClock(Now);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications(), clock: clock).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Từ chối lần 1",
                        before[CampusHn].RowVersion),
                    CancellationToken.None);
            var afterReject = await StateAsync(requestId);

            using (var db = NewContext())
                await ResubmitHandler(db, new FakeUser(Registrant), new RecordingNotifications(), clock).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, afterReject[CampusHn].VisitInstanceId,
                        ResubmitContent(afterReject[CampusHn], Campus("HN", start, delegation))),
                    CancellationToken.None);
            var afterResubmit = await StateAsync(requestId);

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications(), clock).Handle(
                    new ApproveCampusInstanceCommand(requestId, afterResubmit[CampusHn].VisitInstanceId, IcStaffHn,
                        "Duyệt lần 1", afterResubmit[CampusHn].RowVersion),
                    CancellationToken.None);

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);

            var rejected = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected));
            Assert.Equal("Từ chối lần 1", rejected.Reason);
            var approved = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved));
            Assert.Equal("Duyệt lần 1", approved.Reason);
            var resubmitted = Assert.Single(
                result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceContentResubmitted));

            // Chronological: reject happened first, then resubmit, then approve.
            Assert.True(rejected.At <= resubmitted.At);
            Assert.True(resubmitted.At <= approved.At);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B4_A_second_rejection_after_resubmit_creates_a_separate_event_from_the_first()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(43);
            const string delegation = "Đoàn B4";
            requestId = await CreateAsync(Campus("HN", start, delegation));
            var before = await StateAsync(requestId);
            var clock = new SteppingClock(Now);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications(), clock: clock).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Từ chối lần 1",
                        before[CampusHn].RowVersion),
                    CancellationToken.None);
            var afterReject1 = await StateAsync(requestId);

            using (var db = NewContext())
                await ResubmitHandler(db, new FakeUser(Registrant), new RecordingNotifications(), clock).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, afterReject1[CampusHn].VisitInstanceId,
                        ResubmitContent(afterReject1[CampusHn], Campus("HN", start, delegation))),
                    CancellationToken.None);
            var afterResubmit = await StateAsync(requestId);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications(), clock: clock).Handle(
                    new RejectCampusInstanceCommand(requestId, afterResubmit[CampusHn].VisitInstanceId, "Từ chối lần 2",
                        afterResubmit[CampusHn].RowVersion),
                    CancellationToken.None);

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);

            var rejections = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected).ToList();
            Assert.Equal(2, rejections.Count);
            Assert.Contains(rejections, e => e.Reason == "Từ chối lần 1");
            Assert.Contains(rejections, e => e.Reason == "Từ chối lần 2");
            // Two distinct immutable audit rows, not one row read twice.
            Assert.Equal(2, rejections.Select(e => e.EventId).Distinct().Count());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B5_Approval_history_survives_lifecycle_progression_past_assigned()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(44);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn B5"));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn,
                        "Duyệt B5", before[CampusHn].RowVersion),
                    CancellationToken.None);
            var afterApprove = await StateAsync(requestId);

            // Advance the campus past ASSIGNED directly — this test's subject is the history reader,
            // not the lifecycle command; the same raw-SQL progression is already an established pattern
            // elsewhere in this test suite (e.g. OperationalContactLifecycleLockTests). BEFORE_VISIT is
            // enough to prove the point; DURING_VISIT/AFTER_VISIT/CLOSED require an agenda item, which
            // is unrelated to what this test is checking.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_request_campuses SET status = 'BEFORE_VISIT' WHERE visit_instance_id = {0}",
                    afterApprove[CampusHn].VisitInstanceId);

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var approved = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved));
            Assert.Equal("Duyệt B5", approved.Reason);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceDecided);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B6_Approval_with_a_schedule_warning_still_produces_exactly_one_approved_event()
    {
        RequireDb();
        ulong requestId1 = 0, requestId2 = 0;
        try
        {
            var start = Now.AddDays(45);
            requestId1 = await CreateAsync(Campus("HN", start, "Đoàn B6 đầu tiên"));
            var state1 = await StateAsync(requestId1);
            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId1, state1[CampusHn].VisitInstanceId, IcStaffHn,
                        null, state1[CampusHn].RowVersion),
                    CancellationToken.None);

            // Same campus, same host, overlapping window on a SECOND request — this is what
            // hasHostingConflict actually detects (double-booking the same person on the same campus
            // across different requests; host eligibility is per-campus, so it can never trigger
            // across two DIFFERENT campuses).
            requestId2 = await CreateAsync(Campus("HN", start, "Đoàn B6 trùng lịch"));
            var state2 = await StateAsync(requestId2);
            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId2, state2[CampusHn].VisitInstanceId, IcStaffHn,
                        "Duyệt trùng lịch", state2[CampusHn].RowVersion),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var audit = await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == state2[CampusHn].VisitInstanceId
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .SingleAsync();
                Assert.Equal(CampusDecisionAudit.ApprovedWithScheduleWarning, audit.Action);
            }

            using var db2 = NewContext();
            var result = await HistoryHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                new GetVisitRequestHistoryQuery(requestId2), CancellationToken.None);
            var approved = Assert.Single(result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved));
            Assert.Equal("Duyệt trùng lịch", approved.Reason);
        }
        finally
        {
            await CleanupAsync(requestId1);
            await CleanupAsync(requestId2);
        }
    }

    [Fact]
    public async Task B9_Multi_campus_scope_keeps_each_leaders_history_to_their_own_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(46);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn B9 HN"),
                Campus("HCM", start.AddDays(1), "Đoàn B9 HCM"));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn,
                        "Duyệt HN", before[CampusHn].RowVersion),
                    CancellationToken.None);
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Từ chối HCM",
                        before[CampusHcm].RowVersion),
                    CancellationToken.None);

            using (var dbA = NewContext())
            {
                var resultA = await HistoryHandler(dbA, Leader(LeaderHn, CampusHn)).Handle(
                    new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
                Assert.Contains(resultA.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved);
                Assert.DoesNotContain(resultA.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceRejected);
                Assert.DoesNotContain(resultA.Entries, e => e.VisitInstanceId == before[CampusHcm].VisitInstanceId);
            }
            using (var dbB = NewContext())
            {
                var resultB = await HistoryHandler(dbB, Leader(LeaderHcm, CampusHcm)).Handle(
                    new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
                Assert.Contains(resultB.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceRejected);
                Assert.DoesNotContain(resultB.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved);
                Assert.DoesNotContain(resultB.Entries, e => e.VisitInstanceId == before[CampusHn].VisitInstanceId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task B10_Guessing_another_campuss_decision_event_id_reports_not_found()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(47);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn B10 HN"),
                Campus("HCM", start.AddDays(1), "Đoàn B10 HCM"));
            var before = await StateAsync(requestId);

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Từ chối HCM",
                        before[CampusHcm].RowVersion),
                    CancellationToken.None);

            string eventId;
            using (var db = NewContext())
            {
                var audit = await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == before[CampusHcm].VisitInstanceId
                                && a.SourceType == CampusDecisionAudit.SourceType)
                    .SingleAsync();
                eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit.AuditLogId);
            }

            using var db2 = NewContext();
            await Assert.ThrowsAsync<NotFoundException>(() =>
                DetailHandler(db2, Leader(LeaderHn, CampusHn)).Handle(
                    new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }
}
