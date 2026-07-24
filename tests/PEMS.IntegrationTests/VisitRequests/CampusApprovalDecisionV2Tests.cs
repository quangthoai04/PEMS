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
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications)
        => new(db, actor, new FixedClock(), new VisitRequestAggregateStatusService(db), notifications,
            new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()));

    private static RejectCampusInstanceCommandHandler RejectHandler(
        ApplicationDbContext db, FakeUser actor, RecordingNotifications notifications)
        => new(db, actor, new FixedClock(), new VisitRequestAggregateStatusService(db), notifications,
            new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()));

    /// <summary>One campus with content that is unique to it, so a leaked sibling value is unmistakable.</summary>
    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích của {delegationName}", $"Nội dung của {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "+8410", "op@example.com"),
            "VI", null, "DECLINED", null, null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db));
        var form = new VisitRequestFormDataV2(
            "AP" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
            new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"), // A==B → contact ACTIVE
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
                    new ApproveCampusInstanceCommand(requestId, target.VisitInstanceId, IcStaffHn, "Đồng ý tiếp đoàn"),
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
                    new ApproveCampusInstanceCommand(requestId, state[CampusHn].VisitInstanceId, IcStaffHn, null),
                    CancellationToken.None);
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, state[CampusHcm].VisitInstanceId, "Trùng lịch"),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                // A campus-scoped audit query is the natural Pure V2 scope; both decisions must answer it.
                var hn = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == state[CampusHn].VisitInstanceId
                                && a.EntityType == "VisitRequestCampus")
                    .ToListAsync());
                Assert.Equal(CampusHn, hn.CampusId);
                Assert.Equal(requestId, hn.VisitRequestId);
                Assert.Equal(CampusDecisionAudit.SourceType, hn.SourceType);
                Assert.Equal(LeaderHn, hn.ActorUserId);
                Assert.Contains("APPROVE_CAMPUS_INSTANCE", hn.Action);

                var hcm = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == state[CampusHcm].VisitInstanceId
                                && a.EntityType == "VisitRequestCampus")
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
                        new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null),
                        CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                        new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Không tiếp"),
                        CancellationToken.None));

            // A plain IC Staff is not a decider at all, even on their own campus.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    ApproveHandler(db, new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                            new RecordingNotifications())
                        .Handle(new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null),
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
                        new ApproveCampusInstanceCommand(requestId, instanceId, hostUserId, null),
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
                    new ApproveCampusInstanceCommand(requestId, state[CampusHn].VisitInstanceId, LeaderHn, null),
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
                    new ApproveCampusInstanceCommand(requestId, instanceId, IcStaffHn, "Lần đầu"),
                    CancellationToken.None);
            var afterFirst = await StateAsync(requestId);

            // Re-approving with a different host, and rejecting after approval, are both 409s.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    ApproveHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new ApproveCampusInstanceCommand(requestId, instanceId, 102, "Lần hai"),
                        CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                        new RejectCampusInstanceCommand(requestId, instanceId, "Đổi ý"), CancellationToken.None));

            Assert.Equal(afterFirst, await StateAsync(requestId));
            using (var db = NewContext())
            {
                Assert.Single(await db.VisitParticipants.AsNoTracking()
                    .Where(p => p.VisitInstanceId == instanceId).ToListAsync());
                Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceId && a.EntityType == "VisitRequestCampus")
                    .ToListAsync());
            }
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
                    new RejectCampusInstanceCommand(requestId, before[CampusDn].VisitInstanceId, "Cơ sở đang sửa chữa"),
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
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null),
                    CancellationToken.None);
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));

            // Last campus rejected → nothing pending, one approved → APPROVED (not REJECTED).
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Không sắp xếp được"),
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
                        new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "   "),
                        CancellationToken.None));
            Assert.Equal(before, await StateAsync(requestId));

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHn, CampusHn), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, "Lý do HN"),
                    CancellationToken.None);
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Lý do HCM"),
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
                    new ApproveCampusInstanceCommand(requestId, before[CampusHn].VisitInstanceId, IcStaffHn, null),
                    CancellationToken.None);
            using (var db = NewContext())
                await RejectHandler(db, Leader(LeaderHcm, CampusHcm), new RecordingNotifications()).Handle(
                    new RejectCampusInstanceCommand(requestId, before[CampusHcm].VisitInstanceId, "Từ chối"),
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
}
