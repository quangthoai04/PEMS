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
using PEMS.Application.Common.Security;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.News.Commands.CreateNews;
using PEMS.Application.News.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;
using MediaConsentStatus = PEMS.Shared.MediaConsentStatus;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 5D — creating a visit-linked news article enforces per-campus media consent and instance scope.
///
/// Media consent is a per-campus fact on each instance's own detail, so in a mixed request the campus that
/// agreed permits a contribution while the campus that declined refuses one — the handler never reads a
/// request-wide value. A Student must pick a visit, and the backend re-checks that the caller is the host
/// or an accepted participant of the chosen instance rather than trusting the id the client sent.
///
/// These are the guard paths, which fire before any HTML is sanitized or translated, so the sanitizer and
/// translator are inert fakes that are never reached.
/// </summary>
public sealed class NewsContributionConsentV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong HostHn = 101;
    private const ulong HostHcm = 103;
    private const ulong StudentAuthor = 152; // ACTIVE Student
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

    // Inert dependencies — the guard paths under test throw before these are reached.
    private sealed class NoopSanitizer : IHtmlSanitizerService
    {
        public string Sanitize(string? html) => html ?? string.Empty;
        public string SanitizeEmailHtml(string? html) => html ?? string.Empty;
    }

    private sealed class NoopTranslator : INewsTranslationService
    {
        public Task<IReadOnlyList<string>> TranslateTextAsync(IReadOnlyList<string> contents, string s, string t, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(contents);
        public Task<IReadOnlyList<string>> TranslateHtmlAsync(IReadOnlyList<string> contents, string s, string t, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(contents);
        public Task<NewsTranslationConnectionTestResult> TestConnectionAsync(
            string projectId, string location, string credentialJson, int timeoutSeconds, CancellationToken ct)
            => Task.FromResult(new NewsTranslationConnectionTestResult { Success = true });
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName, string consent)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null,
            $"Mục đích {delegationName}", $"Nội dung {delegationName}",
            new List<VisitorDto> { new($"Khách {delegationName}", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto($"Đầu mối {delegationName}", "OpOrg", "+8410", "op@example.com"),
            "VI", null, consent, null, null, null);

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
            "NC" + Guid.NewGuid().ToString("N"),
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

    private static CreateNewsCommandHandler Handler(ApplicationDbContext db, ulong actor, string role, string? subRole)
        => new(db, new FakeUser(actor, role, subRole, CampusHn), new NoopSanitizer(), new NoopTranslator(),
            NullLogger<CreateNewsCommandHandler>.Instance);

    private static CreateNewsCommand News(ulong? instanceId)
        => new()
        {
            VisitInstanceId = instanceId,
            Title = "Tin bài tiếp khách",
            Summary = "Tóm tắt",
            ContentSections = new List<CreateNewsContentSectionDto>
            {
                new() { SectionOrder = 1, SectionTitle = "Phần 1", SectionBodyHtml = "<p>Nội dung</p>" },
            },
        };

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM news WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
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
    public async Task Per_campus_consent_permits_the_agreed_campus_and_refuses_the_declined_sibling()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            requestId = await CreateAsync(
                Campus("HN", start, "Đoàn HN", MediaConsentStatus.Agreed),
                Campus("HCM", start.AddDays(1), "Đoàn HCM", MediaConsentStatus.Declined));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, HostHcm);
            await MoveToAfterVisitAsync(instances[CampusHn]);
            await MoveToAfterVisitAsync(instances[CampusHcm]);

            // The DECLINED campus (HCM) refuses a contribution even to its own host — consent is read from
            // THAT instance's own detail, not a request-wide value.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ConflictException>(() =>
                    Handler(db, HostHcm, RoleCodes.Staff, UserSubRoles.Staff)
                        .Handle(News(instances[CampusHcm]), CancellationToken.None));

            // The AGREED campus (HN) gets PAST the consent + scope guards for its own host: whatever the
            // handler does next, it is NOT the media-consent conflict (proving consent is per-campus).
            using (var db = NewContext())
            {
                var ex = await Record.ExceptionAsync(() =>
                    Handler(db, HostHn, RoleCodes.Staff, UserSubRoles.Staff)
                        .Handle(News(instances[CampusHn]), CancellationToken.None));
                if (ex is ConflictException conflict)
                    Assert.DoesNotContain("truyền thông", conflict.Message);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_student_must_choose_a_visit_and_the_backend_rechecks_the_chosen_instance_scope()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(21), "Đoàn HN", MediaConsentStatus.Agreed));
            var instances = await InstanceIdsAsync(requestId);
            await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, HostHn);
            await MoveToAfterVisitAsync(instances[CampusHn]);

            // A Student with no visit selected is refused up front.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ValidationException>(() =>
                    Handler(db, StudentAuthor, RoleCodes.Student, null).Handle(News(null), CancellationToken.None));

            // A Student who DID select a visit but is not a host/accepted participant of it is refused by
            // the backend re-check — the preselected id is never trusted on its own.
            using (var db = NewContext())
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Handler(db, StudentAuthor, RoleCodes.Student, null)
                        .Handle(News(instances[CampusHn]), CancellationToken.None));
        }
        finally { await CleanupAsync(requestId); }
    }
}
