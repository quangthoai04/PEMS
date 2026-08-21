using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.RespondVisitParticipantInvitation;
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.DepartmentReceptionTasks.Commands.AcceptAssignedLogisticsTask;
using PEMS.Application.DepartmentReceptionTasks.Commands.DeclineAssignedLogisticsTask;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Parent-lifecycle races (final plan correction): a campus cancel (<c>CancelVisitRequestCommandHandler</c>)
/// and a participant/logistics response share the SAME lock hierarchy (VisitRequest → VisitRequestCampus →
/// business target), so the two must serialize instead of each side deciding from its own pre-cancel
/// snapshot. Unlike the mutually-exclusive response-vs-response races
/// (<see cref="EmailActions.ParticipationAssignmentResponseConcurrencyTests"/>), these are NOT
/// mutually-exclusive: whichever side wins the VisitRequestCampus row lock first legitimately commits, and
/// if the response wins first, the cancel proceeding afterward is not corruption. What these tests prove is
/// the one invariant the plan calls out explicitly — a response can never commit against a STALE pre-cancel
/// read — by forcing the cancel to win the lock first and proving the response then blocks, wakes, re-reads
/// the now-cancelled lifecycle, and refuses with zero mutation.
///
/// <para>
/// The "cancel" side here is a hand-rolled lock+mutate matching <c>CancelVisitRequestCommandHandler</c>'s
/// per-campus field set (Status/CancelledBy/CancelledAt/CancellationActorType/CancellationSource/
/// CancellationReason + the logistics cascade), not a call into the handler itself — the same "fake holder"
/// technique <c>ParticipationAssignmentResponseConcurrencyTests</c> uses to get a deterministic, provable
/// hold on the lock. The handler's OWN correctness (authorization, window rules, notifications) is already
/// covered by <see cref="CancelAndInvitationResponseV2Tests"/> and <see cref="LogisticsCancelScopeV2Tests"/>;
/// this suite's only job is proving the lock contract holds against a genuine concurrent responder.
/// </para>
/// </summary>
public sealed class ParentLifecycleCancelConcurrencyTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong IcStaffHn = 101;   // campus host
    private const ulong IcStaffHn2 = 102;  // invitee / logistics assignee
    private const ulong CampusHn = 1;

    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(600);

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
        public Task CreateManyAsync(System.Collections.Generic.IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(System.Collections.Generic.IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong u, string t, string? m, string n, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new System.Collections.Generic.List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new System.Collections.Generic.List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(CampusVisitFormDto campus)
    {
        using var db = NewContext();
        var actor = new FakeUser(Registrant);
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "PLC" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, new System.Collections.Generic.List<CampusVisitFormDto> { campus });
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    private static async Task<ulong> InstanceIdAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId)
    {
        using var db = NewContext();
        var actor = new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn);
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        var handler = new ApproveCampusInstanceCommandHandler(
            db, actor, new FixedClock(),
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db), new SilentNotifications(),
                new VisitFormReadService(db, actor, Microsoft.Extensions.Logging.Abstractions.NullLogger<VisitFormReadService>.Instance, new FixedClock()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<CampusApprovalExecutor>.Instance));
        await handler.Handle(new ApproveCampusInstanceCommand(requestId, instanceId, IcStaffHn, null, rowVersion), CancellationToken.None);
    }

    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId)
    {
        using var db = NewContext();
        var actor = new FakeUser(IcStaffHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn);
        await new StartVisitPreparationCommandHandler(db, actor, new FixedClock())
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task<ulong> InviteAsync(ulong instanceId)
    {
        using var db = NewContext();
        var participant = new VisitParticipant
        {
            VisitInstanceId = instanceId,
            UserId = IcStaffHn2,
            ParticipantRole = ParticipantRoles.IcSupport,
            IsHost = false,
            Status = ParticipantStatuses.Invited,
            AssignedBy = LeaderHn,
            AssignedAt = Now,
            CreatedAt = Now,
            CreatedBy = LeaderHn,
        };
        db.VisitParticipants.Add(participant);
        await db.SaveChangesAsync();
        return participant.ParticipantId;
    }

    private static async Task<ulong> SeedAssignedLogisticsItemAsync(ulong instanceId)
    {
        using var db = NewContext();
        var item = new VisitLogisticsItem
        {
            VisitInstanceId = instanceId,
            ItemType = "EQUIPMENT",
            Title = "Máy chiếu",
            Status = LogisticsItemStatus.Assigned,
            CoordinationMode = "SYSTEM_REQUEST",
            RequestedBy = IcStaffHn,
            RequestedAt = Now,
            AssignedToUserId = IcStaffHn2,
            AssignedBy = IcStaffHn,
            AssignedAt = Now,
            RowVersion = 0,
            CreatedAt = Now,
            CreatedBy = IcStaffHn,
        };
        db.VisitLogisticsItems.Add(item);
        await db.SaveChangesAsync();
        return item.LogisticsItemId;
    }

    /// <summary>
    /// Locks the same tier2-3 (+ tier4 for the logistics cascade) rows
    /// <c>CancelVisitRequestCommandHandler</c> locks, holds them for <paramref name="hold"/> so a
    /// concurrent responder is provably queued behind it, then applies the SAME per-campus (and, when
    /// <paramref name="cascadeLogisticsItemId"/> is given, per-item cascade) field set that handler writes,
    /// and commits — a deterministic, hand-rolled stand-in for "the cancel already landed" (see class remarks).
    /// </summary>
    private static async Task HoldCancelLockThenCancelAsync(
        ulong requestId, ulong instanceId, TaskCompletionSource holding, TimeSpan hold,
        ulong? cascadeLogisticsItemId = null)
    {
        using var db = NewContext();
        var locks = new MySqlUserMutationLockService(db);
        await using var tx = await db.Database.BeginTransactionAsync();

        await locks.LockVisitRequestsAsync(new[] { requestId }, CancellationToken.None);
        await locks.LockVisitRequestCampusesAsync(new[] { instanceId }, CancellationToken.None);
        if (cascadeLogisticsItemId is { } lockItemId)
            await locks.LockVisitLogisticsItemsAsync(new[] { lockItemId }, CancellationToken.None);

        holding.SetResult();
        await Task.Delay(hold);

        var reason = "Khách xác nhận hủy trong lúc chờ phản hồi";
        var now = Now;
        var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceId);
        instance.Status = VisitInstanceStatus.Cancelled;
        instance.CancelledBy = IcStaffHn;
        instance.CancelledAt = now;
        instance.CancellationActorType = CancellationActorType.Host;
        instance.CancellationSource = CancellationSource.ExternalConfirmation;
        instance.CancellationReason = reason;
        instance.UpdatedAt = now;
        instance.UpdatedBy = IcStaffHn;
        instance.RowVersion += 1;

        if (cascadeLogisticsItemId is { } itemId)
        {
            var item = await db.VisitLogisticsItems.SingleAsync(l => l.LogisticsItemId == itemId);
            item.Status = LogisticsItemStatus.Cancelled;
            item.DecisionNote = $"Hủy logistics do campus instance đã hủy. Lý do: {reason}";
            item.UpdatedAt = now;
            item.UpdatedBy = IcStaffHn;
            item.RowVersion += 1;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM notifications WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_logistics_assignment_attempts WHERE logistics_item_id IN (SELECT logistics_item_id FROM visit_logistics_items WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0}))");
        await Del("DELETE FROM visit_logistics_items WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM email_action_tokens WHERE target_type='VISIT_PARTICIPANT' AND target_id IN (SELECT participant_id FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0}))");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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
    public async Task A_participant_accept_that_loses_the_lock_race_to_a_campus_cancel_is_refused_with_zero_mutation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(30), "Đoàn race accept"));
            var instanceId = await InstanceIdAsync(requestId);
            await ApproveAsync(requestId, instanceId);
            await StartPreparationAsync(requestId, instanceId);
            var participantId = await InviteAsync(instanceId);

            var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTask = Task.Run(() => HoldCancelLockThenCancelAsync(requestId, instanceId, holding, Hold));
            var acceptTask = Task.Run(async () =>
            {
                await holding.Task;
                using var db = NewContext();
                var handler = new RespondVisitParticipantInvitationCommandHandler(
                    db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                    new FixedClock(), new SilentNotifications(), new MySqlUserMutationLockService(db));
                var sw = Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => handler.Handle(
                    new RespondVisitParticipantInvitationCommand(participantId, true, null), CancellationToken.None));
                sw.Stop();
                return (Exception: ex, Waited: sw.Elapsed);
            });

            await cancelTask.WaitAsync(LockWait);
            var (ex, waited) = await acceptTask.WaitAsync(LockWait);

            Assert.True(waited >= TimeSpan.FromMilliseconds(300),
                $"Accept returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the VisitRequestCampus lock the cancel held for {Hold.TotalMilliseconds:F0} ms.");
            Assert.IsType<ConflictException>(ex);

            using var check = NewContext();
            var participant = await check.VisitParticipants.AsNoTracking().SingleAsync(p => p.ParticipantId == participantId);
            Assert.Equal(ParticipantStatuses.Invited, participant.Status); // untouched — the accept never wrote
            Assert.Null(participant.RespondedAt);
            var instance = await check.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
            Assert.Equal(VisitInstanceStatus.Cancelled, instance.Status); // the cancel's write stands
            Assert.Empty(await check.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == "VisitParticipant" && a.EntityId == participantId).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_participant_decline_that_loses_the_lock_race_to_a_campus_cancel_is_refused_with_zero_mutation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(30), "Đoàn race decline"));
            var instanceId = await InstanceIdAsync(requestId);
            await ApproveAsync(requestId, instanceId);
            await StartPreparationAsync(requestId, instanceId);
            var participantId = await InviteAsync(instanceId);

            var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTask = Task.Run(() => HoldCancelLockThenCancelAsync(requestId, instanceId, holding, Hold));
            var declineTask = Task.Run(async () =>
            {
                await holding.Task;
                using var db = NewContext();
                var handler = new RespondVisitParticipantInvitationCommandHandler(
                    db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                    new FixedClock(), new SilentNotifications(), new MySqlUserMutationLockService(db));
                var sw = Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => handler.Handle(
                    new RespondVisitParticipantInvitationCommand(participantId, false, "Bận việc đột xuất"), CancellationToken.None));
                sw.Stop();
                return (Exception: ex, Waited: sw.Elapsed);
            });

            await cancelTask.WaitAsync(LockWait);
            var (ex, waited) = await declineTask.WaitAsync(LockWait);

            Assert.True(waited >= TimeSpan.FromMilliseconds(300),
                $"Decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the VisitRequestCampus lock.");
            Assert.IsType<ConflictException>(ex);

            using var check = NewContext();
            var participant = await check.VisitParticipants.AsNoTracking().SingleAsync(p => p.ParticipantId == participantId);
            Assert.Equal(ParticipantStatuses.Invited, participant.Status);
            Assert.Null(participant.Note);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_logistics_accept_that_loses_the_lock_race_to_a_campus_cancel_is_refused_with_zero_mutation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(30), "Đoàn race logi accept"));
            var instanceId = await InstanceIdAsync(requestId);
            await ApproveAsync(requestId, instanceId);
            await StartPreparationAsync(requestId, instanceId);
            var itemId = await SeedAssignedLogisticsItemAsync(instanceId);

            var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTask = Task.Run(() => HoldCancelLockThenCancelAsync(requestId, instanceId, holding, Hold, itemId));
            var acceptTask = Task.Run(async () =>
            {
                await holding.Task;
                using var db = NewContext();
                var handler = new AcceptAssignedLogisticsTaskCommandHandler(
                    db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                    new SilentNotifications(), new MySqlUserMutationLockService(db));
                var sw = Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => handler.Handle(
                    new AcceptAssignedLogisticsTaskCommand { LogisticsItemId = itemId }, CancellationToken.None));
                sw.Stop();
                return (Exception: ex, Waited: sw.Elapsed);
            });

            await cancelTask.WaitAsync(LockWait);
            var (ex, waited) = await acceptTask.WaitAsync(LockWait);

            Assert.True(waited >= TimeSpan.FromMilliseconds(300),
                $"Logistics accept returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the lock the cancel held.");
            Assert.NotNull(ex);

            using var check = NewContext();
            var item = await check.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == itemId);
            Assert.Equal(LogisticsItemStatus.Cancelled, item.Status); // the cancel's write stands
            Assert.Null(item.AssigneeAcceptedAt);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_logistics_decline_that_loses_the_lock_race_to_a_campus_cancel_is_refused_with_zero_mutation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(30), "Đoàn race logi decline"));
            var instanceId = await InstanceIdAsync(requestId);
            await ApproveAsync(requestId, instanceId);
            await StartPreparationAsync(requestId, instanceId);
            var itemId = await SeedAssignedLogisticsItemAsync(instanceId);

            var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelTask = Task.Run(() => HoldCancelLockThenCancelAsync(requestId, instanceId, holding, Hold, itemId));
            var declineTask = Task.Run(async () =>
            {
                await holding.Task;
                using var db = NewContext();
                var handler = new DeclineAssignedLogisticsTaskCommandHandler(
                    db, new FakeUser(IcStaffHn2, RoleCodes.Staff, UserSubRoles.Staff, CampusHn),
                    new SilentNotifications(), new MySqlUserMutationLockService(db));
                var sw = Stopwatch.StartNew();
                var ex = await Record.ExceptionAsync(() => handler.Handle(
                    new DeclineAssignedLogisticsTaskCommand { LogisticsItemId = itemId, Reason = "Đang bận nhiệm vụ khác" },
                    CancellationToken.None));
                sw.Stop();
                return (Exception: ex, Waited: sw.Elapsed);
            });

            await cancelTask.WaitAsync(LockWait);
            var (ex, waited) = await declineTask.WaitAsync(LockWait);

            Assert.True(waited >= TimeSpan.FromMilliseconds(300),
                $"Logistics decline returned after only {waited.TotalMilliseconds:F0} ms — it did not contend on the lock the cancel held.");
            Assert.NotNull(ex);

            using var check = NewContext();
            var item = await check.VisitLogisticsItems.AsNoTracking().SingleAsync(l => l.LogisticsItemId == itemId);
            Assert.Equal(LogisticsItemStatus.Cancelled, item.Status);
            Assert.Null(item.AssigneeResponseNote);
        }
        finally { await CleanupAsync(requestId); }
    }
}
