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
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// HO population/visibility regression coverage — a SEPARATE concern from the request-level status
/// filter itself (<see cref="EffectiveStatusFilterTests"/>). HO is the whole-request monitor: for the
/// SAME keyword/campus/date/scope with no restricting filter, every VisitRequest a Staff Leader can see
/// must exist in HO's population too. Under REQUEST_ANY_CAMPUS (docs/CanhIter3FixBug/GopYCQuyen/
/// PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md, 2026-08-23), the SAME request can legitimately
/// match BOTH a Staff Leader's own INSTANCE_EXACT filter AND HO's ANY-campus filter for two DIFFERENT
/// statuses at once (one campus ASSIGNED, a sibling still WAITING_REQUEST_APPROVAL) — see
/// <see cref="Ho_StatusFilter_MatchesAnyCampusInstance_NotJustTheParentBadgeBucket"/>. What stays a
/// display-only fact, never a filter gate, is the parent row's single aggregate badge
/// (<c>StatusLabel</c>/<c>EffectiveStatusCode</c>, from <c>VisitRowLabels.Resolve</c>) — that can read
/// differently for HO vs. a Staff Leader on the SAME request, and that disagreement is not a bug either.
///
/// <para>
/// <c>QueryRequestLevelAsync</c>'s HO branch (<c>ViewGuestDelegationListQueryHandler.cs</c>, around the
/// comment "HO sees every MULTI_CAMPUS request AND every SINGLE_CAMPUS request") adds NO
/// ownership/campus-responsibility predicate — <c>_context.VisitRequests.AsQueryable()</c> untouched
/// before the optional keyword/campus/date/scope/timing/relation/status filters. A Staff Leader's own
/// population (<c>QueryInstanceLevelAsync</c>'s Leader branch) is instead narrowed to their own campus
/// AND gated behind the global operational-contact-confirmation stage. Both of those restrictions only
/// ever REMOVE rows from the Staff Leader's side, never ADD a row HO's unrestricted query would miss —
/// so on paper HO's population is a structural superset. These tests are the empirical check of that
/// claim against the real triggers/DB, not just the code reading.
/// </para>
/// </summary>
public sealed class HoVisibilityPopulationTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;

    private static bool? _dbUp;
    private static string? _dbFailure;
    private static readonly DateTime Now = DateTime.Now;
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
        public FakeUser(ulong id, string role = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = role; SubRole = subRole; PrimaryCampusId = campusId; }
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

    private static CampusVisitFormDto Campus(string code, ulong actor) =>
        new(code, Now.AddDays(25), Now.AddDays(25).AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng", "+84912345678", V2SeedActor.Email(actor)),
            "EN", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(ulong actor, string actorRole, params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(actor, actorRole), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "XB" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(actor)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, ulong LeaderId, string Status);

    private static async Task<Dictionary<string, CampusRow>> StateAsync(ulong requestId)
    {
        using var db = NewContext();
        return await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitRequestId == requestId
            select new
            {
                site.CampusCode,
                Row = new CampusRow(c.VisitInstanceId, c.CampusId, c.CoordinatorUserId!.Value, c.Status)
            })
            .ToDictionaryAsync(x => x.CampusCode, x => x.Row);
    }

    private static async Task<string> RequestCodeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
    }

    private static async Task ApproveAsync(ulong instanceId, ulong hostUserId)
    {
        using var db = NewContext();
        var leaderId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CoordinatorUserId!.Value).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_source = 'STANDARD_CAMPUS_REVIEW', " +
            "current_host_user_id = {3}, host_assigned_by = {1}, host_assigned_at = {2} " +
            "WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30), hostUserId);
    }

    private static async Task SetCampusStatusAsync(ulong instanceId, string status)
    {
        using var db = NewContext();
        if (status == VisitInstanceStatuses.Cancelled)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1}, cancelled_by = {2}, cancelled_at = {3}, " +
                "cancellation_actor_type = 'VISITOR', cancellation_source = 'SELF_SERVICE', cancellation_reason = {4} " +
                "WHERE visit_instance_id = {0}",
                instanceId, status, Registrant, Now, "Test fixture cancellation");
        }
        else if (status == VisitInstanceStatuses.Rejected)
        {
            var leaderId = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CoordinatorUserId!.Value).FirstAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1}, decided_by = {2}, decided_at = {3}, " +
                "decision_actor_role = 'STAFF_LEADER', decision_note = {4} WHERE visit_instance_id = {0}",
                instanceId, status, leaderId, Now, "Test fixture rejection");
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1} WHERE visit_instance_id = {0}", instanceId, status);
        }
    }

    private static async Task SetRequestStatusAsync(ulong requestId, string status)
    {
        using var db = NewContext();
        if (status == VisitRequestStatuses.Cancelled)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_requests SET status = {1}, cancelled_by = {2}, cancelled_at = {3}, cancellation_reason = {4} " +
                "WHERE visit_request_id = {0}",
                requestId, status, Registrant, Now, "Test fixture cancellation");
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_requests SET status = {1} WHERE visit_request_id = {0}", requestId, status);
        }
    }

    private static async Task<ulong> RegularStaffAtAsync(ulong campusId)
    {
        using var db = NewContext();
        var id = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Staff
                        && u.Status == UserStatuses.Active && u.PrimaryCampusId == campusId)
            .OrderBy(u => u.UserId).Select(u => (ulong?)u.UserId).FirstOrDefaultAsync();
        Assert.True(id.HasValue, $"Campus {campusId} has no ACTIVE regular Staff to host with.");
        return id!.Value;
    }

    private static async Task<PEMS.Application.Common.Models.PaginatedResult<VisitRequestManagementItemDto>> ListAsync(
        ICurrentUserService caller, string keyword, string tab = "responsible",
        string? effectiveStatus = null, string? effectiveStatuses = null)
    {
        using var db = NewContext();
        var handler = new ViewGuestDelegationListQueryHandler(db, caller, new FixedClock());
        return await handler.Handle(
            new ViewGuestDelegationListQuery
            {
                Tab = tab, Page = 1, PageSize = 200, Keyword = keyword,
                EffectiveStatus = effectiveStatus, EffectiveStatuses = effectiveStatuses,
            },
            CancellationToken.None);
    }

    /// <summary>Walks an approved campus up the post-ASSIGNED ladder one guarded step at a time
    /// (mirrors <see cref="EffectiveStatusFilterTests"/>'s own helper of the same shape).</summary>
    private static async Task AdvanceToAsync(ulong instanceId, string target)
    {
        var ladder = new[]
        {
            VisitInstanceStatuses.BeforeVisit,
            VisitInstanceStatuses.DuringVisit,
            VisitInstanceStatuses.AfterVisit,
            VisitInstanceStatuses.Closed,
        };
        using var db = NewContext();
        foreach (var step in ladder)
        {
            if (step == VisitInstanceStatuses.DuringVisit)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO visit_agendas (visit_instance_id, sequence_order, title, start_time, end_time) " +
                    "SELECT {0}, 1, 'Tiếp đón đoàn', c.planned_start_at, c.planned_end_at " +
                    "FROM visit_request_campuses c WHERE c.visit_instance_id = {0}",
                    instanceId);
            }
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE visit_request_campuses SET status = {1} WHERE visit_instance_id = {0}",
                instanceId, step);
            if (step == target) return;
        }
        throw new ArgumentOutOfRangeException(nameof(target), target, "Not a step on the post-ASSIGNED ladder.");
    }

    /// <summary>Drives a fresh single campus to the target status the same way the real product would
    /// reach it (mirrors <see cref="EffectiveStatusFilterTests.DriveToStatusAsync"/>).</summary>
    private static async Task DriveToStatusAsync(ulong requestId, ulong instanceId, ulong hostUserId, string finalCampusStatus)
    {
        if (finalCampusStatus == VisitInstanceStatuses.Rejected)
        {
            await SetCampusStatusAsync(instanceId, VisitInstanceStatuses.Rejected);
            return;
        }

        await ApproveAsync(instanceId, hostUserId);
        if (finalCampusStatus == VisitInstanceStatuses.Assigned) return;

        await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);
        if (finalCampusStatus == VisitInstanceStatuses.Cancelled)
        {
            await SetCampusStatusAsync(instanceId, VisitInstanceStatuses.Cancelled);
            return;
        }
        await AdvanceToAsync(instanceId, finalCampusStatus);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE c FROM visit_instance_amendment_changes c JOIN visit_instance_amendments a ON a.amendment_id = c.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Baseline population checks: HO sees single- and multi-campus requests with no status filter. ──

    [Fact]
    public async Task Ho_AllStatuses_IncludesSingleCampusRequest()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var ho = new FakeUser(999_101, RoleCodes.Ho);

            Assert.Contains((await ListAsync(leader, code)).Items, r => r.VisitRequestId == requestId);
            Assert.Contains((await ListAsync(ho, code)).Items, r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Ho_AllStatuses_IncludesMultiCampusRequest()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            await ApproveAsync(state["HN"].InstanceId, await RegularStaffAtAsync(state["HN"].CampusId));
            await ApproveAsync(state["DN"].InstanceId, await RegularStaffAtAsync(state["DN"].CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            var hnLeader = new FakeUser(state["HN"].LeaderId, RoleCodes.Staff, UserSubRoles.Leader, state["HN"].CampusId);
            var dnLeader = new FakeUser(state["DN"].LeaderId, RoleCodes.Staff, UserSubRoles.Leader, state["DN"].CampusId);
            var ho = new FakeUser(999_102, RoleCodes.Ho);

            Assert.Contains((await ListAsync(hnLeader, code)).Items, r => r.VisitRequestId == requestId);
            Assert.Contains((await ListAsync(dnLeader, code)).Items, r => r.VisitRequestId == requestId);
            Assert.Contains((await ListAsync(ho, code)).Items, r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Ho_AllStatuses_IncludesPartiallyApprovedRequest()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId)); // DN stays WAITING_REQUEST_APPROVAL
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var hnLeader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var dnLeader = new FakeUser(state["DN"].LeaderId, RoleCodes.Staff, UserSubRoles.Leader, state["DN"].CampusId);
            var ho = new FakeUser(999_103, RoleCodes.Ho);

            Assert.Contains((await ListAsync(hnLeader, code)).Items, r => r.VisitRequestId == requestId);
            Assert.Contains((await ListAsync(dnLeader, code)).Items, r => r.VisitRequestId == requestId);
            var hoAll = (await ListAsync(ho, code)).Items.Where(r => r.VisitRequestId == requestId).ToList();
            Assert.Single(hoAll);
            Assert.Equal("Chờ duyệt", hoAll[0].StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The exact scenario from the bug report: HN=ASSIGNED, DN=WAITING_REQUEST_APPROVAL. Superseded
    // 2026-08-23 (docs/CanhIter3FixBug/GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md):
    // HO's status filter is now REQUEST_ANY_CAMPUS, so HO must find this SAME request under BOTH "Đã
    // duyệt" (HN) AND "Chờ duyệt" (DN) at once — not just under whichever bucket its aggregate parent
    // badge happens to summarize. Staff Leader HN stays INSTANCE_EXACT (own campus only), unchanged. ──

    [Fact]
    public async Task Ho_StatusFilter_MatchesAnyCampusInstance_NotJustTheParentBadgeBucket()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId)); // HN -> ASSIGNED, DN stays WAITING_REQUEST_APPROVAL
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var hnLeader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var ho = new FakeUser(999_104, RoleCodes.Ho);

            // Staff Leader HN filtering "Đã duyệt" (their own ASSIGNED) → included, badge agrees.
            // INSTANCE_EXACT is untouched by this fix.
            var leaderMatch = Assert.Single(
                (await ListAsync(hnLeader, code, effectiveStatus: VisitInstanceStatuses.Assigned))
                    .Items.Where(r => r.VisitRequestId == requestId));
            Assert.Equal("Đã duyệt", leaderMatch.StatusLabel);

            // HO / Tất cả trạng thái (no filter at all) → still finds the request. The parent badge is
            // still a single aggregate summary for DISPLAY ("Chờ duyệt" here) — that has not changed.
            var hoAllMatch = Assert.Single(
                (await ListAsync(ho, code)).Items.Where(r => r.VisitRequestId == requestId));
            Assert.Equal("Chờ duyệt", hoAllMatch.StatusLabel);

            // HO / Chờ duyệt → included (DN genuinely is WAITING_REQUEST_APPROVAL).
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);

            // HO / Đã duyệt → NOW ALSO included (HN genuinely is ASSIGNED). The SAME request appears
            // under both filters at once — that is the intended REQUEST_ANY_CAMPUS behavior, not a
            // duplicate row (still exactly one parent row either way) and not a bug to "fix" back to
            // exclusion. Parent badge staying "Chờ duyệt" above is a display fact, never a filter gate.
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Clearing the status filter must remove every status predicate, not just the one the UI last
    // touched — a request sitting in a status HO's OWN dropdown does not even list must still surface
    // once every EffectiveStatus/EffectiveStatuses param is genuinely absent from the request object
    // (mirrors what "Tất cả trạng thái" sends: no status param at all, not an empty-string one). ──

    [Fact]
    public async Task Ho_ClearStatus_RemovesAllStatusPredicates()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            await SetCampusStatusAsync(hn.InstanceId, VisitInstanceStatuses.Rejected);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Rejected);

            using var db = NewContext();
            var ho = new FakeUser(999_105, RoleCodes.Ho);
            var handler = new ViewGuestDelegationListQueryHandler(db, ho, new FixedClock());

            var cleared = await handler.Handle(
                new ViewGuestDelegationListQuery
                {
                    Tab = "responsible", Page = 1, PageSize = 200, Keyword = code,
                    EffectiveStatus = null, EffectiveStatuses = null,
                    RequestStatus = null, CampusStatus = null,
                    CancelledOnly = false, PendingApprovalAny = false, ApprovedAny = false,
                },
                CancellationToken.None);

            Assert.Contains(cleared.Items, r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Broad matrix: every status a single-campus request can reach that a Staff Leader's own
    // (campus-gated, not host-gated — see QueryInstanceLevelAsync's Leader branch) population shows,
    // HO's unfiltered "Tất cả trạng thái" population must show too. ──

    public static IEnumerable<object[]> LeaderVisibleStatuses()
    {
        yield return new object[] { VisitInstanceStatuses.Assigned };
        yield return new object[] { VisitInstanceStatuses.BeforeVisit };
        yield return new object[] { VisitInstanceStatuses.DuringVisit };
        yield return new object[] { VisitInstanceStatuses.AfterVisit };
        yield return new object[] { VisitInstanceStatuses.Closed };
        yield return new object[] { VisitInstanceStatuses.Cancelled };
        yield return new object[] { VisitInstanceStatuses.Rejected };
    }

    [Theory]
    [MemberData(nameof(LeaderVisibleStatuses))]
    public async Task Ho_AllStatuses_IncludesEveryRequestVisibleToCampusLeader(string campusStatus)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            await DriveToStatusAsync(requestId, hn.InstanceId, await RegularStaffAtAsync(hn.CampusId), campusStatus);

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var ho = new FakeUser(999_106, RoleCodes.Ho);

            var leaderVisible = (await ListAsync(leader, code)).Items.Any(r => r.VisitRequestId == requestId);
            Assert.True(leaderVisible, $"Fixture setup problem: Staff Leader cannot even see campusStatus={campusStatus} — not a HO question yet.");

            Assert.Contains((await ListAsync(ho, code)).Items, r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The literal named scenarios from the canonical seed (docs/database/scripts/
    // PEMS_FULL_VS_31_07_NEW.sql, "R1. OPERATIONAL CONTACT SCENARIO MATRIX", requests 47005/47011) —
    // the exact rows the bug report's own screenshot names. Both are MULTI_CAMPUS, aggregate
    // PARTIALLY_APPROVED: campus 1 (HN) is ASSIGNED, the other campus is still WAITING_REQUEST_
    // APPROVAL. Run against whatever data is ALREADY in the test DB (no fixture creation) so this is
    // the closest thing to literally re-running the reported repro, not a synthetic recreation of it. ──

    private static async Task<(ulong RequestId, ulong HnLeaderId, ulong HnCampusId)?> SeededOpcRequestAsync(string requestCode)
    {
        using var db = NewContext();
        var requestId = await db.VisitRequests.AsNoTracking()
            .Where(v => v.RequestCode == requestCode).Select(v => (ulong?)v.VisitRequestId).FirstOrDefaultAsync();
        if (requestId is null or 0) return null;

        var hnLeaderId = await db.Users.AsNoTracking()
            .Where(u => u.Email!.ToLower() == "staff.leader.hn@fpt.edu.vn")
            .Select(u => (ulong?)u.UserId).FirstOrDefaultAsync();
        if (hnLeaderId is null or 0) return null;

        var hnCampusId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId.Value && c.CoordinatorUserId == hnLeaderId.Value)
            .Select(c => (ulong?)c.CampusId).FirstOrDefaultAsync();
        if (hnCampusId is null or 0) return null;

        return (requestId.Value, hnLeaderId.Value, hnCampusId.Value);
    }

    [Theory]
    [InlineData("OPC-05-PARTIAL")]
    [InlineData("OPC-11-HO-MONITOR-ONLY")]
    public async Task Ho_sees_the_named_canonical_seed_scenario_under_All_and_under_every_campus_it_actually_has(string requestCode)
    {
        RequireDb();
        var seeded = await SeededOpcRequestAsync(requestCode);
        if (seeded is null)
        {
            // The canonical R1 scenario matrix is not present in this test DB (a different seed, or
            // pems_pr3_test predates the OPC-01..OPC-11 block) — nothing to assert against here; the
            // synthetic-fixture tests above already cover this exact shape. Confirmed present and
            // exercised (not silently skipped) as of this change: both OPC-05-PARTIAL and
            // OPC-11-HO-MONITOR-ONLY were found and asserted against the real pems_pr3_test data.
            return;
        }
        var (requestId, hnLeaderId, hnCampusId) = seeded.Value;

        var hnLeader = new FakeUser(hnLeaderId, RoleCodes.Staff, UserSubRoles.Leader, hnCampusId);
        var ho = new FakeUser(999_107, RoleCodes.Ho);

        // Staff Leader HN reads their own campus as ASSIGNED = "Đã duyệt". INSTANCE_EXACT, unchanged.
        var leaderMatch = Assert.Single(
            (await ListAsync(hnLeader, requestCode, effectiveStatus: VisitInstanceStatuses.Assigned))
                .Items.Where(r => r.VisitRequestId == requestId));
        Assert.Equal("Đã duyệt", leaderMatch.StatusLabel);

        // HO / Tất cả trạng thái (no filter) — the request the original report said HO could not find.
        var hoAll = (await ListAsync(ho, requestCode)).Items.Where(r => r.VisitRequestId == requestId).ToList();
        Assert.True(hoAll.Count == 1, $"{requestCode}: expected HO to find exactly 1 row under 'Tất cả trạng thái', found {hoAll.Count}.");
        Assert.Equal("Chờ duyệt", hoAll[0].StatusLabel);

        // HO / Chờ duyệt — matches: the sibling campus genuinely is still WAITING_REQUEST_APPROVAL.
        Assert.Contains(
            (await ListAsync(ho, requestCode, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
            r => r.VisitRequestId == requestId);

        // HO / Đã duyệt — REQUEST_ANY_CAMPUS (docs/CanhIter3FixBug/GopYCQuyen/
        // PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md §3.1, superseding the earlier P0-01
        // "correctly excluded" assertion here): HN genuinely IS ASSIGNED, so this request must ALSO
        // appear under "Đã duyệt" — the SAME request matching both filters at once is intended, still
        // exactly one parent row, and the "Tất cả trạng thái" badge above staying "Chờ duyệt" is a
        // display fact that must never gate this filter.
        Assert.Contains(
            (await ListAsync(ho, requestCode, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
            r => r.VisitRequestId == requestId);
    }
}
