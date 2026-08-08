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
        public FakeUser(ulong id) => UserId = id;
        public ulong? UserId { get; }
        public string? Email => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
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
        => new(db, new FakeUser(actor), new FixedClock(),
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
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "IR" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    /// <summary>The payload that resubmits one campus: its current content with a fresh, legal schedule.</summary>
    private static async Task<CampusVisitEditV2Dto> PayloadAsync(ulong instanceId, DateTime? start = null)
    {
        using var db = NewContext();
        var row = await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitInstanceId == instanceId
            select new { c.RowVersion, site.CampusCode, c.FormDetail }).FirstAsync();

        var d = row.FormDetail!;
        var from = start ?? Now.AddDays(30);
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
}
