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
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.SaveVisitAgenda;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5E — saving a campus instance's agenda is host-only and stays on that instance.
///
/// The agenda belongs to one campus instance: only that instance's current Host may save it, only while
/// it is in the preparation window, and the saved items must never touch a sibling campus of the same
/// request. Item order is deterministic (the incoming order becomes sequence_order), and the audit row is
/// filed under the instance's own campus so a campus-scoped audit finds it.
/// </summary>
public sealed class VisitAgendaScopeV2Tests
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
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "AG" + Guid.NewGuid().ToString("N"),
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

    private static SaveVisitAgendaCommandHandler Handler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock(), new SilentNotifications());

    private static SaveVisitAgendaItem Item(string title, int hourOffset)
        => new(null, title, Now.AddDays(5).Date.AddHours(9 + hourOffset),
            Now.AddDays(5).Date.AddHours(10 + hourOffset), null, "Phòng họp", null);

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
    public async Task The_host_saves_their_own_instance_agenda_in_order_and_the_audit_is_campus_scoped()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var hnStart = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", hnStart, "Đoàn nghị trình"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            var hn = instances[CampusHn];

            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hn,
                    new List<SaveVisitAgendaItem> { Item("Đón khách", 0), Item("Tham quan", 1), Item("Ăn trưa", 2) },
                    hnStart, hnStart.AddMinutes(120)),
                    CancellationToken.None);
                Assert.Equal(3, res.Count);
            }

            using (var db = NewContext())
            {
                var agendas = await db.VisitAgendas.AsNoTracking()
                    .Where(a => a.VisitInstanceId == hn).OrderBy(a => a.SequenceOrder).ToListAsync();
                Assert.Equal(new[] { "Đón khách", "Tham quan", "Ăn trưa" }, agendas.Select(a => a.Title).ToArray());
                Assert.Equal(new[] { 0, 1, 2 }, agendas.Select(a => a.SequenceOrder).ToArray()); // deterministic order

                var audit = Assert.Single(await db.AuditLogs.AsNoTracking()
                    .Where(a => a.VisitInstanceId == hn && a.Action == "SAVE_VISIT_AGENDA").ToListAsync());
                Assert.Equal(CampusHn, audit.CampusId);
                Assert.Equal(requestId, audit.VisitRequestId);
                Assert.Equal(HostHn, audit.ActorUserId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// "Lưu lịch trình" also moves the campus's planned window, and whatever reads the instance next
    /// sees the moved one.
    ///
    /// <para>
    /// The Host renegotiates the actual date/time with the delegation while drafting the agenda, so the
    /// two edits are one save. This matters beyond the column: the Schedule Report PDF and the
    /// setup-progress email both render <c>plannedStart</c>/<c>plannedEnd</c> from this instance, so a
    /// window that saved into the agenda but not into <c>visit_request_campuses</c> would send the guest
    /// a report contradicting the schedule directly above it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Saving_the_agenda_moves_the_planned_window_and_later_reads_see_the_new_one()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var originalStart = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", originalStart, "Đoàn dời giờ"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            var hn = instances[CampusHn];

            // The delegation asked to start a day later and stay an extra hour.
            var movedStart = originalStart.AddDays(1).Date.AddHours(9);
            var movedEnd = movedStart.AddMinutes(180);

            using (var db = NewContext())
                await Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hn,
                    new List<SaveVisitAgendaItem> { Item("Đón khách", 0) }, movedStart, movedEnd),
                    CancellationToken.None);

            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking()
                    .FirstAsync(c => c.VisitInstanceId == hn);

                // Compared to the second: MySQL DATETIME is local wall-clock here, never re-based to UTC.
                Assert.Equal(movedStart.ToString("yyyy-MM-dd HH:mm:ss"),
                    instance.PlannedStartAt.ToString("yyyy-MM-dd HH:mm:ss"));
                Assert.Equal(movedEnd.ToString("yyyy-MM-dd HH:mm:ss"),
                    instance.PlannedEndAt.ToString("yyyy-MM-dd HH:mm:ss"));

                // …and the values the report/email templates interpolate come from that same row, so the
                // guest-facing "dự kiến từ … đến …" follows the save rather than the original booking.
                // hostEmail is no longer a variable of this template: {{contactInformationBlock}}
                // carries the Host's address, along with the role and telephone number a bare variable
                // could not, and it resolves from the visit instance rather than from whatever the
                // caller passed. What this test is about — that the times follow the saved row — is
                // unchanged.
                var variables = PEMS.Application.Delegations.SetupProgressEmail.VisitSetupProgressEmailGuard
                    .BuildVariables(instance, "Đoàn dời giờ", "FPT Hà Nội", "Host HN");
                Assert.Equal(movedStart.ToString("HH:mm dd/MM/yyyy"), variables["plannedStart"]);
                Assert.Equal(movedEnd.ToString("HH:mm dd/MM/yyyy"), variables["plannedEnd"]);
                Assert.DoesNotContain("hostEmail", variables.Keys);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// A window shorter than the DB CHECK allows is refused by name rather than by constraint violation.
    /// </summary>
    [Fact]
    public async Task A_planned_window_that_breaks_the_minimum_duration_is_refused_before_the_database_sees_it()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn giờ xấu"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            var hn = instances[CampusHn];

            using (var db = NewContext())
                await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ValidationException>(() =>
                    Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hn,
                        new List<SaveVisitAgendaItem> { Item("Đón khách", 0) },
                        start, start.AddMinutes(10)), CancellationToken.None));

            using (var db = NewContext())
                Assert.Empty(await db.VisitAgendas.AsNoTracking()
                    .Where(a => a.VisitInstanceId == hn).ToListAsync());
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Only_the_instances_own_host_may_save_its_agenda_and_a_sibling_is_never_touched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(21);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN"),
                Campus("HCM", start.AddDays(1), "Đoàn HCM"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await StartPreparationAsync(requestId, instances[CampusHcm], HostHcm, CampusHcm);
            var hn = instances[CampusHn];
            var hcm = instances[CampusHcm];

            // Both hosts save their own campus's agenda.
            using (var db = NewContext())
                await Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hn,
                    new List<SaveVisitAgendaItem> { Item("HN mục 1", 0) },
                    start, start.AddMinutes(120)), CancellationToken.None);
            using (var db = NewContext())
                await Handler(db, HostHcm).Handle(new SaveVisitAgendaCommand(requestId, hcm,
                    new List<SaveVisitAgendaItem> { Item("HCM mục 1", 0), Item("HCM mục 2", 1) },
                    start.AddDays(1), start.AddDays(1).AddMinutes(120)), CancellationToken.None);

            // The HN host reaching for HCM's agenda is refused...
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hcm,
                        new List<SaveVisitAgendaItem> { Item("Xâm phạm", 0) },
                        start.AddDays(1), start.AddDays(1).AddMinutes(120)), CancellationToken.None));

            // ...and HCM's agenda is exactly what its own host left — two items, untouched.
            using (var db = NewContext())
            {
                var hcmAgenda = await db.VisitAgendas.AsNoTracking()
                    .Where(a => a.VisitInstanceId == hcm).OrderBy(a => a.SequenceOrder).ToListAsync();
                Assert.Equal(new[] { "HCM mục 1", "HCM mục 2" }, hcmAgenda.Select(a => a.Title).ToArray());
                var hnAgenda = await db.VisitAgendas.AsNoTracking().Where(a => a.VisitInstanceId == hn).ToListAsync();
                Assert.Single(hnAgenda); // HN untouched by the HCM save
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Responsible_person_is_free_text_not_tied_to_any_user_account()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(22);
            requestId = await CreateAsync(Campus("HN", start, "Đoàn HN"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instances[CampusHn], HostHn, CampusHn);
            var hn = instances[CampusHn];

            // Any free-typed name is accepted — it is plain text, never validated against a user list
            // (unlike the old responsible_user_id link it replaces).
            using (var db = NewContext())
            {
                var res = await Handler(db, HostHn).Handle(new SaveVisitAgendaCommand(requestId, hn,
                    new List<SaveVisitAgendaItem>
                    {
                        new(null, "Mục có người phụ trách", Now.AddDays(5).Date.AddHours(9),
                            Now.AddDays(5).Date.AddHours(10), null, "Phòng họp", "Nguyễn Văn A (khách mời ngoài hệ thống)"),
                    }, start, start.AddMinutes(120)), CancellationToken.None);
                Assert.Equal(1, res.Count);
            }
            using (var db = NewContext())
            {
                var agenda = Assert.Single(await db.VisitAgendas.AsNoTracking().Where(a => a.VisitInstanceId == hn).ToListAsync());
                Assert.Equal("Nguyễn Văn A (khách mời ngoài hệ thống)", agenda.ResponsibleName);
                Assert.Null(agenda.ResponsibleUserId); // no longer written by SaveVisitAgenda
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
