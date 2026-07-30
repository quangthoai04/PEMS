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
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Minutes;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5C — creating and editing meeting minutes stays on one campus instance.
///
/// A minute belongs to exactly one instance: only that instance's Host (or an accepted participant) may
/// create or edit it, and its action items live under that minute. The Host of a sibling campus of the
/// same request has no relation to this instance, so they can neither open nor save its minutes, and the
/// sibling's own minute is never touched.
/// </summary>
public sealed class MinutesMutationScopeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 101;
    private const ulong HostHcm = 103;
    private const ulong Stranger = 202;
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

    private sealed class SilentEmail : IEmailService
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendAsync(OutboundEmail message, CancellationToken ct = default) => Task.CompletedTask;
        public Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.FromResult(EmailDeliveryResult.Sent());
        public Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitRequestOtpAsync(string toEmail, string fullName, string code, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendVisitorAccountCreatedOrLinkedEmailAsync(string toEmail, string contactFullName, string delegationName, string requestCode, string visitScope, string plannedTime, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendRegistrantConfirmationAsync(string toEmail, string registrantFullName, string contactFullName, string contactEmail, string delegationName, string requestCode, CancellationToken ct = default) => Task.CompletedTask;
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
            new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "MN" + Guid.NewGuid().ToString("N"),
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

    private static CreateOrLockMinutesCommandHandler OpenHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock());

    private static SaveMinutesCommandHandler SaveHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor, RoleCodes.Staff, UserSubRoles.Staff, CampusHn), new FixedClock(), new SilentEmail(), new SilentNotifications());

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
    public async Task The_host_creates_and_saves_minutes_with_an_action_item_on_their_own_instance()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20), "Đoàn biên bản"));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            var hn = instances[CampusHn];

            ulong minutesId; string lockToken; uint rowVersion;
            using (var db = NewContext())
            {
                var opened = await OpenHandler(db, HostHn).Handle(
                    new CreateOrLockMinutesCommand(hn, "Biên bản HN"), CancellationToken.None);
                minutesId = opened.MinutesId!.Value; lockToken = opened.EditLockToken!; rowVersion = opened.RowVersion;
            }

            using (var db = NewContext())
            {
                var saved = await SaveHandler(db, HostHn).Handle(new SaveMinutesCommand(
                    minutesId, "Biên bản HN", "Nội dung cuộc họp", lockToken, rowVersion,
                    Participants: null,
                    ActionItems: new List<SaveMinuteActionItemInput> { new(null, "Gửi biên bản", null, null, "TODO") }),
                    CancellationToken.None);
                Assert.Equal("SAVED", saved.Status);
            }

            using (var db = NewContext())
            {
                var minute = await db.Minutes.AsNoTracking().SingleAsync(m => m.MinutesId == minutesId);
                Assert.Equal(hn, minute.VisitInstanceId); // the minute is on THIS instance
                Assert.Equal("Nội dung cuộc họp", minute.Content);
                var actionItem = Assert.Single(await db.MinuteActionItems.AsNoTracking().Where(a => a.MinutesId == minutesId).ToListAsync());
                Assert.Equal("Gửi biên bản", actionItem.Title);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_sibling_campus_host_cannot_open_or_save_this_instances_minutes()
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
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            var hn = instances[CampusHn];

            // The HN host opens and saves HN's minute.
            ulong minutesId; string lockToken; uint rowVersion;
            using (var db = NewContext())
            {
                var opened = await OpenHandler(db, HostHn).Handle(new CreateOrLockMinutesCommand(hn, "Biên bản HN"), CancellationToken.None);
                minutesId = opened.MinutesId!.Value; lockToken = opened.EditLockToken!; rowVersion = opened.RowVersion;
            }
            using (var db = NewContext())
                await SaveHandler(db, HostHn).Handle(new SaveMinutesCommand(
                    minutesId, "Biên bản HN gốc", "Nội dung gốc", lockToken, rowVersion, null, null), CancellationToken.None);

            // The HCM host — host of the SIBLING instance — has no relation to HN, so they can neither
            // open nor save HN's minute.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    OpenHandler(db, HostHcm).Handle(new CreateOrLockMinutesCommand(hn, "Chiếm quyền"), CancellationToken.None));
            using (var db = NewContext())
            {
                var current = await db.Minutes.AsNoTracking().SingleAsync(m => m.MinutesId == minutesId);
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    SaveHandler(db, HostHcm).Handle(new SaveMinutesCommand(
                        minutesId, "Sửa trộm", "Nội dung lạ", "any-token", current.RowVersion, null, null), CancellationToken.None));
            }

            // A stranger with no relation is refused too.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    OpenHandler(db, Stranger).Handle(new CreateOrLockMinutesCommand(hn, "Người lạ"), CancellationToken.None));

            // HN's minute is exactly what its own host saved — untouched by the sibling host / stranger.
            using (var db = NewContext())
            {
                var minute = await db.Minutes.AsNoTracking().SingleAsync(m => m.MinutesId == minutesId);
                Assert.Equal("Biên bản HN gốc", minute.Title);
                Assert.Equal("Nội dung gốc", minute.Content);
                // No minute was created on the sibling instance by these forbidden attempts.
                Assert.Empty(await db.Minutes.AsNoTracking().Where(m => m.VisitInstanceId == instances[CampusHcm]).ToListAsync());
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
