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
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The contact confirmation gate as an APPROVAL-ACTION gate rather than a visibility gate.
///
/// <para>
/// It used to be both. <c>PENDING_CONTACT_CONFIRMATION</c> subtracted a Staff Leader's own campus rows
/// from the review queue AND refused the detail endpoint, so a leader whose campus had already
/// confirmed could not even learn that a visit involving their campus existed while a SIBLING campus
/// was still waiting on its contact. Two different questions were being answered with one status:
/// </para>
/// <list type="bullet">
///   <item><b>Visibility</b> — "may I know this request exists?" Answered by campus responsibility, and
///     by nothing else.</item>
///   <item><b>Authorization</b> — "may I decide it?" Answered by the gate.</item>
/// </list>
///
/// <para>
/// Every test below asserts one half against the other on the SAME row: seen and not decidable, then
/// seen and decidable once the last contact confirms. The aggregate itself is unchanged and is asserted
/// as such — a confirmed campus does not open the gate for its unconfirmed sibling, and nothing here
/// rewrites <c>visit_requests.status</c> to make a row visible.
/// </para>
///
/// <para>
/// The backend guard is tested by CALLING the commands, not by reading them: a list that merely omits a
/// button is a UI courtesy, and the invariant has to survive somebody posting the request by hand.
/// </para>
/// </summary>
public sealed class ContactGateVisibilityTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    // Canonical seed actors. Campus 1 = HN, 2 = HCM, 3 = DN.
    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;      // STAFF/LEADER, campus 1
    private const ulong LeaderHcm = 9;     // STAFF/LEADER, campus 2
    private const ulong LeaderDn = 11;     // STAFF/LEADER, campus 3
    private const ulong IcStaffHn = 101;   // STAFF/STAFF, IC, campus 1
    private const ulong HoUser = 2;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;
    private const ulong CampusDn = 3;

    private const string Approve = "APPROVE_AND_ASSIGN_HOST";
    private const string Reject = "CAMPUS_REJECT";

    private static bool? _dbUp;
    private static string? _dbFailure;
    private static readonly DateTime Now = DateTime.Now;
    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not usable: " + (_dbFailure ?? "CanConnect() returned false."));
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
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

    private static FakeUser Leader(ulong id, ulong campusId)
        => new(id, RoleCodes.Staff, UserSubRoles.Leader, campusId);

    private static FakeUser Ho() => new(HoUser, RoleCodes.Ho);

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

    /// <summary>Always grants — these tests send one message at a time, if any.</summary>
    private sealed class GrantingLock : PEMS.Application.Delegations.VisitNotifications.IEmailRecoveryLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(string key, CancellationToken ct)
            => Task.FromResult<IAsyncDisposable?>(new Handle());
        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A campus whose operational contact is the REGISTRANT'S own address, so it self-matches at submit
    /// and lands confirmed with no invitation. Tests that need a campus BEHIND the gate call
    /// <see cref="ShutGateAsync"/> on it afterwards, which is the same shape the real flow produces
    /// while an invited external contact has not answered yet.
    /// </summary>
    private static CampusVisitFormDto Campus(string code)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null,
            "Thăm " + code, "Nội dung " + code,
            new List<VisitorDto> { new("Khách " + code, "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OpOrg", "Trưởng phòng Hợp tác", "+8410",
                V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)),
            new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "CG" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    /// <summary>
    /// Takes ONE campus back to "nobody has confirmed it", which re-shuts the gate for the WHOLE
    /// request. Written through SQL rather than the aggregate service on purpose: the DB triggers
    /// (<c>trg_visit_campuses_aggregate_au</c>) recompute <c>visit_requests.status</c> themselves, so
    /// this proves the aggregate that the rest of the test reasons about is the real one.
    /// </summary>
    private static async Task ShutGateAsync(ulong instanceId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'WAITING_CONTACT_CONFIRMATION', " +
            "operational_contact_user_id = NULL, operational_contact_confirmed_at = NULL, " +
            "operational_contact_confirmation_source = NULL WHERE visit_instance_id = {0}",
            instanceId);
    }

    /// <summary>The contact answers: the campus is confirmed and the gate opens once the LAST one has.</summary>
    private static async Task ConfirmContactAsync(ulong instanceId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1}, " +
            "operational_contact_confirmed_at = {2}, operational_contact_confirmation_source = 'REGISTRANT_SELF_MATCH', " +
            "status = 'WAITING_REQUEST_APPROVAL' WHERE visit_instance_id = {0}",
            instanceId, Registrant, Now);
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, string Status, int RowVersion);

    private static async Task<Dictionary<ulong, CampusRow>> StateAsync(ulong requestId)
    {
        using var db = NewContext();
        var rows = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .Select(c => new CampusRow(c.VisitInstanceId, c.CampusId, c.Status, c.RowVersion))
            .ToListAsync();
        return rows.ToDictionary(r => r.CampusId);
    }

    private static async Task<string> RequestStatusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.Status).SingleAsync();
    }

    private static async Task<string> RequestCodeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
    }

    private static async Task<List<VisitRequestManagementItemDto>> ListAsync(
        ICurrentUserService caller, string keyword, string tab = "responsible")
    {
        using var db = NewContext();
        var handler = new ViewGuestDelegationListQueryHandler(db, caller, new FixedClock());
        var page = await handler.Handle(
            new ViewGuestDelegationListQuery { Tab = tab, Page = 1, PageSize = 200, Keyword = keyword },
            CancellationToken.None);
        return page.Items.ToList();
    }

    private static async Task<ResolvedVisitFormDto> DetailAsync(ICurrentUserService caller, ulong requestId)
    {
        using var db = NewContext();
        return await new VisitFormReadService(db, caller, NullLogger<VisitFormReadService>.Instance, new FixedClock())
            .ResolveAsync(requestId, CancellationToken.None);
    }

    private static async Task ApproveAsync(FakeUser leader, ulong requestId, ulong instanceId, ulong hostUserId, int rowVersion)
    {
        using var db = NewContext();
        var handler = new ApproveCampusInstanceCommandHandler(
            db, leader, new FixedClock(),
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db),
                new NoopNotifications(),
                new VisitFormReadService(db, leader, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                NullLogger<CampusApprovalExecutor>.Instance));
        await handler.Handle(
            new ApproveCampusInstanceCommand(requestId, instanceId, hostUserId, "Duyệt", rowVersion),
            CancellationToken.None);
    }

    private static async Task RejectAsync(FakeUser leader, ulong requestId, ulong instanceId, int rowVersion)
    {
        using var db = NewContext();
        var handler = new RejectCampusInstanceCommandHandler(
            db, leader, new FixedClock(), new VisitRequestAggregateStatusService(db), new NoopNotifications(),
            new VisitFormReadService(db, leader, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
            new PEMS.Application.Delegations.VisitNotifications.CampusRejectionEmail(db),
            new PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender(
                db, new CampusApprovalDecisionV2Tests.RecordingDispatcher(), new GrantingLock(), new FixedClock(),
                NullLogger<PEMS.Application.Delegations.VisitNotifications.RecoverableVisitEmailSender>.Instance));
        await handler.Handle(
            new RejectCampusInstanceCommand(requestId, instanceId, "Không tiếp nhận", rowVersion),
            CancellationToken.None);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
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
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Case A — one campus, contact unconfirmed ──────────────────────────────────

    /// <summary>
    /// The single-campus case, and the whole change in one assertion pair: the row IS in the leader's
    /// queue while its contact has not answered, and it carries VIEW_DETAIL and neither decision.
    ///
    /// <para>
    /// Before, the first assertion was <c>Assert.Empty</c> — the leader was told nothing at all was
    /// waiting for their campus, which is precisely the transparency this change exists to give back.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Behind_the_gate_the_campus_leader_sees_the_row_and_gets_neither_decision()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var code = await RequestCodeAsync(requestId);
            await ShutGateAsync((await StateAsync(requestId))[CampusHn].InstanceId);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, await RequestStatusAsync(requestId));

            var row = Assert.Single(await ListAsync(Leader(LeaderHn, CampusHn), code));

            Assert.Contains("VIEW_DETAIL", row.AllowedActions);
            Assert.DoesNotContain(Approve, row.AllowedActions);
            Assert.DoesNotContain(Reject, row.AllowedActions);
            // The relation is held — visibility is campus responsibility, and it did not go anywhere.
            Assert.Contains(VisitRowRelations.CampusReviewer, row.RelationContexts.Select(c => c.Relation));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The row must not ADVERTISE a decision it will refuse. A behind-gate campus can already read
    /// WAITING_REQUEST_APPROVAL (its own contact confirmed, a sibling's did not), and the review-due
    /// signals key off exactly that status — so without the gate they would flag the row as needing
    /// action and route it to the campus-review screen, where there would be nothing to click.
    /// </summary>
    [Fact]
    public async Task Behind_the_gate_the_row_is_not_flagged_as_a_review_that_is_due()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // HN confirmed, HCM not: HN really is WAITING_REQUEST_APPROVAL behind a shut gate.
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            var code = await RequestCodeAsync(requestId);

            var row = Assert.Single(await ListAsync(Leader(LeaderHn, CampusHn), code));
            Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, row.CampusStatus);

            var reviewer = Assert.Single(
                row.RelationContexts.Where(c => c.Relation == VisitRowRelations.CampusReviewer));
            Assert.False(reviewer.RequiresAction);
            Assert.NotEqual(VisitEntryContexts.CampusReview, reviewer.EntryContext);
            Assert.NotEqual(VisitEntryContexts.CampusReview, row.PrimaryEntryContext);
            // No next task either: VisitNextTaskBuilder reads AllowedActions, which withheld the decision.
            Assert.True(row.NextTask is null || !row.NextTask.RequiresAction);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case B — two campuses, one confirmed and one not ──────────────────────────

    /// <summary>
    /// The multi-campus heart of the rule. Each leader sees THEIR OWN campus row and neither may
    /// decide, because the gate belongs to the request: one campus confirming does not release its
    /// sibling's leader, and the confirmed campus's own leader is held too.
    /// </summary>
    [Fact]
    public async Task One_confirmed_campus_does_not_open_the_gate_for_either_leader()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            var code = await RequestCodeAsync(requestId);

            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, await RequestStatusAsync(requestId));

            var hnRow = Assert.Single(await ListAsync(Leader(LeaderHn, CampusHn), code));
            var hcmRow = Assert.Single(await ListAsync(Leader(LeaderHcm, CampusHcm), code));

            // Each leader sees their OWN campus and only it — the scoping this change had to preserve.
            Assert.Equal(CampusHn, hnRow.CampusId);
            Assert.Equal(CampusHcm, hcmRow.CampusId);

            foreach (var row in new[] { hnRow, hcmRow })
            {
                Assert.Contains("VIEW_DETAIL", row.AllowedActions);
                Assert.DoesNotContain(Approve, row.AllowedActions);
                Assert.DoesNotContain(Reject, row.AllowedActions);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case C — every contact confirmed ──────────────────────────────────────────

    /// <summary>
    /// The gate opens on the LAST confirmation, and both leaders get their decision back on the same
    /// rows they had been watching read-only. The aggregate reaches PENDING_APPROVAL through the DB
    /// trigger — nothing here writes <c>visit_requests.status</c> by hand.
    /// </summary>
    [Fact]
    public async Task The_last_confirmation_opens_the_gate_and_returns_both_decisions()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            var code = await RequestCodeAsync(requestId);

            // Shut: seen, not decidable.
            Assert.DoesNotContain(Approve,
                Assert.Single(await ListAsync(Leader(LeaderHn, CampusHn), code)).AllowedActions);

            await ConfirmContactAsync(state[CampusHcm].InstanceId);
            Assert.Equal(VisitRequestStatuses.PendingApproval, await RequestStatusAsync(requestId));

            // Open: seen, and decidable — for BOTH campuses, each on its own row.
            foreach (var (leader, campusId) in new[] { (Leader(LeaderHn, CampusHn), CampusHn), (Leader(LeaderHcm, CampusHcm), CampusHcm) })
            {
                var row = Assert.Single(await ListAsync(leader, code));
                Assert.Equal(campusId, row.CampusId);
                Assert.Contains(Approve, row.AllowedActions);
                Assert.Contains(Reject, row.AllowedActions);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case D — a leader of an unrelated campus ──────────────────────────────────

    /// <summary>
    /// The risk this change had to avoid: dropping the gate from the query must not turn the leader
    /// queue into "every request". A leader of a campus the request does not name sees nothing —
    /// whether the gate is shut or open, because campus responsibility never depended on it.
    /// </summary>
    [Fact]
    public async Task A_leader_of_an_unrelated_campus_sees_nothing_either_way()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            var code = await RequestCodeAsync(requestId);
            var stranger = Leader(LeaderDn, CampusDn);

            Assert.Empty(await ListAsync(stranger, code));
            Assert.Empty(await ListAsync(stranger, code, "all"));
            // …and the detail refuses them outright rather than returning an empty campus list.
            await Assert.ThrowsAsync<ForbiddenException>(() => DetailAsync(stranger, requestId));

            await ConfirmContactAsync(state[CampusHcm].InstanceId);
            Assert.Empty(await ListAsync(stranger, code));
            await Assert.ThrowsAsync<ForbiddenException>(() => DetailAsync(stranger, requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case E — HO ───────────────────────────────────────────────────────────────

    /// <summary>
    /// HO monitoring is untouched by this change in both directions: they saw behind-gate requests
    /// before (their population never asked the gate) and they still never decide one.
    /// </summary>
    [Fact]
    public async Task Ho_still_monitors_and_still_never_decides()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHn].InstanceId);
            var code = await RequestCodeAsync(requestId);

            var shut = Assert.Single(await ListAsync(Ho(), code));
            Assert.DoesNotContain(Approve, shut.AllowedActions);
            Assert.DoesNotContain(Reject, shut.AllowedActions);

            await ConfirmContactAsync(state[CampusHn].InstanceId);
            var open = Assert.Single(await ListAsync(Ho(), code));
            Assert.DoesNotContain(Approve, open.AllowedActions);
            Assert.DoesNotContain(Reject, open.AllowedActions);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case G — the backend guard, called directly ───────────────────────────────

    /// <summary>
    /// The invariant, asserted where it actually lives. The list is not an authorization boundary, so
    /// both commands are CALLED behind the gate by the campus's real leader with everything else
    /// correct — right campus, right lifecycle, valid host, current row version — and both must still
    /// refuse with ContactConfirmationRequired.
    ///
    /// <para>
    /// Reject is asserted alongside approve deliberately: protecting only the approve path would leave
    /// the whole rule bypassable through the other outcome of the same decision.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Approve_and_reject_are_both_refused_behind_the_gate_when_called_directly()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // HN confirmed and WAITING_REQUEST_APPROVAL; HCM behind the gate, so the request is too.
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            var hn = (await StateAsync(requestId))[CampusHn];
            Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, hn.Status);

            var leader = Leader(LeaderHn, CampusHn);

            var approveEx = await Assert.ThrowsAsync<ConflictException>(
                () => ApproveAsync(leader, requestId, hn.InstanceId, IcStaffHn, hn.RowVersion));
            Assert.Equal(OperationalContactErrorCodes.ContactConfirmationRequired, approveEx.ErrorCode);

            var rejectEx = await Assert.ThrowsAsync<ConflictException>(
                () => RejectAsync(leader, requestId, hn.InstanceId, hn.RowVersion));
            Assert.Equal(OperationalContactErrorCodes.ContactConfirmationRequired, rejectEx.ErrorCode);

            // Neither refusal left a mark: the campus is untouched and the aggregate did not move.
            var after = (await StateAsync(requestId))[CampusHn];
            Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, after.Status);
            Assert.Equal(VisitRequestStatuses.PendingContactConfirmation, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The other side of the same call: once the last contact confirms, the SAME approve the guard
    /// just refused goes through and lands the campus on ASSIGNED with its Host named. Without this,
    /// the refusal above could pass for a reason that has nothing to do with the gate.
    /// </summary>
    [Fact]
    public async Task The_same_approve_succeeds_once_the_gate_opens()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            await ConfirmContactAsync(state[CampusHcm].InstanceId);

            var hn = (await StateAsync(requestId))[CampusHn];
            await ApproveAsync(Leader(LeaderHn, CampusHn), requestId, hn.InstanceId, IcStaffHn, hn.RowVersion);

            Assert.Equal(VisitInstanceStatus.Assigned, (await StateAsync(requestId))[CampusHn].Status);
            // One campus decided, one still waiting → PARTIALLY_APPROVED. The aggregate rules are
            // exactly as they were; this change never touched them.
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Case F — the registrant who is also a Staff Leader ────────────────────────

    /// <summary>
    /// Registrant rights and campus decision rights are different things, and being both does not
    /// merge them. A Staff Leader who filed the request sees it through BOTH relations while the gate
    /// is shut — and still gets no decision, from either one.
    /// </summary>
    [Fact]
    public async Task Being_the_registrant_does_not_carry_a_decision_through_the_gate()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHn].InstanceId);
            var code = await RequestCodeAsync(requestId);

            // Make HN's own leader the registrant of the request they lead.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}",
                    requestId, LeaderHn);

            var row = Assert.Single(await ListAsync(Leader(LeaderHn, CampusHn), code, "all"));
            var relations = row.RelationContexts.Select(c => c.Relation).ToList();
            Assert.Contains(VisitRowRelations.Registrant, relations);
            Assert.Contains(VisitRowRelations.CampusReviewer, relations);

            Assert.DoesNotContain(Approve, row.AllowedActions);
            Assert.DoesNotContain(Reject, row.AllowedActions);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The detail endpoint (§18) ─────────────────────────────────────────────────

    /// <summary>
    /// The list is only half the change: opening the row has to work too. Behind the gate the campus's
    /// leader gets the detail — scoped to their own campus, as always — with neither decision offered,
    /// and the confirmation summary says WHY through <c>GateOpen</c>. It used to throw Forbidden here.
    /// </summary>
    [Fact]
    public async Task The_detail_opens_read_only_for_the_campus_leader_behind_the_gate()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);

            var detail = await DetailAsync(Leader(LeaderHn, CampusHn), requestId);

            // Own campus only — the read scope is unchanged and the sibling is still not disclosed.
            var campus = Assert.Single(detail.CampusVisits);
            Assert.Equal((long)CampusHn, campus.CampusId);
            Assert.DoesNotContain(Approve, campus.AllowedActions);
            Assert.DoesNotContain(Reject, campus.AllowedActions);
            Assert.False(detail.ConfirmationSummary?.GateOpen);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>The mirror: the gate opens and the same detail call now offers both decisions.</summary>
    [Fact]
    public async Task The_detail_offers_both_decisions_once_the_gate_opens()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("HCM"));
            var state = await StateAsync(requestId);
            await ShutGateAsync(state[CampusHcm].InstanceId);
            await ConfirmContactAsync(state[CampusHcm].InstanceId);

            var detail = await DetailAsync(Leader(LeaderHn, CampusHn), requestId);

            var campus = Assert.Single(detail.CampusVisits);
            Assert.Contains(Approve, campus.AllowedActions);
            Assert.Contains(Reject, campus.AllowedActions);
            Assert.True(detail.ConfirmationSummary?.GateOpen);
        }
        finally { await CleanupAsync(requestId); }
    }
}
