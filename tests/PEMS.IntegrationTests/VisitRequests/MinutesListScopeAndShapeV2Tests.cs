using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.MeetingMinutes.Queries.SearchAndFilterMinutes;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Minutes;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase 3D-2 — the meeting-minutes list: campus scope, per-instance titles, and query shape.
///
/// Scope here is campus-derived and is applied to the joined base query before any keyword or filter, so
/// a minute on a campus the actor does not hold must be invisible in the rows, in the total, and in the
/// summary counters alike — a leak through the counters is still a leak.
///
/// The mapping loop also used to issue nine sequential queries per row, so a ten-row page cost ninety
/// round trips. The counting test pins the shape rather than the timing: the same page must not get more
/// expensive as rows are added.
/// </summary>
public sealed class MinutesListScopeAndShapeV2Tests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private const ulong LeaderHn = 3;
    private const ulong LeaderHcm = 9;
    private const ulong LeaderDn = 11;
    private const ulong IcStaffHn = 101;
    private const ulong IcStaffHcm = 103;
    private const ulong IcStaffDn = 105;
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;
    private const ulong CampusDn = 3;

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString));
        if (counter is not null) builder.AddInterceptors(counter);
        return new ApplicationDbContext(builder.Options);
    }

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Count++; return base.ReaderExecuting(command, eventData, result); }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        { Count++; return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
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
            new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "MIN" + Guid.NewGuid().ToString("N"),
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

    private static async Task<ulong> AddMinuteAsync(ulong instanceId, string title, ulong authorId)
    {
        using var db = NewContext();
        var minute = new Minute
        {
            VisitInstanceId = instanceId,
            Title = title,
            Content = "Nội dung biên bản " + title,
            Status = "DRAFT",
            RowVersion = 0,
            CreatedAt = Now,
            CreatedBy = authorId,
        };
        db.Minutes.Add(minute);
        await db.SaveChangesAsync();

        db.MinuteParticipants.Add(new MinuteParticipant
        {
            MinutesId = minute.MinutesId,
            UserId = authorId,
            FullNameSnapshot = $"[IT] Người dự {authorId}",
            AttendanceStatus = "PRESENT",
            CreatedAt = Now,
        });
        db.MinuteActionItems.Add(new MinuteActionItem
        {
            MinutesId = minute.MinutesId,
            Title = "Việc cần làm " + title,
            Status = "TODO",
            CreatedAt = Now,
        });
        await db.SaveChangesAsync();
        return minute.MinutesId;
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

    /// <summary>Three campuses, one minute each, every name unique so a leak is unmistakable.</summary>
    private static async Task<(ulong RequestId, Dictionary<ulong, ulong> Instances, string Tag)> ThreeCampusFixtureAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var start = Now.AddDays(50);
        var requestId = await CreateAsync(
            Campus("HN", start, $"ĐoànHN{tag}"),
            Campus("HCM", start.AddDays(1), $"ĐoànHCM{tag}"),
            Campus("DN", start.AddDays(2), $"ĐoànDN{tag}"));
        var instances = await InstanceIdsAsync(requestId);
        await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
        await ApproveAsync(requestId, instances[CampusHcm], LeaderHcm, CampusHcm, IcStaffHcm);
        await ApproveAsync(requestId, instances[CampusDn], LeaderDn, CampusDn, IcStaffDn);
        return (requestId, instances, tag);
    }

    [Fact]
    public async Task A_campus_actor_never_sees_a_sibling_campus_minute_in_rows_totals_or_summary()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            Dictionary<ulong, ulong> instances;
            string tag;
            (requestId, instances, tag) = await ThreeCampusFixtureAsync();

            await AddMinuteAsync(instances[CampusHn], $"BiênBảnHN{tag}", IcStaffHn);
            await AddMinuteAsync(instances[CampusHcm], $"BiênBảnHCM{tag}", IcStaffHcm);
            await AddMinuteAsync(instances[CampusDn], $"BiênBảnDN{tag}", IcStaffDn);

            using var db = NewContext();
            var handler = new SearchAndFilterMinutesQueryHandler(
                db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn));

            // The keyword exists ONLY on a campus outside this actor's scope. If keyword ran before
            // scope, the row — or at least the count — would move.
            var hidden = await handler.Handle(
                new SearchAndFilterMinutesQuery { Q = $"BiênBảnHCM{tag}", Page = 1, PageSize = 50 },
                CancellationToken.None);
            var nowhere = await handler.Handle(
                new SearchAndFilterMinutesQuery { Q = $"zz{tag}nowhere", Page = 1, PageSize = 50 },
                CancellationToken.None);

            Assert.Empty(hidden.Items);
            Assert.Equal(nowhere.TotalCount, hidden.TotalCount);

            // The actor's OWN campus keyword does surface the row, titled from its own instance.
            var own = await handler.Handle(
                new SearchAndFilterMinutesQuery { Q = $"BiênBảnHN{tag}", Page = 1, PageSize = 50 },
                CancellationToken.None);
            var row = Assert.Single(own.Items);
            Assert.Equal($"ĐoànHN{tag}", row.VisitTitle);
            Assert.NotEqual($"ĐoànHCM{tag}", row.VisitTitle);
            Assert.NotEqual($"ĐoànDN{tag}", row.VisitTitle);
            Assert.Equal(instances[CampusHn], row.VisitInstanceId);

            // The unfiltered page for this actor contains exactly one of the three minutes, and the
            // summary counters — computed before the keyword — must be scoped too.
            var all = await handler.Handle(
                new SearchAndFilterMinutesQuery { Page = 1, PageSize = 200 }, CancellationToken.None);
            var mine = all.Items.Where(i => i.VisitInstanceId == instances[CampusHn]).ToList();
            Assert.Single(mine);
            Assert.DoesNotContain(all.Items, i => i.VisitInstanceId == instances[CampusHcm]);
            Assert.DoesNotContain(all.Items, i => i.VisitInstanceId == instances[CampusDn]);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Each_campus_leader_reads_their_own_instance_title_for_the_same_request()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            Dictionary<ulong, ulong> instances;
            string tag;
            (requestId, instances, tag) = await ThreeCampusFixtureAsync();

            await AddMinuteAsync(instances[CampusHn], $"BB{tag}", IcStaffHn);
            await AddMinuteAsync(instances[CampusHcm], $"BB{tag}", IcStaffHcm);
            await AddMinuteAsync(instances[CampusDn], $"BB{tag}", IcStaffDn);

            // Same minute title everywhere, so the ONLY thing distinguishing the three answers is
            // whose campus detail each leader reads.
            async Task<string?> TitleFor(ulong leaderId, ulong campusId)
            {
                using var db = NewContext();
                var res = await new SearchAndFilterMinutesQueryHandler(
                        db, new FakeUser(leaderId, RoleCodes.Staff, UserSubRoles.Leader, campusId))
                    .Handle(new SearchAndFilterMinutesQuery { Q = $"BB{tag}", Page = 1, PageSize = 50 },
                        CancellationToken.None);
                return Assert.Single(res.Items).VisitTitle;
            }

            Assert.Equal($"ĐoànHN{tag}", await TitleFor(LeaderHn, CampusHn));
            Assert.Equal($"ĐoànHCM{tag}", await TitleFor(LeaderHcm, CampusHcm));
            Assert.Equal($"ĐoànDN{tag}", await TitleFor(LeaderDn, CampusDn));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_page_costs_the_same_number_of_queries_however_many_rows_it_holds()
    {
        RequireDb();
        // uq_minutes_visit_instance allows one minute per instance, so extra rows mean extra
        // single-campus requests on the SAME campus — both measurements stay inside one actor's scope.
        var tag = Guid.NewGuid().ToString("N")[..6];
        var requestIds = new List<ulong>();
        try
        {
            async Task AddOneRowAsync(int n)
            {
                var requestId = await CreateAsync(
                    Campus("HN", Now.AddDays(50 + n), $"Đoàn{n}{tag}"));
                requestIds.Add(requestId);
                var instances = await InstanceIdsAsync(requestId);
                await ApproveAsync(requestId, instances[CampusHn], LeaderHn, CampusHn, IcStaffHn);
                await AddMinuteAsync(instances[CampusHn], $"BB{n}{tag}", IcStaffHn);
            }

            async Task<int> CountQueries(int expectedRows)
            {
                var counter = new CommandCounter();
                using var db = NewContext(counter);
                var res = await new SearchAndFilterMinutesQueryHandler(
                        db, new FakeUser(LeaderHn, RoleCodes.Staff, UserSubRoles.Leader, CampusHn))
                    .Handle(new SearchAndFilterMinutesQuery { Q = tag, Page = 1, PageSize = 50 },
                        CancellationToken.None);
                Assert.Equal(expectedRows, res.Items.Count);
                return counter.Count;
            }

            await AddOneRowAsync(1);
            var oneRow = await CountQueries(1);

            for (var i = 2; i <= 5; i++) await AddOneRowAsync(i);
            var fiveRows = await CountQueries(5);

            // Five rows must not cost five times one row. Before batching, each extra row added nine
            // queries of its own; now the per-page lookups are fixed and the count does not move.
            Assert.Equal(oneRow, fiveRows);
        }
        finally
        {
            foreach (var requestId in requestIds) await CleanupAsync(requestId);
        }
    }
}
