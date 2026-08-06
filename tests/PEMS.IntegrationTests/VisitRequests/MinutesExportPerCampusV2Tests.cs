using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.MeetingMinutes.Queries.ExportMinutes;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Minutes;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3C — minutes export (Excel + PDF).
///
/// An export is the highest-risk read surface: a wrong delegation name does not fail the request, it
/// ships a document that names the wrong visit. This export is instance-scoped, so a MIXED request must
/// print THIS instance's own delegation name, and a Staff Leader may only export their own campus's
/// minute. The Excel is parsed and its "Đoàn khách" cell is read directly — not merely checked for a
/// non-empty byte array.
/// </summary>
public sealed class MinutesExportPerCampusV2Tests
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

    /// <summary>
    /// Archiving the export to Drive is best-effort and deliberately outside what this suite measures
    /// (which campus may export whose minute). Doing nothing here keeps the assertions about the
    /// returned bytes rather than about a Drive round-trip.
    /// </summary>
    private sealed class SilentReportArchive : PEMS.Application.Reports.Common.IReportArchiveService
    {
        public Task ArchiveAsync(byte[] content, string fileName, string contentType,
            string documentCategory, ulong? campusId, ulong userId, CancellationToken ct)
            => Task.CompletedTask;
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
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "+8410", V2SeedActor.Email(Registrant)),
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
            "MX" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private static async Task ApproveAsync(ulong requestId, ulong instanceId, ulong leaderId, ulong campusId, ulong hostId)
    {
        using var db = NewContext();
        var actor = new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId);
        await new ApproveCampusInstanceCommandHandler(
                db, actor, new FixedClock(), new VisitRequestAggregateStatusService(db), new SilentNotifications(),
                new VisitFormReadService(db, actor, NullLogger<VisitFormReadService>.Instance, new FixedClock()), new MySqlUserMutationLockService(db))
            .Handle(new ApproveCampusInstanceCommand(requestId, instanceId, hostId, null), CancellationToken.None);
    }

    private static async Task<Dictionary<ulong, ulong>> InstanceIdsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.CampusId, c => c.VisitInstanceId);
    }

    private static async Task<ulong> AddMinuteAsync(ulong instanceId, string title)
    {
        using var db = NewContext();
        var minute = new Minute
        {
            VisitInstanceId = instanceId,
            Title = title,
            Content = "Nội dung " + title,
            Status = "SAVED",
            RowVersion = 0,
            CreatedAt = Now,
            CreatedBy = IcStaffHn,
        };
        db.Minutes.Add(minute);
        await db.SaveChangesAsync();
        return minute.MinutesId;
    }

    private static string? ReadDelegationCell(byte[] xlsx)
    {
        using var ms = new MemoryStream(xlsx);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("Thông tin chung");
        // Row 2 = "Đoàn khách" label in A2, value in B2 (see ExportMinutesExcelQueryHandler).
        Assert.Equal("Đoàn khách", ws.Cell(2, 1).GetString());
        return ws.Cell(2, 2).GetString();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE ai FROM minute_action_items ai JOIN minutes m ON m.minutes_id = ai.minutes_id WHERE m.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE mp FROM minute_participants mp JOIN minutes m ON m.minutes_id = mp.minutes_id WHERE m.visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM minutes WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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
    public async Task Excel_export_prints_the_deciding_instances_own_delegation_name_for_a_mixed_request()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(40);
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}"),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);

            var hnMinute = await AddMinuteAsync(instances[CampusHn], $"BB-HN{tag}");
            var hcmMinute = await AddMinuteAsync(instances[CampusHcm], $"BB-HCM{tag}");

            // HN's leader exports HN's minute → the sheet names HN's delegation, never HCM's.
            using (var db = NewContext())
            {
                var xlsx = await new ExportMinutesExcelQueryHandler(
                        db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn))
                    .Handle(new ExportMinutesExcelQuery { MinutesId = hnMinute }, CancellationToken.None);
                var name = ReadDelegationCell(xlsx);
                Assert.Equal($"ĐoànHN{tag}", name);
                Assert.NotEqual($"ĐoànHCM{tag}", name);
            }

            // HCM's leader exports HCM's minute → the OTHER campus's name, from the same request.
            using (var db = NewContext())
            {
                var xlsx = await new ExportMinutesExcelQueryHandler(
                        db, new FakeUser(LeaderHcm, RoleCodes.Staff, UserSubRoles.Leader, CampusHcm))
                    .Handle(new ExportMinutesExcelQuery { MinutesId = hcmMinute }, CancellationToken.None);
                Assert.Equal($"ĐoànHCM{tag}", ReadDelegationCell(xlsx));
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_leader_cannot_export_a_minute_belonging_to_another_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(41);
            requestId = await CreateAsync(
                Campus("HN", start, $"ĐoànHN{tag}"),
                Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);
            var hcmMinute = await AddMinuteAsync(instances[CampusHcm], $"BB-HCM{tag}");

            // HN's leader reaching for HCM's minute is refused, in both formats.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    new ExportMinutesExcelQueryHandler(
                            db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn))
                        .Handle(new ExportMinutesExcelQuery { MinutesId = hcmMinute }, CancellationToken.None));
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    new ExportMinutesPdfQueryHandler(
                            db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn),
                            new SilentReportArchive())
                        .Handle(new ExportMinutesPdfQuery { MinutesId = hcmMinute }, CancellationToken.None));

            // HO (no fixed campus) may export it, and the PDF bytes come out non-empty.
            using (var db = NewContext())
            {
                var pdf = await new ExportMinutesPdfQueryHandler(
                        db, new FakeUser(500, RoleCodes.Ho), new SilentReportArchive())
                    .Handle(new ExportMinutesPdfQuery { MinutesId = hcmMinute }, CancellationToken.None);
                Assert.NotEmpty(pdf);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
