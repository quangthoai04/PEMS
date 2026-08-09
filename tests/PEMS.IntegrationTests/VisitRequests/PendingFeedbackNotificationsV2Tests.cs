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
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Feedbacks.Common;
using PEMS.Application.Feedbacks.Queries.GetPendingFeedbackNotifications;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Owner-WIP slice — pending-feedback notifications resolve each instance's delegation name per campus.
///
/// The handler was refactored from a navigation-property projection (i.FormDetail.DelegationName) to an
/// explicit LEFT JOIN on visit_instance_form_details. This runs it on real MySQL/Pomelo and pins the
/// contract the refactor must preserve: each pending item shows THIS instance's own name (a mixed request
/// never borrows a sibling's), the actor sees only the instances they host or own, a missing detail
/// yields a null name rather than a crash, and the LEFT JOIN produces exactly one row per instance.
/// </summary>
public sealed class PendingFeedbackNotificationsV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 101;
    private const ulong HostHcm = 103;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;

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

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
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

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "PF" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        // Approving states the revision it was decided on. The command requires it, so a fixture
        // that left it out would be exercising a call shape no caller can make any more.
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), 
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null, rowVersion), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    /// <summary>The registrant — the request-level guest-side actor a feedback invitation reaches.</summary>
    private static async Task<ulong> VisitorOwnerIdAsync(ulong requestId)
    {
        using var db = NewContext();
        return (await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RegistrantUserId).SingleAsync())!.Value;
    }

    private static async Task MoveToAfterVisitAsync(ulong instanceId)
    {
        using var db = NewContext();
        db.VisitAgendas.Add(new VisitAgenda
        {
            VisitInstanceId = instanceId, Title = "[IT] Mục nghị trình",
            StartTime = Now.AddDays(-3), EndTime = Now.AddDays(-3).AddHours(1),
            SequenceOrder = 1, CreatedAt = Now, CreatedBy = LeaderHn,
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = {0}, planned_start_at = {1}, planned_end_at = {2} WHERE visit_instance_id = {3}",
            VisitInstanceStatuses.AfterVisit, Now.AddDays(-3), Now.AddDays(-3).AddHours(2), instanceId);
    }

    private static GetPendingFeedbackNotificationsQueryHandler Handler(ApplicationDbContext db, ulong actor, string role)
        => new(db, new FakeUser(actor, role, role == RoleCodes.Staff ? UserSubRoles.Staff : null, CampusHn));

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
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
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM audit_logs WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task Each_pending_item_shows_its_own_instance_name_and_the_actor_sees_only_what_they_hold()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(20);
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}"),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await MoveToAfterVisitAsync(instances[CampusHn]);
            await MoveToAfterVisitAsync(instances[CampusHcm]);

            // The HN host holds only the HN instance → exactly one item, named from HN's own detail, and
            // the sibling name never appears.
            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn, RoleCodes.Staff).Handle(
                    new GetPendingFeedbackNotificationsQuery(), CancellationToken.None);
                var mine = res.Items.Where(i => i.VisitRequestId == requestId).ToList();
                var item = Assert.Single(mine);
                Assert.Equal(instances[CampusHn], item.VisitInstanceId);
                Assert.Equal($"ĐoànHN{tag}", item.DelegationName);
                Assert.DoesNotContain(res.Items, i => i.DelegationName == $"ĐoànHCM{tag}");
                Assert.Equal(FeedbackSubmitterRoles.Host, item.ActorType);
            }

            // The visitor owner holds the whole request → both instances, each with ITS OWN name, one row
            // per instance (the LEFT JOIN does not duplicate), no sibling borrowing.
            var ownerId = await VisitorOwnerIdAsync(requestId);
            using (var db = NewContext())
            {
                var res = await Handler(db, ownerId, RoleCodes.Visitor).Handle(
                    new GetPendingFeedbackNotificationsQuery(), CancellationToken.None);
                var mine = res.Items.Where(i => i.VisitRequestId == requestId).ToList();
                Assert.Equal(2, mine.Count);
                Assert.Single(mine, i => i.VisitInstanceId == instances[CampusHn] && i.DelegationName == $"ĐoànHN{tag}");
                Assert.Single(mine, i => i.VisitInstanceId == instances[CampusHcm] && i.DelegationName == $"ĐoànHCM{tag}");
                // Exactly one row per instance — no duplication from the join.
                Assert.Equal(mine.Select(i => i.VisitInstanceId).Distinct().Count(), mine.Count);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_missing_form_detail_yields_a_null_name_rather_than_a_crash()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(21), "Đoàn thiếu detail"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await MoveToAfterVisitAsync(instances[CampusHn]);

            // Remove this instance's form detail so the LEFT JOIN has no match.
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM visit_instance_form_details WHERE visit_instance_id = {0}", instances[CampusHn]);

            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn, RoleCodes.Staff).Handle(
                    new GetPendingFeedbackNotificationsQuery(), CancellationToken.None);
                // The instance still surfaces (LEFT JOIN keeps it) with a null name — never a crash, and
                // never a sibling's name.
                var item = Assert.Single(res.Items.Where(i => i.VisitInstanceId == instances[CampusHn]));
                Assert.True(string.IsNullOrEmpty(item.DelegationName));
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
