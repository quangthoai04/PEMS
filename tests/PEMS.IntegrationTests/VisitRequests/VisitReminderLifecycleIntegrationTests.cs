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
using PEMS.Application.Delegations.Commands.CompleteVisitStage;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.SaveVisitAgenda;
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Enums;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Layer 1 of the reminder eligibility defence, exercised through the REAL command handlers rather
/// than by calling <see cref="PEMS.Application.Delegations.Reminders.VisitReminderLifecycleSync"/>
/// directly (that is covered in isolation by
/// PEMS.UnitTests.Delegations.Reminders.VisitReminderLifecycleSyncTests) — what belongs here is proof
/// that the handler actually calls it, at the point in the real create→approve→prepare→agenda chain
/// where a PENDING reminder can genuinely exist.
///
/// <para>
/// CancelVisitRequestCommandHandler's and VisitAmendmentService.ApproveAsync's own hooks are NOT
/// covered here — both call the exact same shared helper already proven correct in isolation and
/// exercised end to end by CompleteVisitStageAsync/SaveVisitAgendaAsync below, and standing up their
/// full dependency graphs (lock service, aggregate status service, amendment submit/approve pair) for
/// this specific assertion was judged not worth the added fixture weight. Flagged as a residual gap in
/// the implementation report rather than silently assumed covered.
/// </para>
/// </summary>
public sealed class VisitReminderLifecycleIntegrationTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong HoUser = 2;
    private const ulong HostHn = 101;
    private const ulong CampusHn = 1;

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
        private readonly DateTime _at;
        public FixedClock(DateTime at) => _at = at;
        public DateTime UtcNow => _at;
        public DateTime VietnamNow => _at;
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

    private static FakeUser Owner() => new(Registrant, RoleCodes.Visitor);
    private static FakeUser Host() => new(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn);
    private static FakeUser StaffLeader() => new(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn);

    private static CampusVisitFormDto Campus(DateTime start)
        => new("HN", start, start.AddMinutes(120), "Đoàn giai đoạn", "MEETING", null,
            "Mục đích", "Nội dung",
            new List<VisitorDto> { new("Khách A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<(ulong RequestId, ulong InstanceId)> CreateAsync(DateTime start)
    {
        using var db = NewContext();
        var actor = Owner();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, actor, new FixedClock(Now), new VisitRequestV2CreateService(db),
            new SilentNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "RLC" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, new List<CampusVisitFormDto> { Campus(start) });
        var requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        var instanceId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId).Select(c => c.VisitInstanceId).SingleAsync();
        return (requestId, instanceId);
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, DateTime at)
    {
        using var db = NewContext();
        var actor = StaffLeader();
        var rowVersion = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.RowVersion).SingleAsync();
        await new PEMS.Application.Delegations.Commands.ApproveCampusInstance.ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(at),
                new CampusApprovalExecutor(
                    db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db),
                    new SilentNotifications(),
                    new PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService(
                        db, actor, NullLogger<PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>.Instance, new FixedClock(at)),
                    NullLogger<CampusApprovalExecutor>.Instance))
            .Handle(new PEMS.Application.Delegations.Commands.ApproveCampusInstance.ApproveCampusInstanceCommand(
                requestId, instanceId, HostHn, null, rowVersion), CancellationToken.None);
    }

    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId, DateTime at)
    {
        using var db = NewContext();
        await new StartVisitPreparationCommandHandler(db, Host(), new FixedClock(at))
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task SeedAgendaAsync(ulong requestId, ulong instanceId, DateTime windowStart, DateTime at)
    {
        using var db = NewContext();
        await new SaveVisitAgendaCommandHandler(db, Host(), new FixedClock(at), new SilentNotifications())
            .Handle(new SaveVisitAgendaCommand(requestId, instanceId,
                new List<SaveVisitAgendaItem> { new(null, "Đón khách", windowStart, windowStart.AddHours(1), null, "Phòng họp", null) },
                windowStart, windowStart.AddMinutes(120)),
                CancellationToken.None);
    }

    private static async Task<ulong> SeedPendingReminderAsync(
        ulong instanceId, DateTime scheduledAt, int offsetMinutes = 60,
        VisitReminderChannel channel = VisitReminderChannel.EMAIL,
        VisitReminderTargetGroup target = VisitReminderTargetGroup.HOST)
    {
        using var db = NewContext();
        var row = new VisitInstanceReminderSetting
        {
            VisitInstanceId = instanceId,
            Channel = channel,
            TargetGroup = target,
            OffsetMinutes = offsetMinutes,
            ScheduledAt = scheduledAt,
            Status = VisitReminderStatus.PENDING,
            CreatedAt = DateTime.Now,
        };
        db.VisitInstanceReminderSettings.Add(row);
        await db.SaveChangesAsync();
        return row.ReminderSettingId;
    }

    private static async Task<VisitInstanceReminderSetting> ReminderAsync(ulong reminderId)
    {
        using var db = NewContext();
        return await db.VisitInstanceReminderSettings.AsNoTracking()
            .SingleAsync(r => r.ReminderSettingId == reminderId);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM visit_instance_reminder_settings WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task Completing_the_Before_stage_cancels_a_PENDING_reminder_configured_during_preparation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));

            var reminderId = await SeedPendingReminderAsync(instanceId, start.AddHours(-1));

            await new CompleteVisitStageCommandHandler(NewContext(), Host(), new FixedClock(Now.AddMinutes(4)), new SilentNotifications())
                .Handle(new CompleteVisitStageCommand(requestId, instanceId, VisitStageKeys.Before, true), CancellationToken.None);

            var reminder = await ReminderAsync(reminderId);
            Assert.Equal(VisitReminderStatus.CANCELLED, reminder.Status);
            Assert.StartsWith("VISIT_NO_LONGER_ELIGIBLE:", reminder.ErrorMessage);

            using var verify = NewContext();
            Assert.Equal(VisitInstanceStatus.DuringVisit,
                await verify.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceId).Select(c => c.Status).SingleAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Changing_the_planned_start_via_SaveVisitAgenda_reschedules_a_PENDING_reminder()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var originalStart = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(originalStart);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, originalStart, Now.AddMinutes(3));

            // 60 minutes before the ORIGINAL start.
            var reminderId = await SeedPendingReminderAsync(instanceId, originalStart.AddHours(-1), offsetMinutes: 60);

            // The Host renegotiates the visit 5 hours later while re-saving the agenda.
            var newStart = originalStart.AddHours(5);
            await SeedAgendaAsync(requestId, instanceId, newStart, Now.AddMinutes(4));

            var reminder = await ReminderAsync(reminderId);
            Assert.Equal(VisitReminderStatus.PENDING, reminder.Status);
            // Tolerance, not exact equality: MySQL DATETIME truncates the sub-second part `Now` (an
            // in-memory DateTime.Now) carries, so a round trip through the DB never matches bit-for-bit.
            Assert.Equal(newStart.AddHours(-1), reminder.ScheduledAt, TimeSpan.FromSeconds(1));

            using var verify = NewContext();
            var persistedStart = await verify.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceId).Select(c => c.PlannedStartAt).SingleAsync();
            Assert.Equal(newStart, persistedStart, TimeSpan.FromSeconds(1));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Rescheduling_to_a_moment_the_offset_can_no_longer_reach_cancels_the_reminder_instead_of_firing_it_immediately()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var originalStart = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(originalStart);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, originalStart, Now.AddMinutes(3));

            // 60 minutes before the ORIGINAL start.
            var reminderId = await SeedPendingReminderAsync(instanceId, originalStart.AddHours(-1), offsetMinutes: 60);

            // The Host moves the visit to start in 20 minutes — a 60-minute-before reminder would need
            // to have fired 40 minutes ago, which is no longer possible.
            var soonStart = Now.AddMinutes(24); // +4 min lead time matches the FixedClock passed below
            await SeedAgendaAsync(requestId, instanceId, soonStart, Now.AddMinutes(4));

            var reminder = await ReminderAsync(reminderId);
            Assert.Equal(VisitReminderStatus.CANCELLED, reminder.Status);
            Assert.StartsWith("SCHEDULE_NO_LONGER_VALID:", reminder.ErrorMessage);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Each_channel_keeps_its_own_offset_when_rescheduled_together()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var originalStart = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(originalStart);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, originalStart, Now.AddMinutes(3));

            var inAppId = await SeedPendingReminderAsync(
                instanceId, originalStart.AddHours(-1), offsetMinutes: 60, channel: VisitReminderChannel.IN_APP);
            var emailId = await SeedPendingReminderAsync(
                instanceId, originalStart.AddDays(-1), offsetMinutes: 1440, channel: VisitReminderChannel.EMAIL);

            var newStart = originalStart.AddHours(5);
            await SeedAgendaAsync(requestId, instanceId, newStart, Now.AddMinutes(4));

            var inApp = await ReminderAsync(inAppId);
            var email = await ReminderAsync(emailId);
            Assert.Equal(VisitReminderStatus.PENDING, inApp.Status);
            Assert.Equal(VisitReminderStatus.PENDING, email.Status);
            Assert.Equal(newStart.AddHours(-1), inApp.ScheduledAt, TimeSpan.FromSeconds(1));
            Assert.Equal(newStart.AddDays(-1), email.ScheduledAt, TimeSpan.FromSeconds(1));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_SENT_reminder_is_never_touched_by_either_a_stage_transition_or_a_reschedule()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var instanceId) = await CreateAsync(start);
            await ApproveAsync(requestId, instanceId, Now.AddMinutes(1));
            await StartPreparationAsync(requestId, instanceId, Now.AddMinutes(2));
            await SeedAgendaAsync(requestId, instanceId, start, Now.AddMinutes(3));

            using (var db = NewContext())
            {
                db.VisitInstanceReminderSettings.Add(new VisitInstanceReminderSetting
                {
                    VisitInstanceId = instanceId,
                    Channel = VisitReminderChannel.EMAIL,
                    TargetGroup = VisitReminderTargetGroup.HOST,
                    OffsetMinutes = 60,
                    ScheduledAt = start.AddDays(-10), // long since sent
                    Status = VisitReminderStatus.SENT,
                    LastDispatchedAt = start.AddDays(-10),
                    CreatedAt = DateTime.Now,
                });
                await db.SaveChangesAsync();
            }
            ulong sentId;
            using (var db = NewContext())
                sentId = await db.VisitInstanceReminderSettings.AsNoTracking()
                    .Where(r => r.VisitInstanceId == instanceId).Select(r => r.ReminderSettingId).SingleAsync();

            // Reschedule via SaveVisitAgenda, then leave BEFORE_VISIT via CompleteVisitStage.
            var newStart = start.AddHours(3);
            await SeedAgendaAsync(requestId, instanceId, newStart, Now.AddMinutes(4));
            await new CompleteVisitStageCommandHandler(NewContext(), Host(), new FixedClock(Now.AddMinutes(5)), new SilentNotifications())
                .Handle(new CompleteVisitStageCommand(requestId, instanceId, VisitStageKeys.Before, true), CancellationToken.None);

            var reminder = await ReminderAsync(sentId);
            Assert.Equal(VisitReminderStatus.SENT, reminder.Status);
            Assert.Equal(start.AddDays(-10), reminder.ScheduledAt, TimeSpan.FromSeconds(1));
            Assert.Null(reminder.ErrorMessage);
        }
        finally { await CleanupAsync(requestId); }
    }
}
