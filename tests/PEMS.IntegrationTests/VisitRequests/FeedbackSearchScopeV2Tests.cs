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
using PEMS.Application.Feedbacks.Queries.SearchAndFilterFeedback;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3D-2 — feedback search scope.
///
/// A non-HO/ADMIN actor is scoped to the feedback of their own campus's instances, and that scope is
/// applied to the base query before any keyword or filter. The keyword matches free-text snapshot fields
/// (submitter name, target name, comment), so a comment written on a sibling campus's feedback must move
/// neither the rows, the total, nor the page — and each row still titles from its own instance detail.
/// </summary>
public sealed class FeedbackSearchScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong IcStaffHn = 101;
    private const ulong IcStaffHcm = 103;
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
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "+8410", "op@example.com"),
            "VI", null, "DECLINED", null, null, null);

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant, RoleCodes.Visitor);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db));
        var form = new VisitRequestFormDataV2(
            "FB" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            new ContactPointDto("Registrant", "Org", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), new VisitRequestAggregateStatusService(db), new SilentNotifications(),
                new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task<ulong> AddFeedbackAsync(ulong requestId, ulong instanceId, string comment)
    {
        using var db = NewContext();
        var fb = new Feedback
        {
            VisitRequestId = requestId,
            VisitInstanceId = instanceId,
            FeedbackType = "VISITOR_OVERALL",
            SubmittedByUserId = Registrant,
            SubmitterRole = "VISITOR",
            SubmitterContext = "VISITOR",
            SubmitterNameSnapshot = "[IT] Người gửi",
            TargetType = "VISIT_INSTANCE",
            TargetContext = "VISIT_INSTANCE",
            TargetNameSnapshot = "[IT] Đối tượng",
            Rating = 5,
            Comment = comment,
            SubmittedAt = Now,
        };
        db.Feedbacks.Add(fb);
        await db.SaveChangesAsync();
        return fb.FeedbackId;
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM feedbacks WHERE visit_request_id = {0}");
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
    public async Task A_campus_actor_sees_only_their_own_campus_feedback_and_a_sibling_keyword_never_leaks()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(60);
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}"),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);

            await AddFeedbackAsync(requestId, instances[CampusHn], $"NhậnXétHN{tag}");
            await AddFeedbackAsync(requestId, instances[CampusHcm], $"NhậnXétHCM{tag}");

            using var db = NewContext();
            var handler = new SearchAndFilterFeedbackQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn));

            // The HN leader sees only the HN feedback in the unfiltered list.
            var all = await handler.Handle(new SearchAndFilterFeedbackQuery { Page = 1, PageSize = 200 }, CancellationToken.None);
            var mine = all.Items.Where(i => i.VisitRequestId == requestId).ToList();
            var row = Assert.Single(mine);
            Assert.Equal(instances[CampusHn], row.VisitInstanceId);
            Assert.Equal($"ĐoànHN{tag}", row.VisitTitle); // titled from its own instance detail

            // The HCM feedback's comment must not surface for the HN leader...
            var hidden = await handler.Handle(
                new SearchAndFilterFeedbackQuery { Q = $"NhậnXétHCM{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.DoesNotContain(hidden.Items, i => i.VisitRequestId == requestId);
            var nowhere = await handler.Handle(
                new SearchAndFilterFeedbackQuery { Q = $"zz{tag}nowhere", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Equal(nowhere.TotalItems, hidden.TotalItems); // scope applied before keyword → count unchanged

            // ...but the HN leader's own campus comment does.
            var own = await handler.Handle(
                new SearchAndFilterFeedbackQuery { Q = $"NhậnXétHN{tag}", Page = 1, PageSize = 200 }, CancellationToken.None);
            Assert.Single(own.Items, i => i.VisitRequestId == requestId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Each_campus_leader_reads_the_same_request_feedback_titled_by_their_own_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(61);
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}"),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);

            await AddFeedbackAsync(requestId, instances[CampusHn], $"Chung{tag}");
            await AddFeedbackAsync(requestId, instances[CampusHcm], $"Chung{tag}");

            async Task<(ulong? InstanceId, string Title)> RowFor(ulong leaderId, ulong campusId)
            {
                using var db = NewContext();
                var res = await new SearchAndFilterFeedbackQueryHandler(
                        db, new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId))
                    .Handle(new SearchAndFilterFeedbackQuery { Q = $"Chung{tag}", Page = 1, PageSize = 200 },
                        CancellationToken.None);
                var row = Assert.Single(res.Items, i => i.VisitRequestId == requestId);
                return (row.VisitInstanceId, row.VisitTitle);
            }

            var hn = await RowFor(LeaderHn, CampusHn);
            var hcm = await RowFor(LeaderHcm, CampusHcm);

            Assert.Equal(instances[CampusHn], hn.InstanceId);
            Assert.Equal($"ĐoànHN{tag}", hn.Title);
            Assert.Equal(instances[CampusHcm], hcm.InstanceId);
            Assert.Equal($"ĐoànHCM{tag}", hcm.Title);
        }
        finally { await CleanupAsync(requestId); }
    }
}
