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
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2;
using PEMS.Application.Delegations.Common;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The three interactions that only exist AFTER Dev is merged into Cảnh-Iter1 — each one needs a rule
/// from each side at the same time, so neither branch's own suite could have covered it.
///
/// <para>
/// Cảnh made campus rights follow <c>visit_request_campuses.operational_contact_user_id</c> instead of
/// <c>Role == VISITOR</c>, and made a single rejected campus resubmittable on its own. Dev rewrote the
/// status vocabulary into a role-aware one and repaired the process-detail relation, which had been
/// comparing against the retired <c>VISITOR_OWNER</c> string and was therefore dead code. Merged, the
/// same row is read through both rules at once: the reader is identified by Cảnh's rule and then
/// worded by Dev's.
/// </para>
/// <para>
/// The risk these tests exist for is silent, not loud: both branches merged without a single Git
/// conflict, so nothing forced anyone to look at the seam. A regression here would read as a
/// plausible label on a row the wrong person can act on.
/// </para>
/// </summary>
public sealed class MergeCrossBranchContractTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static bool? _dbUp;
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
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
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
    /// The contact email is the REGISTRANT'S own, so each campus self-matches at submit and the request
    /// starts past the confirmation gate. These tests are about what happens to a campus that is already
    /// live; a campus still behind that gate cannot be decided or moved at all.
    /// </summary>
    private static CampusVisitFormDto Campus(string code)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng", "+84912345678",
                V2SeedActor.Email(Registrant)),
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
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "XB" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, ulong LeaderId, string Status,
        ulong? DecidedBy, ulong? HostUserId);

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
                    c.DecidedBy, c.CurrentHostUserId)
            })
            .ToDictionaryAsync(x => x.CampusCode, x => x.Row);
    }

    private static async Task<string> RequestStatusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.Status).FirstAsync();
    }

    private static async Task<string> RequestCodeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
    }

    /// <summary>Makes <paramref name="userId"/> the confirmed operational contact of ONE campus.</summary>
    private static async Task BindContactAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1} WHERE visit_instance_id = {0}",
            instanceId, userId);
    }

    /// <summary>Approves a campus and names its Staff Leader as Host, leaving it on ASSIGNED.</summary>
    private static async Task ApproveAsync(ulong instanceId)
    {
        using var db = NewContext();
        var leaderId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CoordinatorUserId!.Value).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_source = 'STANDARD_CAMPUS_REVIEW', " +
            "current_host_user_id = {1}, host_assigned_by = {1}, host_assigned_at = {2} " +
            "WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30));
    }

    /// <summary>
    /// Walks an approved campus up the lifecycle ONE STEP PER STATEMENT.
    ///
    /// <para>
    /// <c>trg_visit_campuses_lifecycle_bu</c> rejects a jump — BEFORE_VISIT is only reachable from
    /// ASSIGNED and DURING_VISIT only from BEFORE_VISIT — so a fixture that set the target status
    /// directly failed in the database rather than in an assertion. The guard is the product rule these
    /// tests read a status label for; walking it is the honest way to reach the state.
    /// </para>
    /// </summary>
    private static async Task AdvanceToAsync(ulong instanceId, string target)
    {
        var ladder = new[]
        {
            VisitInstanceStatuses.BeforeVisit,
            VisitInstanceStatuses.DuringVisit,
            VisitInstanceStatuses.AfterVisit,
        };
        using var db = NewContext();
        foreach (var step in ladder)
        {
            // From DURING_VISIT on, the same trigger also demands the campus actually have an agenda —
            // a delegation cannot be "being received" against an empty programme. The Host builds this
            // during BEFORE_VISIT; one row is all the guard asks for.
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

    private static async Task RejectAsync(ulong instanceId, string note)
    {
        using var db = NewContext();
        var leaderId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CoordinatorUserId!.Value).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'REJECTED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_note = {3} WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30), note);
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

    /// <summary>The relation the PROCESS-DETAIL side resolves — the one Dev's repaired branch compares.</summary>
    private static async Task<string> ProcessRelationAsync(ulong instanceId, ulong actorId)
    {
        using var db = NewContext();
        var instance = await db.VisitRequestCampuses.AsNoTracking()
            .SingleAsync(c => c.VisitInstanceId == instanceId);
        var visit = await db.VisitRequests.AsNoTracking()
            .SingleAsync(v => v.VisitRequestId == instance.VisitRequestId);
        return await VisitInstanceAccess.ResolveRelationAsync(
            db, new FakeUser(actorId), instance, visit, CancellationToken.None);
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

    // ── MERGE-CROSS-01 ────────────────────────────────────────────────────────────

    /// <summary>
    /// A holds HN as its operational contact and HN has reached AFTER_VISIT.
    ///
    /// <para>
    /// Three answers have to agree at once, and each comes from a different branch. A is recognised
    /// through the campus row rather than through a role (Cảnh), the relation that recognition produces
    /// is the one Dev's repaired process-detail branch compares against, and the word A reads for
    /// AFTER_VISIT is Dev's Visitor-specific one. The last assertion is the one that would catch a
    /// regression quietly: staff reading the SAME campus must still get the internal wording, so the
    /// role-aware split cannot have collapsed into a single global label.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Contact_of_an_after_visit_campus_keeps_instance_rights_and_reads_the_visitor_wording()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var code = await RequestCodeAsync(requestId);
            var before = await StateAsync(requestId);
            var hn = before["HN"];

            var contactA = await OtherVisitorAsync();
            await BindContactAsync(hn.InstanceId, contactA);
            await ApproveAsync(hn.InstanceId);
            await AdvanceToAsync(hn.InstanceId, VisitInstanceStatuses.AfterVisit);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            // Dev: the process-detail relation is OPERATIONAL_CONTACT — and it is reached through
            // Cảnh's campus-row rule, for a user whose ROLE grants nothing here.
            Assert.Equal(VisitInstanceAccess.OperationalContact,
                await ProcessRelationAsync(hn.InstanceId, contactA));

            var forA = Assert.Single(await ListAsync(new FakeUser(contactA), code));

            // Cảnh: A still operates the campus they hold.
            Assert.Contains("VIEW_RECEPTION_DETAIL", forA.AllowedActions);

            // Dev: the Visitor's word for AFTER_VISIT is about the feedback they owe, not FPT's paperwork.
            Assert.Equal("Chờ đánh giá", forA.StatusLabel);

            // ...and the same campus read by the staff who close it keeps the internal word.
            var forLeader = Assert.Single(await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code));
            Assert.Equal("Chờ đóng", forLeader.StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── MERGE-CROSS-02 ────────────────────────────────────────────────────────────

    /// <summary>
    /// HN rejected, DN approved with a host, A holds HN. A resubmits HN alone.
    ///
    /// <para>
    /// Cảnh's half is that the campus moves and its sibling does not. Dev's half is the word everyone
    /// then reads for it. HO is asserted separately from the Staff Leader on purpose: HO is the one role
    /// for which Dev collapses PARTIALLY_APPROVED into "Chờ duyệt", so if the role argument stopped
    /// reaching <c>VisitRowLabels.Status</c> the HO row — and only the HO row — would silently revert to
    /// "Duyệt một phần".
    /// </para>
    /// </summary>
    [Fact]
    public async Task Resubmitting_one_rejected_campus_moves_only_it_and_reports_dev_vocabulary()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));
            var code = await RequestCodeAsync(requestId);
            var before = await StateAsync(requestId);

            await RejectAsync(before["HN"].InstanceId, "HN không thu xếp được");
            await ApproveAsync(before["DN"].InstanceId);

            var contactA = await OtherVisitorAsync();
            await BindContactAsync(before["HN"].InstanceId, contactA);

            var payload = await PayloadAsync(before["HN"].InstanceId);
            using (var db = NewContext())
            {
                var handler = new ResubmitRejectedVisitInstanceV2CommandHandler(
                    db, new FakeUser(contactA), new FixedClock(),
                    new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db)),
                    new NoopNotifications(),
                    NullLogger<ResubmitRejectedVisitInstanceV2CommandHandler>.Instance, WriteOn);
                var result = await handler.Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, result.VisitInstanceStatus);
            }

            var after = await StateAsync(requestId);

            // HN went back to review and shed its decision.
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after["HN"].Status);
            Assert.Null(after["HN"].DecidedBy);

            // DN did not move: same row, same status, same decider, same host.
            Assert.Equal(before["DN"].InstanceId, after["DN"].InstanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, after["DN"].Status);
            Assert.NotNull(after["DN"].DecidedBy);
            Assert.Equal(after["DN"].LeaderId, after["DN"].HostUserId);

            // The aggregate is re-derived from the campuses, not asserted by the handler.
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));

            // Dev's vocabulary on the campus that moved.
            var hn = after["HN"];
            var forLeader = Assert.Single(await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code));
            Assert.Equal("Chờ duyệt", forLeader.StatusLabel);

            // HO reads a partially-decided request as simply pending — their own canonical label.
            var forHo = Assert.Single(await ListAsync(new FakeUser(999_999, RoleCodes.Ho), code));
            Assert.Equal("Chờ duyệt", forHo.StatusLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>The payload that resubmits one campus: its current content with a fresh, legal schedule.</summary>
    private static async Task<CampusVisitEditV2Dto> PayloadAsync(ulong instanceId)
    {
        using var db = NewContext();
        var row = await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitInstanceId == instanceId
            select new { c.RowVersion, site.CampusCode, c.FormDetail }).FirstAsync();

        var d = row.FormDetail!;
        var from = Now.AddDays(30);
        return new CampusVisitEditV2Dto(
            instanceId, row.RowVersion, row.CampusCode, from, from.AddMinutes(120),
            d.DelegationName, d.VisitType, d.VisitTypeOther, d.Purpose, d.WorkingContent,
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto(
                d.OperationalContactFullName, d.OperationalContactOrganization!,
                d.OperationalContactJobTitle, d.OperationalContactPhone, d.OperationalContactEmail),
            d.WorkingLanguage, d.TransportationNote, d.MediaConsentStatus, d.Notes);
    }

    // ── MERGE-CROSS-03 ────────────────────────────────────────────────────────────

    /// <summary>
    /// The navigation a live campus offers is decided by which side of the visit the reader is on, and
    /// the two sets do not overlap.
    ///
    /// <para>
    /// The internal reader here is Host AND campus Staff Leader at once — the case Dev deliberately
    /// gives both internal entry points to — which makes it the strongest available proof that being
    /// internal never also yields the guest entry point. The contact is the mirror: they hold the campus
    /// under Cảnh's rule, and holding it still buys no internal screen. Relation leakage would show up
    /// here as an action crossing between the two lists.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Internal_actors_and_the_operational_contact_are_routed_to_different_screens()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var code = await RequestCodeAsync(requestId);
            var before = await StateAsync(requestId);
            var hn = before["HN"];

            var contactA = await OtherVisitorAsync();
            await BindContactAsync(hn.InstanceId, contactA);
            await ApproveAsync(hn.InstanceId);
            await AdvanceToAsync(hn.InstanceId, VisitInstanceStatuses.BeforeVisit);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);

            // Internal: Host of this campus, and its Staff Leader.
            var forInternal = Assert.Single(await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code));
            Assert.Contains("OPEN_HOST_PROCESS", forInternal.AllowedActions);
            Assert.Contains("OPEN_PROCESS_SUMMARY", forInternal.AllowedActions);
            Assert.DoesNotContain("VIEW_RECEPTION_DETAIL", forInternal.AllowedActions);
            Assert.Equal("HOST", forInternal.CurrentUserRelation);

            // Guest side: the confirmed operational contact of the same campus.
            var forContact = Assert.Single(await ListAsync(new FakeUser(contactA), code));
            Assert.Contains("VIEW_RECEPTION_DETAIL", forContact.AllowedActions);
            Assert.DoesNotContain("OPEN_HOST_PROCESS", forContact.AllowedActions);
            Assert.DoesNotContain("OPEN_PROCESS_SUMMARY", forContact.AllowedActions);

            // The process-detail side agrees about who is who, through Dev's repaired comparison.
            Assert.Equal(VisitInstanceAccess.OperationalContact,
                await ProcessRelationAsync(hn.InstanceId, contactA));
            Assert.Equal(VisitInstanceAccess.Host,
                await ProcessRelationAsync(hn.InstanceId, hn.LeaderId));
        }
        finally { await CleanupAsync(requestId); }
    }
}
