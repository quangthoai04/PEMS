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
using PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// WHO may reach the per-campus pending edit, and who may approve from it — decided by RELATION to that
/// campus, never by role name (plan §2.1 / §44).
///
/// <para>
/// The distinction has teeth because a registrant is not always a VISITOR. Staff register delegations for
/// their own department, and the code this replaces asked <c>RoleCode == VISITOR</c> before letting anyone
/// edit — which locked staff out of requests they had filed themselves, while the capability API cheerfully
/// offered them the button. So the cases here are deliberately about STAFF accounts:
/// </para>
/// <list type="bullet">
/// <item>STAFF who IS the registrant — edits, but cannot approve (they are not the campus's leader).</item>
/// <item>STAFF with no relation at all — refused, despite being staff.</item>
/// <item>STAFF LEADER who is ALSO the registrant — both sets of rights, including "Lưu và duyệt".</item>
/// </list>
/// <para>
/// The suite drives the COMMAND rather than the service, because authorization lives in the handler; the
/// service's own rules (sibling isolation, immutability, the 72-hour floor) are
/// <see cref="UpdatePendingVisitInstanceV2ServiceTests"/>'s subject.
/// </para>
/// </summary>
public sealed class PerCampusEditRelationAuthorizationTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    // Canonical seed. Campus 1 = HN, 2 = HCM.
    private const ulong LeaderHn = 3;      // STAFF / LEADER, campus 1
    private const ulong IcStaffHn = 101;   // STAFF / STAFF, IC, campus 1 — a valid Host for HN
    private const ulong IcStaffHcm = 103;  // STAFF / STAFF, IC, campus 2 — a stranger to HN
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

    /// <summary>A STAFF account with no leadership — the shape §2.1 cares about.</summary>
    private static FakeUser Staff(ulong id, ulong campusId)
        => new(id, RoleCodes.Staff, UserSubRoles.Staff, campusId);

    private static FakeUser StaffLeader(ulong id, ulong campusId)
        => new(id, RoleCodes.Staff, UserSubRoles.Leader, campusId);

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType,
            string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static UpdatePendingVisitInstanceV2CommandHandler Handler(ApplicationDbContext db, FakeUser actor)
    {
        var notifications = new NoopNotifications();
        return new UpdatePendingVisitInstanceV2CommandHandler(
            db, actor, new FixedClock(),
            new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db)),
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db),
                notifications,
                new PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService(
                    db, actor,
                    NullLogger<PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>.Instance,
                    new FixedClock()),
                NullLogger<CampusApprovalExecutor>.Instance),
            notifications,
            NullLogger<UpdatePendingVisitInstanceV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true },
            new PerCampusFormV2WriteOptions { Enabled = true });
    }

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code, string registrantEmail, DateTime start, string delegation = "Đoàn Base")
        => new(
            code, start, start.AddMinutes(120), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the registrant's own address, so the campus self-matches at submit and the
            // request is past the confirmation gate immediately. That gate is not this suite's subject,
            // and a campus behind it cannot be edited or decided at all.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", registrantEmail),
            "EN", null, "DECLINED", null, null);

    /// <summary>
    /// A committed single-campus (HN) request whose REGISTRANT is <paramref name="registrantUserId"/>,
    /// left at WAITING_REQUEST_APPROVAL. Filed 20 days out so the 72-hour floor is not in play unless a
    /// test deliberately moves the date.
    /// </summary>
    private static async Task<(ulong RequestId, ulong InstanceId)> CreatePendingAsync(ulong registrantUserId)
    {
        var email = V2SeedActor.Email(registrantUserId);
        using var db = NewContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var created = await new VisitRequestV2CreateService(db).CreateV2Async(
            new VisitRequestFormDataV2(
                Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", email),
                null, new List<CampusVisitFormDto> { Campus("HN", email, Now.AddDays(20)) }),
            registrantUserId, "VISITOR_SUBMITTED", Now, CancellationToken.None);
        await tx.CommitAsync();

        var instanceId = created.CampusInstances.Single().VisitInstanceId;
        return (created.VisitRequestId, instanceId);
    }

    private static async Task<CampusVisitEditV2Dto> PayloadAsync(
        ulong instanceId, string delegation, DateTime? start = null)
    {
        using var db = NewContext();
        var i = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
        var from = start ?? i.PlannedStartAt;
        var content = Campus("HN", "unused@example.com", from, delegation);
        return new CampusVisitEditV2Dto(
            instanceId, i.RowVersion, content.CampusId, from, from.AddMinutes(120),
            content.DelegationName, content.VisitType, content.VisitTypeOther, content.Purpose, content.WorkingContent,
            content.Visitors, content.ExternalSupportMembers,
            // Contact identity is immutable through this route; echo back what is stored.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410",
                (await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceId))
                    .OperationalContactEmail ?? "op@example.com"),
            content.WorkingLanguage, content.TransportationNote, content.MediaConsentStatus, content.Notes);
    }

    /// <summary>Drops sub-second precision, which <c>DATETIME</c> does not keep.</summary>
    private static DateTime TrimToSecond(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);

    private static async Task<string> DelegationOfAsync(ulong instanceId)
    {
        using var db = NewContext();
        return (await db.VisitInstanceFormDetails.AsNoTracking()
            .SingleAsync(d => d.VisitInstanceId == instanceId)).DelegationName ?? "";
    }

    private static async Task CleanupAsync(ulong id)
    {
        if (id == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM audit_log_changes WHERE audit_log_id IN (SELECT audit_log_id FROM audit_logs WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── CASE A — a STAFF account that filed the request edits it ────────────────────────────────

    /// <summary>
    /// The registrant's rights come from being the registrant. Being STAFF neither grants nor removes
    /// them — which is the whole of §2.1, and the case the old <c>RoleCode == VISITOR</c> guard broke.
    /// </summary>
    [Fact]
    public async Task A_staff_account_that_is_the_registrant_may_edit_its_own_pending_campus()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(IcStaffHn);

            using var db = NewContext();
            var res = await Handler(db, Staff(IcStaffHn, CampusHn)).Handle(
                new UpdatePendingVisitInstanceV2Command(
                    requestId, instanceId, await PayloadAsync(instanceId, "Đoàn STAFF tự sửa"), false, null),
                CancellationToken.None);

            Assert.False(res.Approved);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, res.VisitInstanceStatus);
            Assert.Equal("Đoàn STAFF tự sửa", await DelegationOfAsync(instanceId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── CASE B — …but filing it does not make them its approver ─────────────────────────────────

    /// <summary>
    /// "Lưu và duyệt" is the campus Staff Leader's act. A registrant who happens to work at the campus is
    /// still the person asking, and approving your own request is the one thing the split exists to stop.
    /// </summary>
    [Fact]
    public async Task A_staff_registrant_who_is_not_the_campus_leader_may_not_approve_from_the_edit()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(IcStaffHn);
            var before = await DelegationOfAsync(instanceId);
            var payload = await PayloadAsync(instanceId, "Đoàn tự duyệt");

            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, Staff(IcStaffHn, CampusHn)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            requestId, instanceId, payload, false,
                            new ApproveAfterSaveDto(IcStaffHn, "tự duyệt")),
                        CancellationToken.None));

            // Refused BEFORE anything was written: the edit does not sneak through on a rejected approval.
            Assert.Equal(before, await DelegationOfAsync(instanceId));
            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);
            Assert.Null(instance.CurrentHostUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── CASE C — a STAFF account with no relation is a stranger ─────────────────────────────────

    [Fact]
    public async Task A_staff_account_with_no_relation_to_the_campus_is_refused()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(IcStaffHn);

            // IC staff of ANOTHER campus: not the registrant, not this campus's contact, not its leader.
            var strangerPayload = await PayloadAsync(instanceId, "Đoàn người lạ");
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, Staff(IcStaffHcm, 2)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            requestId, instanceId, strangerPayload, false, null),
                        CancellationToken.None));

            // A Staff LEADER of another campus is equally a stranger — the campus is part of the relation.
            var otherLeaderPayload = await PayloadAsync(instanceId, "Đoàn leader cơ sở khác");
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, StaffLeader(9, 2)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            requestId, instanceId, otherLeaderPayload, false, null),
                        CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── CASE D — Staff Leader who is also the registrant: both sets of rights ───────────────────

    /// <summary>
    /// Save and "save and approve" are different acts even for the person entitled to both: a plain save
    /// leaves the campus WAITING (§30.1). The approval, when asked for, carries the Host with it.
    /// </summary>
    [Fact]
    public async Task A_staff_leader_who_is_the_registrant_may_save_and_may_save_and_approve()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(LeaderHn);
            var leader = StaffLeader(LeaderHn, CampusHn);

            // 1. Save only — editing is not deciding.
            using (var db = NewContext())
            {
                var saved = await Handler(db, leader).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, instanceId, await PayloadAsync(instanceId, "Đoàn leader sửa"), false, null),
                    CancellationToken.None);
                Assert.False(saved.Approved);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, saved.VisitInstanceStatus);
            }

            // 2. Save AND approve — one call, one transaction, Host named.
            using (var db = NewContext())
            {
                var approved = await Handler(db, leader).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, instanceId, await PayloadAsync(instanceId, "Đoàn leader duyệt"), false,
                        new ApproveAfterSaveDto(IcStaffHn, "Đồng ý tiếp đoàn")),
                    CancellationToken.None);
                Assert.True(approved.Approved);
                Assert.Equal(IcStaffHn, approved.HostUserId);
                Assert.Equal(VisitInstanceStatuses.Assigned, approved.VisitInstanceStatus);
            }

            // Both halves landed: the content AND the decision.
            Assert.Equal("Đoàn leader duyệt", await DelegationOfAsync(instanceId));
            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, instance.Status);
            Assert.Equal(IcStaffHn, instance.CurrentHostUserId);
            Assert.Equal(LeaderHn, instance.DecidedBy);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §15 — the two halves of "Lưu và duyệt" commit together or not at all. Approving with no Host is
    /// refused (an ASSIGNED campus without one is a campus nobody can act on), and the edit that travelled
    /// with it must be gone too: a form rewritten to a schedule nobody approved is the failure this
    /// single transaction exists to prevent.
    /// </summary>
    [Fact]
    public async Task A_refused_approval_takes_the_edit_down_with_it()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(LeaderHn);
            var before = await DelegationOfAsync(instanceId);
            var payload = await PayloadAsync(instanceId, "Đoàn sẽ bị rollback");

            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, StaffLeader(LeaderHn, CampusHn)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            requestId, instanceId, payload, false,
                            new ApproveAfterSaveDto(0, null)),   // no Host → HOST_REQUIRED_ON_APPROVAL
                        CancellationToken.None));

            Assert.Equal(before, await DelegationOfAsync(instanceId));
            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, instance.Status);
            Assert.Null(instance.CurrentHostUserId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §31 — the 72-hour floor is the campus LEADER's to pass, and only after saying so. Unconfirmed is a
    /// question (409), not a refusal; confirmed applies and leaves its own audit row.
    /// </summary>
    [Fact]
    public async Task Only_the_campus_leader_may_file_a_schedule_inside_the_floor_and_only_after_confirming()
    {
        RequireDb();
        var (requestId, instanceId) = (0UL, 0UL);
        try
        {
            (requestId, instanceId) = await CreatePendingAsync(LeaderHn);
            // Whole seconds: the column is DATETIME, so anything finer is truncated on the way in and the
            // round-trip comparison below would fail on precision rather than on the rule being tested.
            var tooSoon = TrimToSecond(Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-30));

            // Unconfirmed → asked, not refused, and nothing applied.
            var askPayload = await PayloadAsync(instanceId, "Đoàn dời gần", tooSoon);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, StaffLeader(LeaderHn, CampusHn)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            requestId, instanceId, askPayload, false, null),
                        CancellationToken.None));
                Assert.Equal(VisitMutationErrorCodes.LeadTimeOverrideConfirmationRequired, ex.ErrorCode);
            }

            // Confirmed → applied, with the override recorded as a decision of its own.
            using (var db = NewContext())
                await Handler(db, StaffLeader(LeaderHn, CampusHn)).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, instanceId, await PayloadAsync(instanceId, "Đoàn dời gần", tooSoon), true, null),
                    CancellationToken.None);

            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(tooSoon, instance.PlannedStartAt);
            Assert.True(await check.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.VisitInstanceId == instanceId && a.Action == VisitAuditActions.LeadTimeOverride));

            // The REGISTRANT side cannot do the same thing, flag or no flag — the override is a relation.
            var (otherRequestId, otherInstanceId) = await CreatePendingAsync(IcStaffHn);
            try
            {
                var registrantPayload = await PayloadAsync(otherInstanceId, "Đoàn dời gần", tooSoon);
                using var db = NewContext();
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, Staff(IcStaffHn, CampusHn)).Handle(
                        new UpdatePendingVisitInstanceV2Command(
                            otherRequestId, otherInstanceId, registrantPayload, true, null),
                        CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            }
            finally { await CleanupAsync(otherRequestId); }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §16 — the instance is resolved INSIDE the named request's own campus collection, so a real
    /// instance id belonging to somebody else's request resolves to nothing rather than to itself.
    /// </summary>
    [Fact]
    public async Task A_campus_of_another_request_is_not_reachable_through_this_route()
    {
        RequireDb();
        var (mine, _) = (0UL, 0UL);
        var (theirs, theirInstance) = (0UL, 0UL);
        try
        {
            (mine, _) = await CreatePendingAsync(LeaderHn);
            (theirs, theirInstance) = await CreatePendingAsync(IcStaffHn);

            var payload = await PayloadAsync(theirInstance, "Đoàn IDOR");
            using var db = NewContext();
            await Assert.ThrowsAsync<NotFoundException>(() =>
                Handler(db, StaffLeader(LeaderHn, CampusHn)).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        mine, theirInstance, payload, false, null),
                    CancellationToken.None));
        }
        finally
        {
            await CleanupAsync(mine);
            await CleanupAsync(theirs);
        }
    }
}
