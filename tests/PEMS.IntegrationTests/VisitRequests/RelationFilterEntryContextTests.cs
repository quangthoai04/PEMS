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
/// The three questions the management list has to keep apart, and the ways they used to bleed into
/// each other:
///
/// <list type="number">
///   <item><b>FILTER</b> — why a row is on screen. It is a POPULATION. "Tôi là người đăng ký" used to
///     also subtract "…and am not the contact", so a person who registered a visit and then confirmed
///     as its contact vanished from it.</item>
///   <item><b>AUTHORIZATION</b> — what the caller may do, from the relations they genuinely hold. The
///     registered and attending tabs used to return read-only before looking at anything, so switching
///     filter took rights away from somebody whose data had not changed at all.</item>
///   <item><b>ENTRY CONTEXT</b> — which screen the row opens. This is the ONLY one of the three a
///     filter is allowed to change.</item>
/// </list>
///
/// <para>
/// Every test here is written against a caller who holds MORE THAN ONE relation, because that is the
/// only shape in which the three can be told apart: with one relation, a tab-derived answer and a
/// data-derived answer look identical, which is why the bug survived so long.
/// </para>
/// <para>
/// Campus scope for internal staff is deliberately narrow: one account works at one campus, so HOST and
/// CAMPUS_REVIEWER are only ever recognised at that account's own campus (see
/// <see cref="Staff_relations_are_confined_to_their_own_campus"/>).
/// </para>
/// </summary>
public sealed class RelationFilterEntryContextTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    /// <summary>The seeded Visitor every fixture registers as unless a test names somebody else.</summary>
    private const ulong Registrant = 8;

    private static bool? _dbUp;
    /// <summary>
    /// Why the database was unusable. Reported verbatim: the canonical import fails for very specific
    /// reasons (a stale table/trigger count, a retarget refusal), and collapsing all of them into
    /// "not reachable" sends the reader looking for a server that is running perfectly well.
    /// </summary>
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

    // ── Fixtures ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A campus whose contact email is the REGISTRANT'S own, so it self-matches at submit: the request
    /// clears the confirmation gate and the registrant is ALSO the confirmed operational contact. That
    /// double relation is the subject of most of this file.
    /// </summary>
    private static CampusVisitFormDto Campus(string code, ulong actor)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng", "+84912345678",
                V2SeedActor.Email(actor)),
            "EN", null, "DECLINED", null, null);
    }

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
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "XB" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(actor)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, ulong LeaderId, string Status,
        ulong? HostUserId, ulong? ContactUserId);

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
                Row = new CampusRow(c.VisitInstanceId, c.CampusId, c.CoordinatorUserId!.Value, c.Status,
                    c.CurrentHostUserId, c.OperationalContactUserId)
            })
            .ToDictionaryAsync(x => x.CampusCode, x => x.Row);
    }

    private static async Task<string> RequestCodeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
    }

    private static async Task BindContactAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1} WHERE visit_instance_id = {0}",
            instanceId, userId);
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

    private static async Task SetRequestStatusAsync(ulong requestId, string status)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_requests SET status = {1} WHERE visit_request_id = {0}", requestId, status);
    }

    private static async Task<ulong> OtherVisitorAsync()
    {
        using var db = NewContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
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

    // ── V1 / V2 — a single relation each. The control group. ──────────────────────

    /// <summary>
    /// V1 + V2 from one request, because they are two halves of the same claim: each filter answers
    /// its own question and nothing else. The registrant is only on "registered", the contact only on
    /// "responsible", and "all" shows each of them one row.
    /// </summary>
    [Fact]
    public async Task Single_relation_callers_appear_on_their_own_filter_only()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            // Hand the campus to somebody else, which leaves the registrant registrant-ONLY.
            var contactOnly = await OtherVisitorAsync();
            await BindContactAsync(hn.InstanceId, contactOnly);

            var registrant = new FakeUser(Registrant);
            var contact = new FakeUser(contactOnly);

            // V1 — registrant only.
            Assert.Single(await ListAsync(registrant, code, "registered"));
            Assert.Empty(await ListAsync(registrant, code, "responsible"));
            var v1All = Assert.Single(await ListAsync(registrant, code, "all"));
            Assert.Equal(new[] { VisitRowRelations.Registrant }, RelationsOf(v1All));

            // V2 — contact only. Holding a campus buys no request-level act.
            Assert.Empty(await ListAsync(contact, code, "registered"));
            Assert.Single(await ListAsync(contact, code, "responsible"));
            var v2All = Assert.Single(await ListAsync(contact, code, "all"));
            Assert.Equal(new[] { VisitRowRelations.OperationalContact }, RelationsOf(v2All));
            Assert.DoesNotContain("EDIT_PENDING_REQUEST", v2All.AllowedActions);
            Assert.DoesNotContain("RESUBMIT_REJECTED_REQUEST", v2All.AllowedActions);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── V3 — the regression this whole change exists for. ─────────────────────────

    /// <summary>
    /// V3. The registrant confirmed as their own campus's contact, so they hold both relations.
    ///
    /// <para>
    /// Before, "Tôi là người đăng ký" subtracted anyone who was also the contact: the row simply was
    /// not there, and the person was told — by a filter — that they had not registered their own visit.
    /// Both filters must now find it, and (the part that matters) the registrant's own EDIT must be
    /// offered on BOTH, because a filter cannot take a right away.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Registrant_who_is_also_the_contact_keeps_both_filters_and_both_sets_of_rights()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);

            // The self-matched contact IS the registrant — the exact overlap the old filter erased.
            var hn = (await StateAsync(requestId))["HN"];
            Assert.Equal(Registrant, hn.ContactUserId);

            var me = new FakeUser(Registrant);
            var registered = Assert.Single(await ListAsync(me, code, "registered"));
            var responsible = Assert.Single(await ListAsync(me, code, "responsible"));
            var all = Assert.Single(await ListAsync(me, code, "all"));

            // Both relations are reported wherever the row is read from.
            foreach (var row in new[] { registered, responsible, all })
            {
                Assert.Contains(VisitRowRelations.Registrant, RelationsOf(row));
                Assert.Contains(VisitRowRelations.OperationalContact, RelationsOf(row));
            }

            // The registrant's request-level right survives the contact relation AND the filter.
            Assert.Contains("EDIT_PENDING_REQUEST", registered.AllowedActions);
            Assert.Contains("EDIT_PENDING_REQUEST", responsible.AllowedActions);
            Assert.Contains("EDIT_PENDING_REQUEST", all.AllowedActions);
            Assert.False(registered.IsReadOnly);

            // INVARIANT — a filter changes the entry context and nothing else.
            Assert.Equal(
                responsible.AllowedActions.OrderBy(a => a),
                registered.AllowedActions.OrderBy(a => a));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── S3 / SL2 — registrant + host. ─────────────────────────────────────────────

    /// <summary>
    /// S3. A regular Staff registered the visit and then received its campus as Host.
    ///
    /// <para>
    /// Two filters, two entry contexts, one set of rights. "Đơn tôi đăng ký" opens the request;
    /// "Đơn/Đoàn tôi phụ trách" opens the host process for the campus — and the host actions are
    /// present on the registered row too, which they were not before: the tab returned read-only
    /// before it looked at who was asking.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Staff_who_registered_and_now_hosts_keeps_both_relations_on_both_filters()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using (var probe = NewContext())
            {
                // Pick the campus first so the staff account and the campus agree.
                _ = probe;
            }

            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            var staff = await RegularStaffAtAsync(hn.CampusId);
            // Make that Staff the registrant of this request, and its Host.
            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}",
                    requestId, staff);
            }
            await ApproveAsync(hn.InstanceId, staff);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            var caller = new FakeUser(staff, RoleCodes.Staff, UserSubRoles.Staff, hn.CampusId);

            var registered = Assert.Single(await ListAsync(caller, code, "registered"));
            var hosted = Assert.Single(await ListAsync(caller, code, "hosted"));
            var all = Assert.Single(await ListAsync(caller, code, "all"));

            foreach (var row in new[] { registered, hosted, all })
            {
                Assert.Contains(VisitRowRelations.Registrant, RelationsOf(row));
                Assert.Contains(VisitRowRelations.Host, RelationsOf(row));
                // Host rights travel with the relation, not with the tab.
                Assert.Contains("OPEN_HOST_PROCESS", row.AllowedActions);
            }

            // ENTRY CONTEXT is the one thing the filter decides.
            Assert.Equal(VisitEntryContexts.RequestDetail, registered.PrimaryEntryContext);
            Assert.Equal(VisitEntryContexts.HostProcess, hosted.PrimaryEntryContext);
            Assert.Equal(hn.InstanceId, hosted.PrimaryEntryVisitInstanceId);
            // …and on "all" the most urgent relation wins: there is a campus to run.
            Assert.Equal(VisitEntryContexts.HostProcess, all.PrimaryEntryContext);

            // INVARIANT — same rights under both filters.
            Assert.Equal(hosted.AllowedActions.OrderBy(a => a), registered.AllowedActions.OrderBy(a => a));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── SL1 — registrant + campus reviewer, decision pending. ─────────────────────

    /// <summary>
    /// SL1. The campus Staff Leader also registered this visit, and their campus is waiting on their
    /// decision.
    ///
    /// <para>
    /// One row on "all", carrying both relations; the campus decision is the primary entry because it
    /// is the most urgent thing they owe; and the registrant half is NOT lost to the merge, which is
    /// exactly what the old first-wins dedupe destroyed. Self-overlap is allowed on purpose — banning
    /// self-approval would be a business rule with its own tests, not a side effect of a list.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Staff_leader_who_registered_their_own_campus_visit_keeps_review_and_registrant_in_one_row()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}",
                    requestId, hn.LeaderId);
            }

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);

            var registered = Assert.Single(await ListAsync(leader, code, "registered"));
            var responsible = Assert.Single(await ListAsync(leader, code, "responsible"));
            var all = Assert.Single(await ListAsync(leader, code, "all"));

            // ONE row on "all", and it kept everything.
            Assert.Contains(VisitRowRelations.Registrant, RelationsOf(all));
            Assert.Contains(VisitRowRelations.CampusReviewer, RelationsOf(all));

            // The decision they owe is the primary task…
            Assert.Equal(VisitEntryContexts.CampusReview, all.PrimaryEntryContext);
            Assert.Contains("APPROVE_AND_ASSIGN_HOST", all.AllowedActions);
            Assert.Contains("CAMPUS_REJECT", all.AllowedActions);
            // …and the registrant half is still there as a secondary act.
            Assert.Contains("EDIT_PENDING_REQUEST", all.AllowedActions);

            // The campus decision is not something "Đơn tôi đăng ký" may take away.
            Assert.Contains("APPROVE_AND_ASSIGN_HOST", registered.AllowedActions);
            Assert.Contains("EDIT_PENDING_REQUEST", responsible.AllowedActions);

            // INVARIANT — identical rights, different entry.
            Assert.Equal(responsible.AllowedActions.OrderBy(a => a), registered.AllowedActions.OrderBy(a => a));
            Assert.Equal(VisitEntryContexts.RequestDetail, registered.PrimaryEntryContext);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── SL4 — reviewer without the registrant relation. ───────────────────────────

    /// <summary>
    /// SL4, the mirror of SL1: unioning relations must not leak the other way. A Staff Leader who did
    /// NOT register the visit reviews it and gets nothing request-level.
    /// </summary>
    [Fact]
    public async Task Campus_reviewer_who_did_not_register_gets_no_request_level_rights()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var row = Assert.Single(await ListAsync(leader, code, "responsible"));

            Assert.Contains(VisitRowRelations.CampusReviewer, RelationsOf(row));
            Assert.DoesNotContain(VisitRowRelations.Registrant, RelationsOf(row));
            Assert.Contains("APPROVE_AND_ASSIGN_HOST", row.AllowedActions);
            Assert.DoesNotContain("EDIT_PENDING_REQUEST", row.AllowedActions);
            Assert.DoesNotContain("RESUBMIT_REJECTED_REQUEST", row.AllowedActions);

            // Registering somebody else's request is not a thing this filter can invent either.
            Assert.Empty(await ListAsync(leader, code, "registered"));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── SL5 / campus-scope acceptance. ────────────────────────────────────────────

    /// <summary>
    /// One internal account works at ONE campus, so its campus-scoped relations are recognised there
    /// and nowhere else. Caller's own campus is HN, on a request that also has a DN campus:
    /// HOST(HN) ✅, CAMPUS_REVIEWER(HN) ✅, HOST(DN) ❌, CAMPUS_REVIEWER(DN) ❌.
    ///
    /// <para>
    /// The caller here is the REGISTRANT of the request, which is what makes the test possible at all:
    /// only a request-level row enumerates every campus, so only there can the list be caught granting
    /// something at a campus the account has no business at. An instance row for DN would never be
    /// returned to an HN account in the first place, so asserting on one would prove nothing.
    /// </para>
    /// <para>
    /// The stronger illegal case — the same account named Host of both campuses — cannot be built:
    /// <c>trg_visit_campuses_*</c> refuses "current_host_user_id must be IC Staff of same campus or the
    /// approving Staff Leader themself". The rule is enforced in the schema, so the list only has to
    /// not invent a relation the data never claimed.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Staff_relations_are_confined_to_their_own_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var before = await StateAsync(requestId);
            var hn = before["HN"];
            var dn = before["DN"];

            // Each campus is approved by, and handed to, its OWN Staff Leader — the only host the
            // schema will accept here.
            await ApproveAsync(hn.InstanceId, hn.LeaderId);
            await ApproveAsync(dn.InstanceId, dn.LeaderId);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            // HN's leader also registered the request, so their one row covers BOTH campuses.
            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}",
                    requestId, hn.LeaderId);
            }

            var caller = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);
            var row = Assert.Single(await ListAsync(caller, code, "registered"));

            // The row really does cover DN — otherwise the negatives below would be vacuous.
            Assert.Equal(2, row.CampusProgressItems.Count);
            Assert.Contains(row.CampusProgressItems, c => c.CampusId == dn.CampusId);

            var hostCampuses = row.RelationContexts
                .Where(c => c.Relation == VisitRowRelations.Host).Select(c => c.CampusId).ToList();
            var reviewerCampuses = row.RelationContexts
                .Where(c => c.Relation == VisitRowRelations.CampusReviewer).Select(c => c.CampusId).ToList();

            Assert.Contains((ulong?)hn.CampusId, hostCampuses);          // HOST(HN) ✅
            Assert.DoesNotContain((ulong?)dn.CampusId, hostCampuses);    // HOST(DN) ❌
            Assert.Contains((ulong?)hn.CampusId, reviewerCampuses);      // CAMPUS_REVIEWER(HN) ✅
            Assert.DoesNotContain((ulong?)dn.CampusId, reviewerCampuses);// CAMPUS_REVIEWER(DN) ❌

            // And on their own campus list, an HN leader is only ever shown HN.
            var leaderRows = await ListAsync(caller, code, "responsible");
            Assert.All(leaderRows, r => Assert.Equal(hn.CampusId, r.CampusId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §15.4 Participant overlap. ────────────────────────────────────────────────

    /// <summary>
    /// The campus Staff Leader was also invited to the visit as a supporting participant, so the row
    /// reaches them through the invitation filter as well.
    ///
    /// <para>
    /// "Đơn mời tham dự" used to return read-only before looking at anything, which meant an
    /// invitation could take a campus decision away from the person who owed it. The invitation is a
    /// population; the decision belongs to the campus they lead, and being invited is not a reason to
    /// lose it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_invitation_does_not_take_away_the_campus_decision_the_reader_still_owes()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var code = await RequestCodeAsync(requestId);
            var hn = (await StateAsync(requestId))["HN"];

            // Somebody else invites the HN leader to support the visit.
            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO visit_participants (visit_instance_id, user_id, participant_role, is_host, status, invited_by, invited_at) " +
                    "VALUES ({0}, {1}, 'IC_SUPPORT', 0, 'INVITED', {2}, {3})",
                    hn.InstanceId, hn.LeaderId, Registrant, Now.AddMinutes(-10));
            }

            var leader = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);

            var attending = Assert.Single(await ListAsync(leader, code, "attending"));
            var responsible = Assert.Single(await ListAsync(leader, code, "responsible"));

            // Both relations are on the row, whichever filter produced it.
            Assert.Contains(VisitRowRelations.Participant, RelationsOf(attending));
            Assert.Contains(VisitRowRelations.CampusReviewer, RelationsOf(attending));

            // The decision survives the invitation filter — this is the assertion that used to fail.
            Assert.Contains("APPROVE_AND_ASSIGN_HOST", attending.AllowedActions);
            Assert.False(attending.IsReadOnly);

            // INVARIANT — the filter moved the entry context, not the rights.
            Assert.Equal(responsible.AllowedActions.OrderBy(a => a), attending.AllowedActions.OrderBy(a => a));

            // One row on "all", carrying both, and the decision is what it opens.
            var all = Assert.Single(await ListAsync(leader, code, "all"));
            Assert.Contains(VisitRowRelations.Participant, RelationsOf(all));
            Assert.Contains(VisitRowRelations.CampusReviewer, RelationsOf(all));
            Assert.Equal(VisitEntryContexts.CampusReview, all.PrimaryEntryContext);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── "All" merge — one row per request, nothing dropped. ───────────────────────

    /// <summary>
    /// The merge contract itself. A Visitor who registered a request AND holds its campus arrives at
    /// the merge from two different sources; the old code kept whichever came first and threw the other
    /// away. Exactly one row must come out, and it must carry both sources' knowledge — including the
    /// per-campus accordion, which only the request-level source produces.
    /// </summary>
    [Fact]
    public async Task All_merges_a_request_into_one_row_without_losing_a_source()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant));
            var code = await RequestCodeAsync(requestId);

            var me = new FakeUser(Registrant);
            var all = await ListAsync(me, code, "all");

            var row = Assert.Single(all);
            Assert.Equal(requestId, row.VisitRequestId);
            Assert.Equal(2, row.CampusProgressItems.Count);
            Assert.True(row.CanExpandCampuses);

            // Both relations survived the merge, on both campuses.
            Assert.Contains(VisitRowRelations.Registrant, RelationsOf(row));
            Assert.Equal(2, row.RelationContexts.Count(c => c.Relation == VisitRowRelations.OperationalContact));

            // A multi-campus summary row opens the request — it is not one campus, so it cannot open one.
            Assert.Equal(VisitEntryContexts.RequestDetail, row.PrimaryEntryContext);
            Assert.Null(row.PrimaryEntryVisitInstanceId);
        }
        finally { await CleanupAsync(requestId); }
    }
}
