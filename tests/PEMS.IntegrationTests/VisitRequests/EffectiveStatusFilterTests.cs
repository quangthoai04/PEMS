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
/// Status-filter regression coverage for request-level rows (<c>QueryRequestLevelAsync</c> — HO
/// monitoring, Visitor's own contact-owner view, and the "registered" tab for any role) and the
/// merged "all" tab. Originally written around a single-canonical-bucket resolution per row
/// (P0-01); superseded 2026-08-23 by REQUEST_ANY_CAMPUS semantics — see docs/CanhIter3FixBug/
/// GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md. Under REQUEST_ANY_CAMPUS, a
/// request is INCLUDED under a chosen status the moment ANY of its own campus instances carries
/// that status — a multi-campus request straddling two stages appears under BOTH of their filters
/// at once, never collapsed to one bucket. The row's <c>StatusLabel</c>/<c>EffectiveStatusCode</c>
/// stay a single aggregate badge for DISPLAY only (<c>VisitRowLabels.Resolve</c>/
/// <c>MultiCampusProgress</c>, unchanged by this fix) — the filter must never be limited to that one
/// bucket. Runs against the real MySQL/Pomelo test DB (not mocked) specifically because the filter is
/// implemented as SQL predicates (<c>ViewGuestDelegationListQueryHandler.ApplyRequestLevelAnyCampusStatusFilter</c>),
/// and this is the gate on whether those predicates actually translate rather than just reading
/// correctly on paper.
/// </summary>
public sealed class EffectiveStatusFilterTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    /// <summary>The seeded Visitor every fixture registers as unless a test names somebody else.</summary>
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

    // ── Fixtures (mirrors RelationFilterEntryContextTests.cs's own — kept self-contained per this
    // test project's convention of one file, one set of local helpers, rather than a shared base). ──

    /// <summary>A campus whose contact email is <paramref name="actor"/>'s own, so it self-matches at
    /// submit and the confirmation gate clears immediately (straight to WAITING_REQUEST_APPROVAL).</summary>
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

    /// <summary>Approves a campus and names <paramref name="hostUserId"/> its Host, leaving it ASSIGNED.</summary>
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

    /// <summary>Direct status set for stages ApproveAsync doesn't reach (BEFORE_VISIT/CANCELLED/REJECTED/…).</summary>
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
            // A trigger requires cancelled_by/cancelled_at/cancellation_reason whenever status flips
            // to CANCELLED — matching what the real cancel command itself would have set.
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

    private static async Task SetRegistrantAsync(ulong requestId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}", requestId, userId);
    }

    /// <summary>An ACTIVE regular Staff (not a Leader) whose own campus is <paramref name="campusId"/>.</summary>
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
        string? effectiveStatus = null, string? effectiveStatuses = null, ulong? campusId = null)
    {
        using var db = NewContext();
        var handler = new ViewGuestDelegationListQueryHandler(db, caller, new FixedClock());
        return await handler.Handle(
            new ViewGuestDelegationListQuery
            {
                Tab = tab, Page = 1, PageSize = 200, Keyword = keyword,
                EffectiveStatus = effectiveStatus, EffectiveStatuses = effectiveStatuses,
                CampusId = campusId,
            },
            CancellationToken.None);
    }

    private static IEnumerable<string> RelationsOf(VisitRequestManagementItemDto row) =>
        row.RelationContexts.Select(c => c.Relation).Distinct();

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

    // ── REQUEST_ANY_CAMPUS: a multi-campus request straddling two stages must appear under BOTH
    // stages' filters — for HO monitoring AND for the registrant's own "registered" tab alike. ──

    /// <summary>
    /// A multi-campus request with one campus ASSIGNED and one still WAITING_REQUEST_APPROVAL
    /// (aggregate PARTIALLY_APPROVED). Its parent badge stays a single aggregate summary ("Chờ duyệt"
    /// for HO — VisitRowLabelsTests already pins the label) for DISPLAY only; the FILTER must find the
    /// row under BOTH EffectiveStatus=ASSIGNED and EffectiveStatus=WAITING_REQUEST_APPROVAL, because
    /// each is genuinely true of one of its campuses — for HO AND for the registrant's own tracking
    /// view, uniformly (no more per-role bucket resolution on this query).
    /// </summary>
    [Fact]
    public async Task Multi_campus_request_appears_under_every_status_any_of_its_campuses_actually_has()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("HCM", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];

            var host = await RegularStaffAtAsync(hn.CampusId);
            await ApproveAsync(hn.InstanceId, host); // HN -> ASSIGNED, HCM stays WAITING_REQUEST_APPROVAL
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var ho = new FakeUser(999_001, RoleCodes.Ho);

            // HO / Đã duyệt (ASSIGNED) → INCLUDED: HN genuinely is ASSIGNED, even though the parent
            // badge for the row still reads "Chờ duyệt".
            Assert.Contains(
                (await ListAsync(ho, code, "responsible", effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);

            // HO / Chờ duyệt (WAITING_REQUEST_APPROVAL) → ALSO included: HCM genuinely is still
            // waiting. The row's own badge is unaffected by which filter matched it.
            var hoMatch = Assert.Single(
                (await ListAsync(ho, code, "responsible", effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval))
                    .Items.Where(r => r.VisitRequestId == requestId));
            Assert.Equal("Chờ duyệt", hoMatch.StatusLabel);

            // HO / Đang chuẩn bị (BEFORE_VISIT) → correctly EXCLUDED: no campus of this request is at
            // that stage.
            Assert.DoesNotContain(
                (await ListAsync(ho, code, "responsible", effectiveStatus: VisitInstanceStatuses.BeforeVisit)).Items,
                r => r.VisitRequestId == requestId);

            // The registrant's OWN "registered" tab tracks the whole request the same way — both
            // statuses must match there too, not just on HO's monitoring view.
            await SetRegistrantAsync(requestId, Registrant);
            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);
            Assert.Contains(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── REQUEST_ANY_CAMPUS Case E (plan §9): request aggregate APPROVED, campuses at two different
    // live stages at once — the filter must catch the row under BOTH stages; the display badge stays
    // the single least-progressed-campus summary, unaffected. ──

    /// <summary>
    /// A fully-approved multi-campus request where one campus has moved on to BEFORE_VISIT and the
    /// other is still sitting at ASSIGNED. The summary badge shows the LEAST progressed campus
    /// ("Đã duyệt" — VisitRowLabels.MultiCampusProgress, unchanged, display-only); the FILTER must
    /// catch the row under BOTH EffectiveStatus=ASSIGNED and EffectiveStatus=BEFORE_VISIT — excluding
    /// BEFORE_VISIT would hide a campus the registrant/HO are genuinely tracking.
    /// </summary>
    [Fact]
    public async Task Multi_campus_approved_request_matches_every_live_campus_stage_it_actually_has()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("HCM", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var hcm = state["HCM"];

            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));
            await ApproveAsync(hcm.InstanceId, await RegularStaffAtAsync(hcm.CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);
            await SetCampusStatusAsync(hn.InstanceId, VisitInstanceStatuses.BeforeVisit); // HCM stays ASSIGNED

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);

            var assignedMatch = Assert.Single(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.Assigned))
                    .Items.Where(r => r.VisitRequestId == requestId));
            // Display badge is a separate question and stays the aggregate summary either way.
            Assert.Equal("Đã duyệt", assignedMatch.StatusLabel);

            Assert.Contains(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.BeforeVisit)).Items,
                r => r.VisitRequestId == requestId);

            Assert.DoesNotContain(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.DuringVisit)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── SINGLE_CAMPUS request-level rows: EffectiveCode is a straight passthrough. ──

    [Fact]
    public async Task Single_campus_request_level_row_is_a_straight_passthrough_of_its_own_campus_status()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);

            Assert.Contains(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.BeforeVisit)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── EffectiveStatuses (plural/union) — the OR-combination machinery itself. ──

    /// <summary>
    /// Two SEPARATE single-campus requests, one CANCELLED and one REJECTED. A single
    /// EffectiveStatuses="CANCELLED,REJECTED" query must return BOTH (proving the dynamic OR-of-
    /// predicates combinator produces one working SQL query, not just its first arm), and neither a
    /// request that matches only one of the two, nor an unrelated code, leaks in or out incorrectly.
    /// </summary>
    [Fact]
    public async Task EffectiveStatuses_union_matches_any_of_the_requested_codes()
    {
        RequireDb();
        ulong cancelledId = 0, rejectedId = 0;
        try
        {
            cancelledId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            rejectedId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HCM", Registrant));
            var cancelledCode = await RequestCodeAsync(cancelledId);
            var rejectedCode = await RequestCodeAsync(rejectedId);
            var cancelledHn = (await StateAsync(cancelledId))["HN"];
            var rejectedHcm = (await StateAsync(rejectedId))["HCM"];

            // A trigger refuses a pending campus instance being cancelled/rejected on its own — only
            // as a consequence of the parent request itself moving to that terminal status. Set the
            // request first, matching how the real cancel/reject commands order it.
            await SetRequestStatusAsync(cancelledId, VisitRequestStatuses.Cancelled);
            await SetCampusStatusAsync(cancelledHn.InstanceId, VisitInstanceStatuses.Cancelled);
            await SetRequestStatusAsync(rejectedId, VisitRequestStatuses.Rejected);
            await SetCampusStatusAsync(rejectedHcm.InstanceId, VisitInstanceStatuses.Rejected);

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);
            var union = $"{VisitInstanceStatuses.Cancelled},{VisitInstanceStatuses.Rejected}";

            var cancelledMatches = await ListAsync(registrant, cancelledCode, "registered", effectiveStatuses: union);
            Assert.Contains(cancelledMatches.Items, r => r.VisitRequestId == cancelledId);

            var rejectedMatches = await ListAsync(registrant, rejectedCode, "registered", effectiveStatuses: union);
            Assert.Contains(rejectedMatches.Items, r => r.VisitRequestId == rejectedId);

            // A code NOT in the union must not match either row.
            Assert.DoesNotContain(
                (await ListAsync(registrant, cancelledCode, "registered", effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == cancelledId);
        }
        finally { await CleanupAsync(cancelledId); await CleanupAsync(rejectedId); }
    }

    // ── tab=all: CloneForMerge regression + merge-then-filter ordering. ──

    /// <summary>
    /// CloneForMerge used to silently drop ApprovedAny/PendingApprovalAny/Timing when fetching merge
    /// sources for tab=all — those legacy quick filters were no-ops whenever combined with "all".
    ///
    /// <para>
    /// Uses a VISITOR caller deliberately: ApprovedAny/PendingApprovalAny are consumed ONLY inside
    /// <c>QueryRequestLevelAsync</c> — a Staff Leader's tab=all "responsible" source is instance-level
    /// (<c>QueryInstanceLevelAsync</c>), which never reads these params at all, before or after this
    /// fix, so it would be the wrong role to prove this regression with. Visitor's tab=all merges
    /// TWO request-level sources ("responsible" + "registered", both QueryRequestLevelAsync) — the
    /// combination these params were actually built for, and the only one HO's real dropdown options
    /// exist for (HO itself never reaches tab=all — see the role gate in Handle()).
    /// </para>
    /// </summary>
    [Fact]
    public async Task CloneForMerge_no_longer_drops_pending_approval_any_and_approved_any_on_tab_all()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);
            using var db = NewContext();
            var handler = new ViewGuestDelegationListQueryHandler(db, registrant, new FixedClock());

            var pendingOnly = await handler.Handle(
                new ViewGuestDelegationListQuery { Tab = "all", Page = 1, PageSize = 200, Keyword = code, PendingApprovalAny = true },
                CancellationToken.None);
            Assert.DoesNotContain(pendingOnly.Items, r => r.VisitRequestId == requestId);

            var approvedOnly = await handler.Handle(
                new ViewGuestDelegationListQuery { Tab = "all", Page = 1, PageSize = 200, Keyword = code, ApprovedAny = true },
                CancellationToken.None);
            Assert.Contains(approvedOnly.Items, r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// A Staff Leader who ALSO registered a multi-campus visit: their OWN campus (HN, where they lead)
    /// reaches ASSIGNED while the sibling campus is still undecided, so the request-level "registered"
    /// candidate's OWN reading (aggregate PARTIALLY_APPROVED) genuinely differs from the instance-level
    /// "responsible" candidate's reading (ASSIGNED, campus wins). CandidateRank picks the instance-level
    /// row as the merge winner (Host process is more urgent than a registrant tracking act here).
    ///
    /// <para>
    /// This proves BOTH halves of the merge-then-filter design at once: (1) filtering by the WINNING
    /// candidate's code (ASSIGNED) returns the row, filtering by the LOSING candidate's own would-be
    /// code (PARTIALLY_APPROVED) does not — the merge picks ONE authoritative code, not either
    /// candidate's in isolation; and (2) the returned row still carries BOTH relations AND the full
    /// multi-campus CampusProgressItems — proving the "registered" source was never excluded from the
    /// merge just because its own status reading didn't match the filter (the exact anti-pattern
    /// CloneForMerge's doc comment warns against: filtering a source pre-merge would have dropped this
    /// candidate before it had a chance to contribute its data).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Tab_all_filters_the_merged_rows_winning_code_without_losing_the_other_candidates_data()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("HCM", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            await SetRegistrantAsync(requestId, hn.LeaderId);
            // The Staff Leader is also named Host of their own campus — makes the instance-level
            // candidate's CandidateRank a HOST_PROCESS_REQUIRED tie-break winner over the registrant's
            // request-level candidate, which is exactly the shape this test needs.
            await ApproveAsync(hn.InstanceId, hn.LeaderId);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);

            // The winning (instance-level) candidate's own code — must match.
            var winnerMatch = Assert.Single(
                (await ListAsync(leader, code, "all", effectiveStatus: VisitInstanceStatuses.Assigned))
                    .Items.Where(r => r.VisitRequestId == requestId));
            Assert.Equal(VisitInstanceStatuses.Assigned, winnerMatch.EffectiveStatusCode);

            // Merge/data-preservation: the registrant relation and the full 2-campus accordion survived
            // the merge even though the filter targeted a code the LOSING candidate never had.
            Assert.Contains(VisitRowRelations.Registrant, RelationsOf(winnerMatch));
            Assert.Contains(VisitRowRelations.Host, RelationsOf(winnerMatch));
            Assert.Equal(2, winnerMatch.CampusProgressItems.Count);

            // REQUEST_ANY_CAMPUS on the merged row (plan §3.6/Case G): the sibling campus HCM, still
            // WAITING_REQUEST_APPROVAL, must ALSO match — the merged row carries the full 2-campus
            // accordion (asserted above), so it is included under every status any of ITS campuses
            // has, not just the one the winning (instance-level) candidate's own status.
            Assert.Contains(
                (await ListAsync(leader, code, "all", effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);

            // PARTIALLY_APPROVED is a request-AGGREGATE code, never a literal campus instance status —
            // it must NOT match either campus of the accordion (neither carries that string).
            Assert.DoesNotContain(
                (await ListAsync(leader, code, "all", effectiveStatus: EffectiveStatusCodes.PartiallyApproved)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Explicit acceptance-criteria matrix (strict semantics): "Đã duyệt" — the frontend's
    // effectiveStatuses="ASSIGNED,APPROVED" — must return ONLY rows whose OWN badge reads "Đã duyệt".
    // This is the exact literal repro: /dashboard/visit?tab=all&status=APPROVED must never return a
    // row badged "Đang chuẩn bị" (or any other stage) — the first pass at this fix left Visitor's
    // "Đã duyệt" as a broad lifecycle union (ASSIGNED,BEFORE_VISIT,DURING_VISIT,AFTER_VISIT,CLOSED,
    // APPROVED); this matrix pins the corrected, narrow mapping so it cannot regress back to that. ──

    public static IEnumerable<object[]> DaDuyetMatrix()
    {
        yield return new object[] { VisitInstanceStatuses.Assigned, true };
        yield return new object[] { VisitInstanceStatuses.BeforeVisit, false };
        yield return new object[] { VisitInstanceStatuses.DuringVisit, false };
        yield return new object[] { VisitInstanceStatuses.AfterVisit, false };
        yield return new object[] { VisitInstanceStatuses.Closed, false };
        yield return new object[] { VisitInstanceStatuses.Cancelled, false };
        yield return new object[] { VisitInstanceStatuses.Rejected, false };
    }

    /// <summary>
    /// Same matrix, minus REJECTED: <c>trg_visit_campuses_lifecycle_bu</c> only allows rejecting a
    /// still-PENDING campus, which means a rejected campus can never have a Host — so it can never
    /// enter regular Staff's host-gated "responsible" population in the first place. Testing it there
    /// would prove population exclusion, not filter exclusion; Visitor's matrix above (population is
    /// registrant-keyed, not host-gated) already covers REJECTED honestly.
    /// </summary>
    public static IEnumerable<object[]> DaDuyetMatrixHostGated()
    {
        foreach (var row in DaDuyetMatrix())
            if ((string)row[0] != VisitInstanceStatuses.Rejected) yield return row;
    }

    /// <summary>
    /// Walks an approved campus up the post-ASSIGNED lifecycle ladder ONE STEP PER STATEMENT —
    /// <c>trg_visit_campuses_lifecycle_bu</c> rejects a jump (BEFORE_VISIT only from ASSIGNED,
    /// DURING_VISIT only from BEFORE_VISIT, ...), and DURING_VISIT/AFTER_VISIT/CLOSED additionally
    /// require at least one agenda row (a delegation cannot be "being received" against an empty
    /// programme). Mirrors the proven pattern in MergeCrossBranchContractTests.AdvanceToAsync.
    /// </summary>
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

    /// <summary>
    /// Drives a fresh (WAITING_REQUEST_APPROVAL) single campus to the state under test, the same way
    /// the real product would reach it — never a direct status overwrite that skips a lifecycle guard:
    ///   - REJECTED: only a still-PENDING campus may be rejected — approving first would make the
    ///     later reject illegal, so this branch never calls ApproveAsync (no Host, by construction —
    ///     see DaDuyetMatrixHostGated's doc comment for what that means for a host-gated population).
    ///   - CANCELLED: an approved single campus is cancelled while the AGGREGATE stays APPROVED (a
    ///     CAMPUS_INSTANCE-level cancel, not a REQUEST-level one) — forcing vr.status to CANCELLED
    ///     first made the same trigger read it as an (invalid, non-pending) cascade-cancel attempt.
    ///   - BEFORE_VISIT/DURING_VISIT/AFTER_VISIT/CLOSED: walked one guarded step at a time.
    /// </summary>
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

    [Theory]
    [MemberData(nameof(DaDuyetMatrix))]
    public async Task Visitor_Da_duyet_filter_returns_only_rows_whose_badge_reads_Da_duyet(
        string campusStatus, bool expectedIncluded)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            await DriveToStatusAsync(requestId, hn.InstanceId, await RegularStaffAtAsync(hn.CampusId), campusStatus);

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);
            var result = await ListAsync(registrant, code, "registered", effectiveStatuses: "ASSIGNED,APPROVED");
            var included = result.Items.Any(r => r.VisitRequestId == requestId);
            Assert.True(included == expectedIncluded,
                $"campusStatus={campusStatus}: expected included={expectedIncluded}, got {included}");

            if (expectedIncluded)
                Assert.Equal("Đã duyệt", result.Items.Single(r => r.VisitRequestId == requestId).StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Theory]
    [MemberData(nameof(DaDuyetMatrixHostGated))]
    public async Task Staff_tab_all_Da_duyet_filter_returns_only_rows_whose_badge_reads_Da_duyet(
        string campusStatus, bool expectedIncluded)
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];
            var host = await RegularStaffAtAsync(hn.CampusId);
            // Host assignment ALWAYS happens first (see DriveToStatusAsync), so the row stays in
            // regular Staff's "responsible" (host-only) population for every case — only the status
            // filter itself decides inclusion, matching the real /dashboard/visit?tab=all&status=
            // APPROVED repro this test is pinned to.
            await DriveToStatusAsync(requestId, hn.InstanceId, host, campusStatus);

            var staff = new FakeUser(host, RoleCodes.Staff, UserSubRoles.Staff, hn.CampusId);
            var result = await ListAsync(staff, code, "all", effectiveStatuses: "ASSIGNED,APPROVED");
            var included = result.Items.Any(r => r.VisitRequestId == requestId);
            Assert.True(included == expectedIncluded,
                $"campusStatus={campusStatus}: expected included={expectedIncluded}, got {included}");

            if (expectedIncluded)
                Assert.Equal("Đã duyệt", result.Items.Single(r => r.VisitRequestId == requestId).StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The rare edge case the matrix above cannot exercise (needs multi-campus): a request whose
    /// aggregate is APPROVED but every campus has since been individually cancelled. The DISPLAY badge
    /// still reads "Đã duyệt" (EffectiveStatusCodes.ApprovedNoLiveCampus — MultiCampusProgress finds
    /// nothing live to rank, so the plain aggregate resolution ships, unaffected by this fix). The
    /// FILTER is a separate question under REQUEST_ANY_CAMPUS: neither campus is literally ASSIGNED,
    /// so "Đã duyệt" (mapped to the single campus code ASSIGNED, no aggregate union anymore — dropped
    /// 2026-08-23) correctly does NOT include it; it surfaces under "Đã hủy" instead, which is what
    /// its campuses actually are.
    /// </summary>
    [Fact]
    public async Task Visitor_approved_aggregate_with_no_live_campus_is_excluded_from_Da_duyet_but_found_under_cancelled()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("HCM", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);

            // Decide both campuses FIRST (ASSIGNED, no longer "pending") — a trigger refuses a still-
            // PENDING campus being cancelled except as a consequence of cancelling the pending main
            // request, which is not this shape (the aggregate stays APPROVED throughout). Both go to
            // CANCELLED rather than one Rejected: REJECTED is only legal from a still-pending campus
            // (a separate trigger), and "every campus terminal" is the same shape either way here.
            await ApproveAsync(state["HN"].InstanceId, await RegularStaffAtAsync(state["HN"].CampusId));
            await ApproveAsync(state["HCM"].InstanceId, await RegularStaffAtAsync(state["HCM"].CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            await SetCampusStatusAsync(state["HN"].InstanceId, VisitInstanceStatuses.Cancelled);
            await SetCampusStatusAsync(state["HCM"].InstanceId, VisitInstanceStatuses.Cancelled);

            var registrant = new FakeUser(Registrant, RoleCodes.Visitor);

            // "Đã duyệt" (ASSIGNED) → EXCLUDED: no campus of this request is literally ASSIGNED.
            Assert.DoesNotContain(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);

            // "Đã hủy" (CANCELLED) → INCLUDED: both campuses genuinely are.
            var row = Assert.Single(
                (await ListAsync(registrant, code, "registered", effectiveStatus: VisitInstanceStatuses.Cancelled))
                    .Items.Where(r => r.VisitRequestId == requestId));

            // Display badge is unaffected by the filter change — still the aggregate summary.
            Assert.Equal(EffectiveStatusCodes.ApprovedNoLiveCampus, row.EffectiveStatusCode);
            Assert.Equal("Đã duyệt", row.StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── REQUEST_ANY_CAMPUS Cases A/B/C (plan §9) + the CampusId+Status existential-mismatch guard
    // (plan §12) — new coverage added 2026-08-23, on top of the rewritten tests above. ──

    /// <summary>
    /// Case A: HN=ASSIGNED, DN=WAITING_REQUEST_APPROVAL. Full matrix for HO (ALL/ASSIGNED/WAITING in,
    /// BEFORE_VISIT out) AND for both campuses' own Staff Leaders (INSTANCE_EXACT — untouched by this
    /// fix): Leader HN sees only their own campus's own status, Leader DN likewise.
    /// </summary>
    [Fact]
    public async Task CaseA_mixed_approval_matrix_across_HO_and_both_campus_leaders()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];
            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId)); // DN stays WAITING_REQUEST_APPROVAL
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var ho = new FakeUser(999_201, RoleCodes.Ho);
            Assert.Contains((await ListAsync(ho, code)).Items, r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.BeforeVisit)).Items,
                r => r.VisitRequestId == requestId);

            var hnLeader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            Assert.Contains(
                (await ListAsync(hnLeader, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(hnLeader, code, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);

            var dnLeader = new FakeUser(dn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, dn.CampusId);
            Assert.Contains(
                (await ListAsync(dnLeader, code, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(dnLeader, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Case B: three campuses, each at a different LIVE stage (ASSIGNED / BEFORE_VISIT / DURING_VISIT).
    /// HO must find the request under all three of those filters and not under a stage none of the
    /// three campuses reached (AFTER_VISIT).
    /// </summary>
    [Fact]
    public async Task CaseB_three_campuses_at_three_different_live_stages_all_match_for_HO()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant), Campus("HCM", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];
            var hcm = state["HCM"];

            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));
            await ApproveAsync(dn.InstanceId, await RegularStaffAtAsync(dn.CampusId));
            await ApproveAsync(hcm.InstanceId, await RegularStaffAtAsync(hcm.CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);
            // HN stays ASSIGNED.
            await SetCampusStatusAsync(dn.InstanceId, VisitInstanceStatuses.BeforeVisit);
            await AdvanceToAsync(hcm.InstanceId, VisitInstanceStatuses.DuringVisit);

            var ho = new FakeUser(999_202, RoleCodes.Ho);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.BeforeVisit)).Items,
                r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.DuringVisit)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.AfterVisit)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Case C: HN=CLOSED, DN=CANCELLED — both terminal, neither ASSIGNED. HO must find the request
    /// under BOTH "Đã hoàn tất" and "Đã hủy", and not under "Đã duyệt".
    /// </summary>
    [Fact]
    public async Task CaseC_closed_and_cancelled_campuses_both_match_neither_assigned()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];

            await ApproveAsync(hn.InstanceId, await RegularStaffAtAsync(hn.CampusId));
            await ApproveAsync(dn.InstanceId, await RegularStaffAtAsync(dn.CampusId));
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);
            await AdvanceToAsync(hn.InstanceId, VisitInstanceStatuses.Closed);
            await SetCampusStatusAsync(dn.InstanceId, VisitInstanceStatuses.Cancelled);

            var ho = new FakeUser(999_203, RoleCodes.Ho);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Closed)).Items,
                r => r.VisitRequestId == requestId);
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Cancelled)).Items,
                r => r.VisitRequestId == requestId);
            Assert.DoesNotContain(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Plan §12's existential-mismatch guard: HN=WAITING_REQUEST_APPROVAL, DN=ASSIGNED. Combining
    /// CampusId with Status must require BOTH on the SAME campus instance — "Campus=HN + Status=
    /// ASSIGNED" must NOT true-positive just because DN (a different campus) happens to be ASSIGNED.
    /// </summary>
    [Fact]
    public async Task CampusId_plus_Status_must_match_the_same_campus_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];
            await ApproveAsync(dn.InstanceId, await RegularStaffAtAsync(dn.CampusId)); // HN stays WAITING_REQUEST_APPROVAL
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var ho = new FakeUser(999_204, RoleCodes.Ho);

            // Campus=HN + Status=ASSIGNED → EXCLUDED: HN itself is not ASSIGNED (DN is, but DN is a
            // different campus than the one selected).
            Assert.DoesNotContain(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned, campusId: hn.CampusId)).Items,
                r => r.VisitRequestId == requestId);

            // Campus=DN + Status=ASSIGNED → INCLUDED: same campus, both conditions genuinely true of it.
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.Assigned, campusId: dn.CampusId)).Items,
                r => r.VisitRequestId == requestId);

            // Campus=HN + Status=WAITING_REQUEST_APPROVAL → INCLUDED: same campus, both true of HN.
            Assert.Contains(
                (await ListAsync(ho, code, effectiveStatus: VisitInstanceStatuses.WaitingRequestApproval, campusId: hn.CampusId)).Items,
                r => r.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }
}
