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
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.ResubmitRejectedVisitInstanceV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Per-instance Resubmit (plan v9 §5, §9, §17).
///
/// <para>
/// The whole-request resubmit demands that EVERY campus be rejected and then resets all of them. That
/// is right when a request was refused outright and wrong when one campus said no while another said
/// yes — which is exactly what the operational contact of the refused campus is looking at, and what
/// they are confirmed to be allowed to act on.
/// </para>
/// <para>
/// Two properties carry this suite: authority comes from
/// <c>visit_request_campuses.operational_contact_user_id</c> and not from a role, and a campus going
/// back to review moves NOTHING on its siblings.
/// </para>
/// </summary>
public sealed class InstanceResubmitAuthorizationTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    /// <summary>STAFF / STAFF, IC, campus 1 — same canonical seed id used as IcStaffHn by
    /// PerCampusEditRelationAuthorizationTests, reused here for the short-notice resubmit cases.</summary>
    private const ulong InternalStaffRegistrant = 101;
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
        public FakeUser(ulong id, string roleCode = RoleCodes.Visitor, string? subRole = null)
        {
            UserId = id;
            RoleCode = roleCode;
            SubRole = subRole;
        }
        public ulong? UserId { get; }
        public string? Email => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? RoleId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
        public bool IsAuthenticated => true;
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

    private static ResubmitRejectedVisitInstanceV2CommandHandler Handler(ApplicationDbContext db, ulong actor)
        => Handler(db, new FakeUser(actor));

    private static ResubmitRejectedVisitInstanceV2CommandHandler Handler(ApplicationDbContext db, FakeUser actor)
        => new(db, actor, new FixedClock(),
            new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db)),
            new NoopNotifications(),
            NullLogger<ResubmitRejectedVisitInstanceV2CommandHandler>.Instance, WriteOn);

    // ── Fixtures ──────────────────────────────────────────────────────────────────

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

    private static Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses) => CreateAsync(Registrant, campuses);

    /// <summary>
    /// Same fixture, filed by an arbitrary registrant — used to seed a request an internal (Staff/Staff
    /// Leader) account owns, for the short-notice resubmit cases. The actor is direct-create authenticated
    /// as <paramref name="registrantUserId"/> itself (self-registration), matching how the real endpoint
    /// only ever grants short notice to a registrant editing/resubmitting their OWN request.
    /// </summary>
    private static async Task<ulong> CreateAsync(ulong registrantUserId, params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(registrantUserId), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "IR" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(registrantUserId)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    /// <summary>Drops sub-second precision, which <c>DATETIME</c> does not keep.</summary>
    private static DateTime TrimToSecond(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);

    /// <summary>The payload that resubmits one campus: SCHEDULE-ONLY (plan FIX-G/H) — its own row
    /// version, campus code and a fresh, legal start/end. No content, no members, no contact.</summary>
    private static async Task<InstanceResubmitScheduleDto> PayloadAsync(ulong instanceId, DateTime? start = null)
    {
        using var db = NewContext();
        var row = await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitInstanceId == instanceId
            select new { c.RowVersion, site.CampusCode }).FirstAsync();

        var from = start ?? Now.AddDays(30);
        return new InstanceResubmitScheduleDto(row.RowVersion, row.CampusCode, from, from.AddMinutes(120));
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, string Status, ulong? DecidedBy, string? DecisionNote);

    private static async Task<Dictionary<string, CampusRow>> StateAsync(ulong requestId)
    {
        using var db = NewContext();
        return await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitRequestId == requestId
            select new { site.CampusCode, Row = new CampusRow(c.VisitInstanceId, c.CampusId, c.Status, c.DecidedBy, c.DecisionNote) })
            .ToDictionaryAsync(x => x.CampusCode, x => x.Row);
    }

    private static async Task<string> RequestStatusAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.Status).FirstAsync();
    }

    /// <summary>Rejects one campus directly — the decision has its own suite; this one is about resubmit.</summary>
    private static async Task RejectAsync(ulong instanceId, string note)
    {
        using var db = NewContext();
        var campusId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CampusId).FirstAsync();
        var leaderId = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                        && u.Status == UserStatuses.Active && u.PrimaryCampusId == campusId)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'REJECTED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_note = {3} WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30), note);
    }

    /// <summary>Approves a campus and assigns its host — the sibling state that must survive untouched.</summary>
    private static async Task ApproveAsync(ulong instanceId)
    {
        using var db = NewContext();
        var campusId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CampusId).FirstAsync();
        var leaderId = await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Staff && u.SubRole == UserSubRoles.Leader
                        && u.Status == UserStatuses.Active && u.PrimaryCampusId == campusId)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', current_host_user_id = {1}, host_assigned_by = {1}, " +
            "host_assigned_at = {2} WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30));
    }

    /// <summary>Makes <paramref name="userId"/> the confirmed operational contact of one campus.</summary>
    private static async Task BindContactAsync(ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET operational_contact_user_id = {1} WHERE visit_instance_id = {0}",
            instanceId, userId);
    }

    /// <summary>
    /// Confirms the FIRST contact of a still-<c>WAITING_CONTACT_CONFIRMATION</c> campus, exactly the way
    /// <c>PerCampusEditRelationAuthorizationTests.ConfirmContactAsync</c> does: the DB triggers on that
    /// column (<c>WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT</c>,
    /// <c>CONTACT_CONFIRMATION_REQUIRED</c>) expect the full confirmed-contact shape — the timestamp and
    /// source alongside the id — and the matching invitation settled, not a bare id swap.
    /// </summary>
    private static async Task ConfirmInitialContactAsync(ulong requestId, ulong instanceId, ulong userId)
    {
        using var db = NewContext();
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceId);
        instance.OperationalContactUserId = userId;
        instance.OperationalContactConfirmedAt = Now;
        instance.OperationalContactConfirmationSource = OperationalContactSources.EmailConfirmation;
        instance.Status = VisitInstanceStatuses.WaitingRequestApproval;

        // Matches ConfirmContactAsync's own simplification: these fixtures never need the gate to read
        // correctly for an UNCONFIRMED sibling mid-setup, only for the trigger on THIS campus's own
        // columns to accept the write; by the time a test actually exercises the request, every campus
        // it cares about has been through this same call.
        var visit = await db.VisitRequests.SingleAsync(v => v.VisitRequestId == requestId);
        visit.Status = VisitRequestStatuses.PendingApproval;
        visit.ContactGateRevision += 1;

        var invitations = await db.VisitRequestIdentityChanges
            .Where(c => c.VisitRequestId == requestId && c.VisitInstanceId == instanceId
                        && c.Status == IdentityChangeStatuses.Pending)
            .ToListAsync();
        foreach (var invitation in invitations)
        {
            invitation.Status = IdentityChangeStatuses.Applied;
            invitation.NewUserId = userId;
            invitation.AppliedAt = Now;
        }

        await db.SaveChangesAsync();
    }

    private static async Task<ulong> OtherVisitorAsync()
    {
        using var db = NewContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
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

    // ── §5: the case the whole-request resubmit cannot express ────────────────────

    /// <summary>
    /// HN rejected, DN approved with a host. HN's own contact resubmits HN.
    ///
    /// <para>
    /// HN alone re-enters review and sheds the decision the database refuses to let it keep. DN keeps
    /// its status, its decider and its host, and the request lands on PARTIALLY_APPROVED — derived from
    /// the campuses, not named by the handler.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_contact_of_a_rejected_campus_resubmits_only_that_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));
            var before = await StateAsync(requestId);

            await RejectAsync(before["HN"].InstanceId, "HN không thu xếp được");
            await ApproveAsync(before["DN"].InstanceId);

            var contactA = await OtherVisitorAsync();
            await BindContactAsync(before["HN"].InstanceId, contactA);

            var payload = await PayloadAsync(before["HN"].InstanceId);
            using (var db = NewContext())
            {
                var result = await Handler(db, contactA).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, result.VisitInstanceStatus);
            }

            var after = await StateAsync(requestId);

            // HN is back in review with its decision cleared — the DB would refuse to hold it otherwise.
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after["HN"].Status);
            Assert.Null(after["HN"].DecidedBy);
            Assert.Null(after["HN"].DecisionNote);

            // DN moved not at all.
            Assert.Equal(before["DN"].InstanceId, after["DN"].InstanceId);
            Assert.Equal(VisitInstanceStatuses.Assigned, after["DN"].Status);
            Assert.NotNull(after["DN"].DecidedBy);

            // Derived, not assumed: one approved + one pending.
            Assert.Equal(VisitRequestStatuses.PartiallyApproved, await RequestStatusAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §9. A sibling campus's contact and an unrelated VISITOR are both refused. Holding one campus —
    /// or merely holding an account — is not authority over another campus.
    /// </summary>
    [Fact]
    public async Task A_sibling_contact_and_a_random_visitor_cannot_resubmit_this_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));
            var before = await StateAsync(requestId);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var contactB = await OtherVisitorAsync();
            await BindContactAsync(before["DN"].InstanceId, contactB);   // B holds DN, not HN

            var payload = await PayloadAsync(before["HN"].InstanceId);

            // The contact of the SIBLING campus.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, contactB).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None));

            // A VISITOR with no relation to this request at all.
            var stranger = await StrangerAsync(contactB);
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, stranger).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None));

            // HN is still rejected; nothing was let through.
            Assert.Equal(VisitInstanceStatuses.Rejected, (await StateAsync(requestId))["HN"].Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    private static async Task<ulong> StrangerAsync(ulong notThisOne)
    {
        using var db = NewContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant && u.UserId != notThisOne)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
    }

    /// <summary>§5. The registrant keeps the right too — the contact gains one, nobody loses one.</summary>
    [Fact]
    public async Task The_registrant_may_also_resubmit_one_rejected_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"), Campus("DN"));
            var before = await StateAsync(requestId);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var payload = await PayloadAsync(before["HN"].InstanceId);
            using (var db = NewContext())
                await Handler(db, Registrant).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None);

            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval,
                (await StateAsync(requestId))["HN"].Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §17. This IS a resubmit, so the 72-hour registration floor applies to the NEW start — measured
    /// from now, not from when the request was originally filed.
    /// </summary>
    [Fact]
    public async Task A_resubmitted_campus_starting_inside_72h_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var before = await StateAsync(requestId);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var tooSoon = await PayloadAsync(before["HN"].InstanceId, Now.AddHours(40));
            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() => Handler(db, Registrant).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, tooSoon),
                    CancellationToken.None));

            Assert.Equal(VisitInstanceStatuses.Rejected, (await StateAsync(requestId))["HN"].Status);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>§5. A campus that was never rejected cannot be "resubmitted" into review.</summary>
    [Fact]
    public async Task A_campus_that_is_not_rejected_cannot_be_resubmitted()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var before = await StateAsync(requestId);

            var payload = await PayloadAsync(before["HN"].InstanceId);
            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() => Handler(db, Registrant).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// §5. A stale row version loses. The instance's own version is the guard — deliberately not the
    /// request's, which a sibling being decided would bump.
    /// </summary>
    [Fact]
    public async Task A_stale_instance_row_version_is_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN"));
            var before = await StateAsync(requestId);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var payload = await PayloadAsync(before["HN"].InstanceId);
            var stale = payload with { ExpectedRowVersion = payload.ExpectedRowVersion - 1 };

            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() => Handler(db, Registrant).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, stale),
                    CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS: resubmit-instance is the LAST of the five
    //    mutation paths the plan extends short notice to. Before this change ApplyInstanceResubmitAsync
    //    had no lead-time exemption at all — every actor, internal or not, was held to the 72-hour floor
    //    on the resubmitted campus's new schedule. ─────────────────────────────────────────────────────

    /// <summary>
    /// An internal (Staff/Staff Leader) registrant resubmitting a rejected campus of THEIR OWN request
    /// may propose a new start inside the 72-hour floor — automatically, no confirmation dialog, the same
    /// capability Create/pending-edit already grant this pairing
    /// (<c>VisitMutationPolicy.IsShortNoticeEligible</c>).
    /// </summary>
    [Fact]
    public async Task An_internal_registrant_may_resubmit_a_rejected_campus_inside_the_floor()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(InternalStaffRegistrant, Campus("HN"));
            var before = await StateAsync(requestId);
            // HN's contact (Campus()'s fixed email) is NOT this registrant, so create leaves it
            // WAITING_CONTACT_CONFIRMATION with no contact bound yet — confirm one directly (as the real
            // confirmation flow would) before the reject trigger will accept the campus.
            await ConfirmInitialContactAsync(requestId, before["HN"].InstanceId, Registrant);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var tooSoon = TrimToSecond(Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-30));
            var payload = await PayloadAsync(before["HN"].InstanceId, tooSoon);

            using (var db = NewContext())
            {
                var result = await Handler(db, new FakeUser(InternalStaffRegistrant, RoleCodes.Staff, UserSubRoles.Staff))
                    .Handle(new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                        CancellationToken.None);
                Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, result.VisitInstanceStatus);
            }

            var after = await StateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.WaitingRequestApproval, after["HN"].Status);
            using var check = NewContext();
            var instance = await check.VisitRequestCampuses.AsNoTracking()
                .SingleAsync(c => c.VisitInstanceId == before["HN"].InstanceId);
            Assert.Equal(tooSoon, instance.PlannedStartAt);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// A non-internal (VISITOR) registrant resubmitting their own rejected campus keeps the 72-hour floor
    /// exactly as before — short notice is Staff/Staff Leader only, whatever else the actor may be to the
    /// request.
    /// </summary>
    [Fact]
    public async Task A_visitor_registrant_resubmitting_inside_the_floor_is_still_refused()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN")); // filed by Registrant (VISITOR seed)
            var before = await StateAsync(requestId);
            await RejectAsync(before["HN"].InstanceId, "HN từ chối");

            var tooSoon = TrimToSecond(Now.AddHours(VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(-30));
            var payload = await PayloadAsync(before["HN"].InstanceId, tooSoon);

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Handler(db, Registrant).Handle(
                    new ResubmitRejectedVisitInstanceV2Command(requestId, before["HN"].InstanceId, payload),
                    CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            }

            var after = await StateAsync(requestId);
            Assert.Equal(VisitInstanceStatuses.Rejected, after["HN"].Status); // untouched — the refusal rolled back
        }
        finally { await CleanupAsync(requestId); }
    }

    // Short notice never widens WHO may resubmit — only how soon they may schedule it. That relation-only
    // refusal (a sibling campus's contact, or a stranger, hold no authority over THIS campus) is already
    // proven role-independently by A_sibling_contact_and_a_random_visitor_cannot_resubmit_this_campus
    // above, and this change does not touch VisitRequestOwnership.IsGuestSide at all — allowShortNotice is
    // gated behind IsRegistrant, a strict SUBSET of what that guard already requires.
}
