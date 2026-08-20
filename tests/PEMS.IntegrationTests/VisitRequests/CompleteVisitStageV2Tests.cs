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
using PEMS.Application.Delegations.Commands.CompleteVisitStage;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.SaveVisitAgenda;
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Commit 4 — Lifecycle History Integrity (Fix Group F). CompleteVisitStageCommandHandler's own
/// three transitions (BEFORE_VISIT→DURING_VISIT, DURING_VISIT→AFTER_VISIT, AFTER_VISIT→CLOSED) had
/// zero test coverage before this commit — this file exercises the REAL handler chain end to end
/// (create → approve → start preparation → save agenda → each stage) so the new lifecycle audit and
/// its business-history entries are proven against actual writer output, not an assumption of what
/// the writer produces. Privacy/multi-campus/legacy-fallback regression lives in
/// VisitRequestHistoryV2Tests.cs (C4-6..C4-14), matching that file's own hand-built-fixture
/// convention for scope testing.
/// </summary>
public sealed class CompleteVisitStageV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong HoUser = 2;
    private const ulong HostHn = 101;
    private const ulong CampusHn = 1;

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
        public FakeUser(ulong id, string roleCode, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId; }
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

    /// <summary>A clock fixed to one explicit instant per call — used to give each step of a
    /// progression its own deterministic, strictly-increasing timestamp (chronological ordering,
    /// Case C4-4) without a shared mutable counter.</summary>
    private sealed class FixedClock : IDateTimeService
    {
        private readonly DateTime _at;
        public FixedClock(DateTime at) => _at = at;
        public DateTime UtcNow => _at;
        public DateTime VietnamNow => _at;
    }

    private sealed class SilentNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong u, string t, string? m, string n, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static FakeUser Owner() => new(Registrant, RoleCodes.Visitor);
    private static FakeUser Ho() => new(HoUser, RoleCodes.Ho);
    private static FakeUser Host() => new(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn);
    private static FakeUser StaffLeader() => new(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn);

    private static GetVisitRequestHistoryQueryHandler HistoryHandler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, ReadOn);
    private static GetVisitHistoryDetailQueryHandler HistoryDetailHandler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, ReadOn);

    private static CampusVisitFormDto Campus(DateTime start)
        => new("HN", start, start.AddMinutes(120), "Đoàn giai đoạn", "MEETING", null,
            "Mục đích", "Nội dung",
            new List<VisitorDto> { new("Khách A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // Self-matches the registrant, so the campus starts past the confirmation gate — this
            // suite is about lifecycle stages, not contact confirmation.
            new ContactPointDto("Đầu mối", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<(ulong RequestId, ulong InstanceId)> CreateAsync(DateTime start)
    {
        using var db = NewContext();
        var actor = Owner();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(Now), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "STG" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, new List<CampusVisitFormDto> { Campus(start) });
        var requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        var instanceId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
        return (requestId, instanceId);
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, DateTime at)
    {
        using var db = NewContext();
        var actor = StaffLeader();
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(at),
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db),
                    new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock(at)),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, HostHn, null, rowVersion), CancellationToken.None);
    }

    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId, DateTime at)
    {
        using var db = NewContext();
        await new StartVisitPreparationCommandHandler(db, Host(), new FixedClock(at))
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task SeedAgendaAsync(ulong requestId, ulong instanceId, DateTime windowStart, DateTime at)
    {
        using var db = NewContext();
        await new SaveVisitAgendaCommandHandler(db, Host(), new FixedClock(at), new SilentNotifications())
            .Handle(new SaveVisitAgendaCommand(requestId, instanceId,
                new List<SaveVisitAgendaItem> { new(null, "Đón khách", windowStart, windowStart.AddHours(1), null, "Phòng họp", null) },
                windowStart, windowStart.AddMinutes(120)),
                CancellationToken.None);
    }

    private static async Task<CompleteVisitStageResponse> CompleteStageAsync(
        ulong requestId, ulong instanceId, string stage, DateTime at, bool confirmNoNews = true)
    {
        using var db = NewContext();
        return await new CompleteVisitStageCommandHandler(db, Host(), new FixedClock(at), new SilentNotifications())
            .Handle(new CompleteVisitStageCommand(requestId, instanceId, stage, confirmNoNews), CancellationToken.None);
    }

    private static async Task<uint> FormRevisionAsync(ulong instanceId)
    {
        using var db = NewContext();
        return await db.VisitInstanceFormDetails.AsNoTracking()
            .Where(d => d.VisitInstanceId == instanceId).Select(d => d.FormRevision).SingleAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    /// <summary>
    /// Case A-C4-1 (VISIT_HISTORY_INTEGRITY final phase, Phase A). ASSIGNED → BEFORE_VISIT —
    /// StartVisitPreparationCommandHandler's own transition, the one gap Commit 4 left open — now has
    /// the same fully-scoped immutable audit as the three CompleteVisitStageCommandHandler transitions.
    /// </summary>
    [Fact]
    public async Task A_C4_1_Assigned_to_before_visit_has_immutable_scoped_lifecycle_audit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));

            var transitionAt = Now.AddMinutes(2);
            await StartPreparationAsync(requestId, instanceId, transitionAt);

            using var db = NewContext();
            var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitRequestId == requestId && a.Action == VisitLifecycleHistoryAudit.PreparationStarted)
                .ToListAsync());
            Assert.Equal(requestId, audit.VisitRequestId);
            Assert.Equal(instanceId, audit.VisitInstanceId);
            Assert.Equal(CampusHn, audit.CampusId);
            Assert.Equal(HostHn, audit.ActorUserId);
            Assert.Equal(VisitLifecycleHistoryAudit.SourceType, audit.SourceType);
            var change = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.Assigned, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, change.NewValueText);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case A-C4-2. The timeline shows exactly one VISIT_PREPARATION_STARTED.</summary>
    [Fact]
    public async Task A_C4_2_Timeline_shows_exactly_one_preparation_started_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted);
            Assert.Equal(instanceId, entry.VisitInstanceId);
            Assert.False(string.IsNullOrWhiteSpace(entry.ActorName));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case A-C4-3. Preparation detail shows ASSIGNED → BEFORE_VISIT exactly.</summary>
    [Fact]
    public async Task A_C4_3_Preparation_detail_shows_assigned_to_before_visit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted);

            var drawer = await HistoryDetailHandler(db, Owner()).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.VisitPreparationStarted, drawer.EventCode);
            var field = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "status");
            Assert.Equal(VisitInstanceStatuses.Assigned, field.BeforeValue);
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, field.AfterValue);
            Assert.Equal((long)CampusHn, drawer.CampusId);
            Assert.False(string.IsNullOrWhiteSpace(drawer.ActorName));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Case A-C4-4. The FULL chain including preparation — ASSIGNED → BEFORE_VISIT → DURING_VISIT →
    /// AFTER_VISIT → CLOSED — is exactly four lifecycle events, correctly chronologically ordered, no
    /// duplicates. Distinct from the pre-existing C4_4 (which only ever asserted its own three codes
    /// and stays valid unchanged: VisitPreparationStarted is a fourth, disjoint code it never filtered
    /// for).
    /// </summary>
    [Fact]
    public async Task A_C4_4_Full_lifecycle_including_preparation_is_four_chronological_events_no_duplicates()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var lifecycle = result.Entries.Where(e =>
                e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted
                || e.EventCode == VisitHistoryEventCodes.VisitStarted
                || e.EventCode == VisitHistoryEventCodes.VisitCompleted
                || e.EventCode == VisitHistoryEventCodes.InstanceClosed).ToList();

            Assert.Equal(4, lifecycle.Count);
            // result.Entries is OrderByDescending(At) — the LAST transition (close) sorts first.
            Assert.Equal(VisitHistoryEventCodes.InstanceClosed, lifecycle[0].EventCode);
            Assert.Equal(VisitHistoryEventCodes.VisitCompleted, lifecycle[1].EventCode);
            Assert.Equal(VisitHistoryEventCodes.VisitStarted, lifecycle[2].EventCode);
            Assert.Equal(VisitHistoryEventCodes.VisitPreparationStarted, lifecycle[3].EventCode);
            Assert.True(lifecycle[0].At > lifecycle[1].At);
            Assert.True(lifecycle[1].At > lifecycle[2].At);
            Assert.True(lifecycle[2].At > lifecycle[3].At);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case A-C4-5. FormRevision is exactly unchanged across all FOUR transitions, preparation included.</summary>
    [Fact]
    public async Task A_C4_5_FormRevision_unchanged_across_all_four_transitions()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            var revBefore = await FormRevisionAsync(instanceId);

            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));

            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // Phase B (VISIT_HISTORY_INTEGRITY final phase, ProposedHostActivation) has its own real-handler
    // coverage in OperationalContactConfirmationWorkflowTests.cs (B2_*) — that suite already owns the
    // full create-with-proposal → invite → accept-confirmation harness (an internal creator proposing
    // themself cannot ALSO self-match as the campus's own contact, which is what this file's fixtures
    // do, so its harness cannot exercise the activation path; reusing the existing one there is both
    // correct and avoids a second, parallel harness).

    /// <summary>Case C4-1. BEFORE_VISIT → DURING_VISIT has fully-scoped immutable history.</summary>
    [Fact]
    public async Task C4_1_Before_to_during_has_immutable_scoped_history()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            var revBefore = await FormRevisionAsync(instanceId);

            var transitionAt = Now.AddMinutes(4);
            var response = await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, transitionAt);
            Assert.Equal(VisitInstanceStatuses.DuringVisit, response.InstanceStatus);

            using var db = NewContext();
            var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitRequestId == requestId && a.Action == VisitLifecycleHistoryAudit.CompleteBeforeVisit)
                .ToListAsync());
            Assert.Equal(requestId, audit.VisitRequestId);
            Assert.Equal(instanceId, audit.VisitInstanceId);
            Assert.Equal(CampusHn, audit.CampusId);
            Assert.Equal(HostHn, audit.ActorUserId);
            Assert.Equal(VisitLifecycleHistoryAudit.SourceType, audit.SourceType);
            var change = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.DuringVisit, change.NewValueText);

            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);
            Assert.Equal(instanceId, entry.VisitInstanceId);
            Assert.False(string.IsNullOrWhiteSpace(entry.ActorName));

            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-2. DURING_VISIT → AFTER_VISIT — same structure.</summary>
    [Fact]
    public async Task C4_2_During_to_after_has_immutable_scoped_history()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            var revBefore = await FormRevisionAsync(instanceId);

            var transitionAt = Now.AddMinutes(5);
            var response = await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, transitionAt);
            Assert.Equal(VisitInstanceStatuses.AfterVisit, response.InstanceStatus);

            using var db = NewContext();
            var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitRequestId == requestId && a.Action == VisitLifecycleHistoryAudit.CompleteDuringVisit)
                .ToListAsync());
            Assert.Equal(instanceId, audit.VisitInstanceId);
            Assert.Equal(CampusHn, audit.CampusId);
            var change = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.DuringVisit, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.AfterVisit, change.NewValueText);

            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitCompleted);

            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Case C4-3. AFTER_VISIT → CLOSED. Confirms VisitHistoryEventCodes.InstanceClosed — a constant
    /// that existed before this commit but nothing ever constructed — is now actually used.
    /// </summary>
    [Fact]
    public async Task C4_3_After_to_closed_has_immutable_scoped_history_and_uses_instanceclosed()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            var revBefore = await FormRevisionAsync(instanceId);

            // Must be at/after PlannedEndAt (start + 120min) for the close blocker to clear.
            var transitionAt = start.AddMinutes(200);
            var response = await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, transitionAt, confirmNoNews: true);
            Assert.Equal(VisitInstanceStatuses.Closed, response.InstanceStatus);

            using var db = NewContext();
            var audit = Assert.Single(await db.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitRequestId == requestId && a.Action == VisitLifecycleHistoryAudit.CloseVisitInstance)
                .ToListAsync());
            Assert.Equal(instanceId, audit.VisitInstanceId);
            Assert.Equal(CampusHn, audit.CampusId);
            var change = Assert.Single(audit.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.AfterVisit, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.Closed, change.NewValueText);

            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceClosed);

            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-4. The full chain — exactly three lifecycle events, correct chronological order, no duplicates.</summary>
    [Fact]
    public async Task C4_4_Full_lifecycle_produces_exactly_three_chronological_events_no_duplicates()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var lifecycle = result.Entries.Where(e =>
                e.EventCode == VisitHistoryEventCodes.VisitStarted
                || e.EventCode == VisitHistoryEventCodes.VisitCompleted
                || e.EventCode == VisitHistoryEventCodes.InstanceClosed).ToList();

            Assert.Equal(3, lifecycle.Count);
            // result.Entries is OrderByDescending(At) — the LAST transition (close) sorts first.
            Assert.Equal(VisitHistoryEventCodes.InstanceClosed, lifecycle[0].EventCode);
            Assert.Equal(VisitHistoryEventCodes.VisitCompleted, lifecycle[1].EventCode);
            Assert.Equal(VisitHistoryEventCodes.VisitStarted, lifecycle[2].EventCode);
            Assert.True(lifecycle[0].At > lifecycle[1].At);
            Assert.True(lifecycle[1].At > lifecycle[2].At);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-5. Commit 2's approval history is retained unchanged as lifecycle progresses.</summary>
    [Fact]
    public async Task C4_5_Approval_history_is_retained_alongside_lifecycle_progression()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var approved = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved
                && e.VisitInstanceId == instanceId);
            Assert.False(string.IsNullOrWhiteSpace(approved.ActorName));
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitCompleted);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceClosed);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-11. An invalid/repeated transition writes no lifecycle audit and no history event.</summary>
    [Fact]
    public async Task C4_11_Invalid_transition_creates_no_lifecycle_audit_and_no_history_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            // Campus is ASSIGNED, not yet BEFORE_VISIT — completing "DURING" is invalid from here.
            await Assert.ThrowsAsync<ConflictException>(async () =>
                await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(2)));

            using var db = NewContext();
            Assert.False(await db.AuditLogs.AnyAsync(a => a.VisitRequestId == requestId
                && VisitLifecycleHistoryAudit.LifecycleActions.Contains(a.Action)));

            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.DoesNotContain(result.Entries, e =>
                e.EventCode == VisitHistoryEventCodes.VisitStarted
                || e.EventCode == VisitHistoryEventCodes.VisitCompleted
                || e.EventCode == VisitHistoryEventCodes.InstanceClosed);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-12. FormRevision is exactly unchanged after the full lifecycle.</summary>
    [Fact]
    public async Task C4_12_FormRevision_unchanged_across_full_lifecycle()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            var revBefore = await FormRevisionAsync(instanceId);

            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            Assert.Equal(revBefore, await FormRevisionAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-D1. Start-stage detail shows BEFORE_VISIT → DURING_VISIT exactly.</summary>
    [Fact]
    public async Task C4_D1_Start_detail_shows_status_before_after()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);

            var drawer = await HistoryDetailHandler(db, Owner()).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.VisitStarted, drawer.EventCode);
            var field = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "status");
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, field.BeforeValue);
            Assert.Equal(VisitInstanceStatuses.DuringVisit, field.AfterValue);
            Assert.Equal((long)CampusHn, drawer.CampusId);
            Assert.False(string.IsNullOrWhiteSpace(drawer.ActorName));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-D2. Complete-stage detail shows DURING_VISIT → AFTER_VISIT exactly.</summary>
    [Fact]
    public async Task C4_D2_Complete_detail_shows_status_before_after()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitCompleted);

            var drawer = await HistoryDetailHandler(db, Owner()).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            var field = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "status");
            Assert.Equal(VisitInstanceStatuses.DuringVisit, field.BeforeValue);
            Assert.Equal(VisitInstanceStatuses.AfterVisit, field.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>Case C4-D3. Close detail shows AFTER_VISIT → CLOSED exactly.</summary>
    [Fact]
    public async Task C4_D3_Close_detail_shows_status_before_after()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.Before, Now.AddMinutes(4));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.During, Now.AddMinutes(5));
            await CompleteStageAsync(requestId, instanceId, VisitStageKeys.After, start.AddMinutes(200), confirmNoNews: true);

            using var db = NewContext();
            var result = await HistoryHandler(db, Owner()).Handle(
                new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            var entry = Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceClosed);

            var drawer = await HistoryDetailHandler(db, Owner()).Handle(
                new GetVisitHistoryDetailQuery(requestId, entry.EventId!), CancellationToken.None);
            var field = Assert.Single(drawer.FieldChanges, f => f.FieldCode == "status");
            Assert.Equal(VisitInstanceStatuses.AfterVisit, field.BeforeValue);
            Assert.Equal(VisitInstanceStatuses.Closed, field.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }
}
