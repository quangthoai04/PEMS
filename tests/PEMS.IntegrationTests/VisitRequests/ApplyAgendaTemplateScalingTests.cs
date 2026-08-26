using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.AgendaTemplates.Commands.ApplyAgendaTemplate;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.StartVisitPreparation;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Proportional agenda-template scaling (see AgendaTemplateTimelineScaler), exercised end-to-end
/// through the real Apply pipeline against a real MySQL row — not just the pure-math unit tests.
///
/// Builds a campus instance through the actual production path (Create → Approve → StartPreparation,
/// mirroring VisitAgendaScopeV2Tests) so every DB trigger the real feature relies on is exercised, then
/// applies a template whose relative timeline does not match the visit's real planned window and
/// asserts the persisted visit_agendas rows landed on the SCALED boundaries, not the template's raw
/// baseline minutes.
/// </summary>
public sealed class ApplyAgendaTemplateScalingTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2/PR-3 master into it to run these tests.");
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

    private static CampusVisitFormDto Campus(string code, DateTime start, DateTime end, string delegationName)
        => new(code, start, end, delegationName, VisitTypes.Meeting, null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // Self-matched contact so the campus starts already past the confirmation gate — this suite
            // is not testing that gate.
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "VI", null, "DECLINED", null, null);

    private static async Task<ulong> CreateAsync(CampusVisitFormDto campus)
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
            "AATS" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, new List<CampusVisitFormDto> { campus });
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
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

    private static async Task StartPreparationAsync(ulong requestId, ulong instanceId, ulong hostId, ulong campusId)
    {
        using var db = NewContext();
        var actor = new FakeUser(hostId, RoleCodes.Staff, UserSubRoles.Staff, campusId);
        await new StartVisitPreparationCommandHandler(db, actor, new FixedClock())
            .Handle(new StartVisitPreparationCommand(requestId, instanceId), CancellationToken.None);
    }

    private static async Task<ulong> InstanceIdAsync(ulong requestId, ulong campusId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId && c.CampusId == campusId)
            .Select(c => c.VisitInstanceId).SingleAsync();
    }

    /// <summary>The classic 3-item / 120-minute-span template from the feature spec: 0-20, 20-90, 90-120.</summary>
    private static async Task<ulong> CreateThreeItemTemplateAsync()
    {
        using var db = NewContext();
        var template = new AgendaTemplate
        {
            CampusId = null,
            CampusScopeKey = AgendaScope.Global,
            VisitType = VisitTypes.Meeting,
            Name = "AATS-" + Guid.NewGuid().ToString("N")[..12],
            Status = AgendaTemplateStatuses.Active,
            CreatedAt = Now,
            Items = new List<AgendaTemplateItem>
            {
                new() { DisplayOrder = 1, StartOffsetMinutes = 0, DurationMinutes = 20, Title = "Đón đoàn", CreatedAt = Now },
                new() { DisplayOrder = 2, StartOffsetMinutes = 20, DurationMinutes = 70, Title = "Trao đổi làm việc", CreatedAt = Now },
                new() { DisplayOrder = 3, StartOffsetMinutes = 90, DurationMinutes = 30, Title = "Tiễn đoàn", CreatedAt = Now },
            },
        };
        db.AgendaTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.AgendaTemplateId;
    }

    private static async Task CleanupAsync(ulong requestId, ulong templateId)
    {
        using var db = NewContext();
        if (templateId != 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM agenda_template_items WHERE agenda_template_id = {0}", templateId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM agenda_templates WHERE agenda_template_id = {0}", templateId);
        }
        if (requestId == 0) return;
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

    /// <summary>Spec Case A/B: a 120-minute-span template applied to a 60-minute visit halves every
    /// boundary, and the last item's end lands exactly on the real plannedEndAt — round-tripped
    /// through a real MySQL DATETIME column, not just kept in memory.</summary>
    [Fact]
    public async Task Applying_a_120_minute_template_to_a_60_minute_visit_halves_every_boundary()
    {
        RequireDb();
        ulong requestId = 0, templateId = 0;
        try
        {
            var start = Now.AddDays(20);
            var end = start.AddMinutes(60); // half the template's 120-minute span
            requestId = await CreateAsync(Campus("HN", start, end, "Đoàn co ngắn"));
            var instanceId = await InstanceIdAsync(requestId, CampusHn);
            await ApproveAsync(requestId, instanceId, LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instanceId, HostHn, CampusHn);
            templateId = await CreateThreeItemTemplateAsync();

            using (var db = NewContext())
            {
                var handler = new ApplyAgendaTemplateCommandHandler(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());
                var response = await handler.Handle(
                    new ApplyAgendaTemplateCommand { VisitInstanceId = instanceId, AgendaTemplateId = templateId, ReplaceExisting = false },
                    CancellationToken.None);
                Assert.Equal(3, response.Count);
            }

            using (var db = NewContext())
            {
                var agendas = await db.VisitAgendas.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceId).OrderBy(a => a.SequenceOrder).ToListAsync();

                Assert.Equal(3, agendas.Count);
                AssertMinutesFromStart(start, agendas[0].StartTime, 0);
                AssertMinutesFromStart(start, agendas[0].EndTime!.Value, 10);
                AssertMinutesFromStart(start, agendas[1].StartTime, 10);
                AssertMinutesFromStart(start, agendas[1].EndTime!.Value, 45);
                AssertMinutesFromStart(start, agendas[2].StartTime, 45);
                AssertMinutesFromStart(start, agendas[2].EndTime!.Value, 60);

                // The last boundary is pinned EXACTLY to the real plannedEndAt (MySQL wall-clock DATETIME,
                // compared to the second — this is the round-trip guarantee, not just the in-memory one).
                Assert.Equal(end.ToString("yyyy-MM-dd HH:mm:ss"), agendas[2].EndTime!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }
        finally { await CleanupAsync(requestId, templateId); }
    }

    /// <summary>Spec Case B: the same template applied to a visit DOUBLE its span scales every
    /// boundary up proportionally — proof this is a ratio, not a difference from the 120-minute
    /// baseline in either direction.</summary>
    [Fact]
    public async Task Applying_a_120_minute_template_to_a_240_minute_visit_doubles_every_boundary()
    {
        RequireDb();
        ulong requestId = 0, templateId = 0;
        try
        {
            var start = Now.AddDays(21);
            var end = start.AddMinutes(240); // double the template's 120-minute span
            requestId = await CreateAsync(Campus("HN", start, end, "Đoàn kéo dài"));
            var instanceId = await InstanceIdAsync(requestId, CampusHn);
            await ApproveAsync(requestId, instanceId, LeaderHn, CampusHn, HostHn);
            await StartPreparationAsync(requestId, instanceId, HostHn, CampusHn);
            templateId = await CreateThreeItemTemplateAsync();

            using (var db = NewContext())
            {
                var handler = new ApplyAgendaTemplateCommandHandler(db, new FakeUser(HostHn, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());
                await handler.Handle(
                    new ApplyAgendaTemplateCommand { VisitInstanceId = instanceId, AgendaTemplateId = templateId, ReplaceExisting = false },
                    CancellationToken.None);
            }

            using (var db = NewContext())
            {
                var agendas = await db.VisitAgendas.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceId).OrderBy(a => a.SequenceOrder).ToListAsync();

                Assert.Equal(3, agendas.Count);
                AssertMinutesFromStart(start, agendas[0].StartTime, 0);
                AssertMinutesFromStart(start, agendas[0].EndTime!.Value, 40);
                AssertMinutesFromStart(start, agendas[1].StartTime, 40);
                AssertMinutesFromStart(start, agendas[1].EndTime!.Value, 180);
                AssertMinutesFromStart(start, agendas[2].StartTime, 180);
                AssertMinutesFromStart(start, agendas[2].EndTime!.Value, 240);
            }
        }
        finally { await CleanupAsync(requestId, templateId); }
    }

    private static void AssertMinutesFromStart(DateTime plannedStart, DateTime actual, int expectedMinutes)
    {
        var diff = (actual - plannedStart).TotalMinutes;
        Assert.True(Math.Abs(diff - expectedMinutes) < 0.01,
            $"expected {expectedMinutes} minutes from plannedStart, got {diff}");
    }
}
