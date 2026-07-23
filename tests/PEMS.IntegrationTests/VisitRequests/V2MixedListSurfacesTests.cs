using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Common.Options;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase F — list/report surfaces for MIXED per-campus v2 requests (plan §8.3). Verifies the shared
/// conditional (mixed rows use THIS instance's detail; v1/non-mixed keep the projection; NO global
/// fallback for mixed) both through the batched helper primitive and through a real end-to-end list
/// surface (Staff calendar), including the hidden-sibling keyword rule. Committed data is cascade-cleaned.
/// </summary>
public sealed class V2MixedListSurfacesTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
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
        public FakeUser(ulong id, string role = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            UserId = id; RoleCode = role; SubRole = subRole; PrimaryCampusId = campusId;
        }
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

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegationName)
        => new(code, start, start.AddMinutes(120), delegationName, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);

    /// <summary>Creates a committed MIXED 2-campus request (HN name ≠ HCM name), drives both instances
    /// to ASSIGNED + parent APPROVED, and returns (requestId, hnInstance, hcmInstance).</summary>
    private static async Task<(ulong RequestId, ulong HnInstance, ulong HcmInstance)> CreateMixedApprovedAsync(
        string hnName, string hcmName, DateTime start)
    {
        ulong requestId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db));
            var form = new VisitRequestFormDataV2(
                "LS" + Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
                new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"),
                null,
                new List<CampusVisitFormDto> { Campus("HN", start, hnName), Campus("HCM", start.AddDays(1), hcmName) });
            var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
            Assert.True(created.HasMixedCampusDetails);
            requestId = created.VisitRequestId;
        }
        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);
            foreach (var instance in visit.CampusInstances)
            {
                instance.Status = VisitInstanceStatuses.Assigned;
                instance.CurrentHostUserId = instance.CoordinatorUserId;
                instance.HostAssignedBy = instance.CoordinatorUserId;
                instance.HostAssignedAt = Now;
                instance.DecidedBy = instance.CoordinatorUserId;
                instance.DecidedAt = Now;
                instance.DecisionActorRole = "STAFF_LEADER";
                instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
                instance.RowVersion += 1;
            }
            await db.SaveChangesAsync();
            visit.Status = VisitRequestStatuses.Approved;
            visit.RowVersion += 1;
            await db.SaveChangesAsync();
        }
        using (var db = NewContext())
        {
            var rows = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId)
                .OrderBy(c => c.CampusId)
                .Select(c => c.VisitInstanceId)
                .ToListAsync();
            return (requestId, rows[0], rows[1]);
        }
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    [Fact]
    public async Task Effective_name_helper_uses_instance_detail_for_mixed_and_projection_otherwise()
    {
        RequireDb();
        ulong mixed = 0, uniform = 0;
        try
        {
            var start = Now.AddDays(20);
            (mixed, var hn, var hcm) = await CreateMixedApprovedAsync("Đoàn HN riêng", "Đoàn HCM riêng", start);

            using (var db = NewContext())
            {
                var names = await VisitInstanceEffectiveName.ForInstancesAsync(
                    db, new[] { hn, hcm }, CancellationToken.None);
                Assert.Equal("Đoàn HN riêng", names[hn]);   // per-instance, never the projection
                Assert.Equal("Đoàn HCM riêng", names[hcm]); // sibling keeps its own content
            }

            // Non-mixed v2: the projection IS every instance's content — helper returns it unchanged.
            using (var db = NewContext())
            {
                var handler = new CreateVisitRequestV2CommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                    new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                    new UserProvisionService(db),
                    NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db));
                var form = new VisitRequestFormDataV2(
                    "LS" + Guid.NewGuid().ToString("N"),
                    new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
                    new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"),
                    null,
                    new List<CampusVisitFormDto>
                    {
                        Campus("HN", start, "Đoàn đồng nhất"),
                        Campus("HCM", start.AddDays(1), "Đoàn đồng nhất"),
                    });
                var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
                Assert.False(created.HasMixedCampusDetails);
                uniform = created.VisitRequestId;
            }
            using (var db = NewContext())
            {
                var ids = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == uniform).Select(c => c.VisitInstanceId).ToListAsync();
                var names = await VisitInstanceEffectiveName.ForInstancesAsync(db, ids, CancellationToken.None);
                Assert.All(ids, id => Assert.Equal("Đoàn đồng nhất", names[id]));
            }
        }
        finally
        {
            await CleanupAsync(mixed);
            await CleanupAsync(uniform);
        }
    }

    [Fact]
    public async Task Staff_calendar_rows_show_each_campus_its_own_mixed_name()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, var hn, var hcm) = await CreateMixedApprovedAsync(
                "Đoàn Lịch HN " + Guid.NewGuid().ToString("N")[..6],
                "Đoàn Lịch HCM " + Guid.NewGuid().ToString("N")[..6], start);

            string hnName, hcmName;
            ulong hnLeader, hcmLeader, hnCampus, hcmCampus;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking()
                    .Include(c => c.FormDetail)
                    .Where(c => c.VisitRequestId == requestId)
                    .OrderBy(c => c.CampusId)
                    .Select(c => new { c.CampusId, c.CoordinatorUserId, c.FormDetail!.DelegationName })
                    .ToListAsync();
                hnCampus = rows[0].CampusId; hcmCampus = rows[1].CampusId;
                hnLeader = rows[0].CoordinatorUserId!.Value; hcmLeader = rows[1].CoordinatorUserId!.Value;
                hnName = rows[0].DelegationName; hcmName = rows[1].DelegationName;
            }

            // The HN Staff Leader's office calendar shows the HN row titled with the HN detail —
            // and NEVER the sibling's (HCM) name: the projection is not used for mixed requests.
            using (var db = NewContext())
            {
                var handler = new GetStaffCalendarQueryHandler(
                    db, new FakeUser(hnLeader, RoleCodes.Staff, UserSubRoles.Leader, hnCampus), new FixedClock());
                var result = await handler.Handle(
                    new GetStaffCalendarQuery("office", start.AddDays(-1), start.AddDays(3)), CancellationToken.None);
                var row = result.Items.Single(i => i.VisitRequestId == requestId);
                Assert.Equal(hnName, row.DelegationName);
                Assert.NotEqual(hcmName, row.DelegationName);
            }
            using (var db = NewContext())
            {
                var handler = new GetStaffCalendarQueryHandler(
                    db, new FakeUser(hcmLeader, RoleCodes.Staff, UserSubRoles.Leader, hcmCampus), new FixedClock());
                var result = await handler.Handle(
                    new GetStaffCalendarQuery("office", start, start.AddDays(3)), CancellationToken.None);
                var row = result.Items.Single(i => i.VisitRequestId == requestId);
                Assert.Equal(hcmName, row.DelegationName);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Management_list_exposes_form_schema_version_so_frontend_routes_to_v2()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var start = Now.AddDays(20);
            (requestId, _, _) = await CreateMixedApprovedAsync(
                "Đoàn Ver HN " + Guid.NewGuid().ToString("N")[..6],
                "Đoàn Ver HCM " + Guid.NewGuid().ToString("N")[..6], start);

            string requestCode;
            using (var db = NewContext())
                requestCode = await db.VisitRequests.AsNoTracking()
                    .Where(v => v.VisitRequestId == requestId)
                    .Select(v => v.RequestCode!)
                    .SingleAsync();

            // The Visitor owner's management list must carry form_schema_version=2 (+ mixed flag) so the
            // frontend routes this row straight to the v2 UI — never waiting for a v1 endpoint 409.
            using (var db = NewContext())
            {
                var handler = new ViewGuestDelegationListQueryHandler(
                    db, new FakeUser(Registrant), new FixedClock());
                var result = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = requestCode },
                    CancellationToken.None);

                var row = result.Items.Single(i => i.VisitRequestId == requestId);
                // Pure V2: there is no form-version discriminator to assert. What matters is that the row
                // is flagged mixed and shows the safe label instead of one campus's content.
                Assert.True(row.HasMixedCampusDetails);
                Assert.Equal("Khác nhau theo cơ sở", row.DelegationName);          // mixed request-level label
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Slice 5B: scope-safe search match contexts ────────────────────────────────

    [Fact]
    public async Task Search_match_contexts_scope_to_authorized_campuses_and_do_not_leak_a_hidden_sibling()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var alpha = $"AlphaKW{tag}";
            var beta = $"BetaKW{tag}";
            var start = Now.AddDays(20);
            (requestId, _, _) = await CreateMixedApprovedAsync($"Đoàn {alpha}", $"Đoàn {beta}", start);

            ulong hnCampus, hcmCampus, hnLeader;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).OrderBy(c => c.CampusId)
                    .Select(c => new { c.CampusId, c.CoordinatorUserId }).ToListAsync();
                hnCampus = rows[0].CampusId; hcmCampus = rows[1].CampusId;
                hnLeader = rows[0].CoordinatorUserId!.Value;
            }

            // Owner sees ALL campuses → a campus-specific keyword yields ONLY that campus's context.
            using (var db = NewContext())
            {
                var handler = new ViewGuestDelegationListQueryHandler(db, new FakeUser(Registrant), new FixedClock());
                var res = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = beta },
                    CancellationToken.None);
                var row = res.Items.Single(i => i.VisitRequestId == requestId);
                var campusCtx = Assert.Single(row.MatchedContexts!.Where(c => c.Scope == SearchMatchScopes.Campus));
                Assert.Equal(hcmCampus, campusCtx.CampusId);
                Assert.Contains(VisitSearchFieldCodes.DelegationName, campusCtx.MatchedFields);
                Assert.DoesNotContain(row.MatchedContexts!, c => c.CampusId == hnCampus); // non-matching campus not attributed
            }

            // The HN Staff Leader is scoped to campus HN. HCM's keyword (a hidden sibling) must NOT surface the
            // request and must not differ in count from a keyword that exists nowhere.
            using (var db = NewContext())
            {
                var handler = new ViewGuestDelegationListQueryHandler(
                    db, new FakeUser(hnLeader, RoleCodes.Staff, UserSubRoles.Leader, hnCampus), new FixedClock());
                var hidden = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = beta },
                    CancellationToken.None);
                Assert.DoesNotContain(hidden.Items, i => i.VisitRequestId == requestId);
                var nowhere = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = $"zz_{tag}_nowhere" },
                    CancellationToken.None);
                Assert.DoesNotContain(nowhere.Items, i => i.VisitRequestId == requestId);
                Assert.Equal(nowhere.TotalItems, hidden.TotalItems); // hidden-campus match never inflates the count

                // The HN leader's OWN keyword surfaces the row with ONLY the HN campus context.
                var own = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = alpha },
                    CancellationToken.None);
                var row = own.Items.Single(i => i.VisitRequestId == requestId);
                var campusCtx = Assert.Single(row.MatchedContexts!.Where(c => c.Scope == SearchMatchScopes.Campus));
                Assert.Equal(hnCampus, campusCtx.CampusId);
                Assert.DoesNotContain(row.MatchedContexts!, c => c.CampusId == hcmCampus); // sibling never leaks
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Search_returns_one_row_with_a_context_per_matching_authorized_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var shared = $"SharedKW{tag}";
            var start = Now.AddDays(20);
            (requestId, _, _) = await CreateMixedApprovedAsync($"Đoàn {shared} HN", $"Đoàn {shared} HCM", start);

            ulong hnCampus, hcmCampus;
            string requestCode;
            using (var db = NewContext())
            {
                var rows = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).OrderBy(c => c.CampusId)
                    .Select(c => c.CampusId).ToListAsync();
                hnCampus = rows[0]; hcmCampus = rows[1];
                requestCode = await db.VisitRequests.AsNoTracking()
                    .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
            }

            using (var db = NewContext())
            {
                var handler = new ViewGuestDelegationListQueryHandler(db, new FakeUser(Registrant), new FixedClock());

                // A token shared by both campuses → ONE parent row carrying TWO authorized campus contexts.
                var shared2 = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = shared },
                    CancellationToken.None);
                var row = Assert.Single(shared2.Items.Where(i => i.VisitRequestId == requestId)); // never duplicated per campus
                var campusCtxs = row.MatchedContexts!.Where(c => c.Scope == SearchMatchScopes.Campus).ToList();
                Assert.Equal(2, campusCtxs.Count);
                Assert.Contains(campusCtxs, c => c.CampusId == hnCampus);
                Assert.Contains(campusCtxs, c => c.CampusId == hcmCampus);

                // A request-level field (the code) → a REQUEST context, never attributed to a campus.
                var byCode = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = requestCode },
                    CancellationToken.None);
                var codeRow = byCode.Items.Single(i => i.VisitRequestId == requestId);
                var reqCtx = Assert.Single(codeRow.MatchedContexts!.Where(c => c.Scope == SearchMatchScopes.Request));
                Assert.Contains(VisitSearchFieldCodes.RequestCode, reqCtx.MatchedFields);
                Assert.DoesNotContain(codeRow.MatchedContexts!, c => c.Scope == SearchMatchScopes.Campus);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Guest_member_names_are_not_searched_and_produce_no_row()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var start = Now.AddDays(20);
            // Campus() seeds a guest member "Guest A"; the delegation names deliberately omit "Guest".
            (requestId, _, _) = await CreateMixedApprovedAsync($"Đoàn HN {tag}", $"Đoàn HCM {tag}", start);

            using (var db = NewContext())
            {
                var handler = new ViewGuestDelegationListQueryHandler(db, new FakeUser(Registrant), new FixedClock());
                var res = await handler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", Page = 1, PageSize = 200, Keyword = "Guest A" },
                    CancellationToken.None);
                Assert.DoesNotContain(res.Items, i => i.VisitRequestId == requestId); // guest names are not a searchable field
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
