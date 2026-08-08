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
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Commands.CancelVisitLogisticsItem;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Shared;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5E — cancelling a logistics item stays on its own campus instance.
///
/// A logistics item belongs to one instance and only that instance's Host may cancel it. The item lookup
/// is doubly scoped (item id AND instance id), and the host check is on that instance, so a Host of a
/// sibling campus of the same request can reach neither the item through their own instance nor the
/// instance through the sibling's item — and the sibling's item is never touched.
/// </summary>
public sealed class LogisticsCancelScopeV2Tests
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
            "LG" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), 
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                    new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null), CancellationToken.None);
    }

    /// <summary>
    /// The Host's own step: ASSIGNED → BEFORE_VISIT. Approving assigns the Host and stops; every setup
    /// mutation below refuses until the Host has actually started, so a fixture that only approves is
    /// describing a campus nobody has opened yet.
    /// </summary>
    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId, ulong hostId, ulong campusId)
    {
        using var db = NewContext();
        var actor = new FakeUser(hostId, RoleCodes.Staff, UserSubRoles.Staff, campusId);
        await new StartVisitPreparationCommandHandler(db, actor, new FixedClock())
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task<ulong> SeedRequestedItemAsync(ulong instanceId, ulong hostId, string title)
    {
        using var db = NewContext();
        var item = new VisitLogisticsItem
        {
            VisitInstanceId = instanceId,
            ItemType = "EQUIPMENT",
            Title = title,
            Status = LogisticsItemStatus.Requested,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedBy = hostId,
            RequestedAt = Now,
            RowVersion = 0,
            CreatedAt = Now,
            CreatedBy = hostId,
        };
        db.VisitLogisticsItems.Add(item);
        await db.SaveChangesAsync();
        return item.LogisticsItemId;
    }

    private static CancelVisitLogisticsItemCommandHandler Handler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_logistics_items WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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
    public async Task The_host_cancels_their_own_items_and_cannot_reach_a_sibling_campus_item()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await StartPreparationAsync(requestId, instances[CampusHcm], HostHcm, CampusHcm);

            var hnItem = await SeedRequestedItemAsync(instances[CampusHn], HostHn, "Máy chiếu HN");
            var hcmItem = await SeedRequestedItemAsync(instances[CampusHcm], HostHcm, "Máy chiếu HCM");

            // The HN host cancels their own item.
            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn).Handle(
                    new CancelVisitLogisticsItemCommand(instances[CampusHn], hnItem, "Không cần nữa"), CancellationToken.None);
                Assert.Equal(LogisticsItemStatus.Cancelled, res.Status);
            }
            using (var db = NewContext())
            {
                var item = await db.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == hnItem);
                Assert.Equal(LogisticsItemStatus.Cancelled, item.Status);
                Assert.Equal("Không cần nữa", item.DecisionNote);
                var audit = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.EntityType == "VisitLogisticsItem" && a.EntityId == hnItem).ToListAsync());
                Assert.Equal(CampusHn, audit.CampusId);
            }

            // Reaching the sibling item through the HN host's OWN instance fails: the item is not on that
            // instance (item-and-instance scoped lookup) → NotFound.
            using (var db = NewContext())
                await Assert.ThrowsAsync<NotFoundException>(() =>
                    Handler(db, HostHn).Handle(
                        new CancelVisitLogisticsItemCommand(instances[CampusHn], hcmItem, "Xâm phạm"), CancellationToken.None));

            // Reaching the sibling INSTANCE directly fails the host check: HN host is not HCM's host → Forbidden.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, HostHn).Handle(
                        new CancelVisitLogisticsItemCommand(instances[CampusHcm], hcmItem, "Xâm phạm"), CancellationToken.None));

            // The sibling's item is untouched.
            using (var db = NewContext())
            {
                var item = await db.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == hcmItem);
                Assert.Equal(LogisticsItemStatus.Requested, item.Status);
                Assert.Null(item.DecisionNote);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_missing_reason_is_refused_and_a_second_cancel_is_idempotent()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(21), "Đoàn HN"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            var hnItem = await SeedRequestedItemAsync(instances[CampusHn], HostHn, "Băng rôn");

            // A blank reason is refused before anything is written.
            using (var db = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Handler(db, HostHn).Handle(
                        new CancelVisitLogisticsItemCommand(instances[CampusHn], hnItem, "   "), CancellationToken.None));
            using (var db = NewContext())
                Assert.Equal(LogisticsItemStatus.Requested,
                    (await db.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == hnItem)).Status);

            using (var db = NewContext())
                await Handler(db, HostHn).Handle(
                    new CancelVisitLogisticsItemCommand(instances[CampusHn], hnItem, "Đổi kế hoạch"), CancellationToken.None);

            // Cancelling again is an idempotent no-op, not an error, and the reason from the first cancel stands.
            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn).Handle(
                    new CancelVisitLogisticsItemCommand(instances[CampusHn], hnItem, "Lý do khác"), CancellationToken.None);
                Assert.Equal(LogisticsItemStatus.Cancelled, res.Status);
            }
            using (var db = NewContext())
                Assert.Equal("Đổi kế hoạch",
                    (await db.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == hnItem)).DecisionNote);
        }
        finally { await CleanupAsync(requestId); }
    }
}
