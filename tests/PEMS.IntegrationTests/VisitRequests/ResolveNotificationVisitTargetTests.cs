using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Queries.ResolveNotificationVisitTarget;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The exact-target resolver's own guarantee, distinct from what
/// <see cref="RelationFilterEntryContextTests"/> already covers for the list endpoint: a notification
/// naming ONE exact (request, instance) pair must get an answer scoped to THAT pair, never to whichever
/// relation the "all"-tab merge (<c>ViewGuestDelegationListQueryHandler.QueryAllMergedAsync</c>) would
/// have picked as the row's single winner, and never to a request-level summary row's top-level
/// <c>VisitInstanceId</c> (null for a Visitor/HO multi-campus request) — see
/// docs/CanhIter3FixBug/GopYCQuyen/PEMS_NOTIFICATION_VISIT_EXACT_TARGET_IMPLEMENTATION_PLAN.md.
/// </summary>
public sealed class ResolveNotificationVisitTargetTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;

    private static bool? _dbUp;
    private static string? _dbFailure;
    private static readonly DateTime Now = DateTime.Now;
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch (Exception ex) { _dbUp = false; _dbFailure = ex.ToString(); }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not usable: " + (_dbFailure ?? "CanConnect() returned false."));
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string role = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        { UserId = id; RoleCode = role; SubRole = subRole; PrimaryCampusId = campusId; }
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
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> r, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> i, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong r, string t, string? m, string nt, string? rt, ulong? ri, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest r, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// Routes ONLY <see cref="ViewGuestDelegationListQuery"/> to a fresh
    /// <see cref="ViewGuestDelegationListQueryHandler"/> for the given caller — exactly what the real
    /// DI-resolved <c>IMediator</c> does in production, just without the container. Nothing else the
    /// resolver could theoretically call is exercised by these tests.
    /// </summary>
    private sealed class FakeMediator : IMediator
    {
        private readonly ICurrentUserService _caller;
        public FakeMediator(ICurrentUserService caller) { _caller = caller; }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is ViewGuestDelegationListQuery q)
            {
                using var db = NewContext();
                var handler = new ViewGuestDelegationListQueryHandler(db, _caller, new FixedClock());
                var result = await handler.Handle(q, cancellationToken);
                return (TResponse)(object)result;
            }
            throw new NotImplementedException($"FakeMediator does not route {request.GetType().Name}");
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new NotImplementedException();
    }

    private static async Task<NotificationVisitTargetDto> ResolveAsync(
        ICurrentUserService caller, ulong requestId, ulong? instanceId)
    {
        // MUST be `async`/`await`, not a bare `return handler.Handle(...)`: with the latter, this
        // method's `using` disposes `db` as soon as it returns the (still-running) Task, not when
        // that Task settles — the handler's own fallback DB probe (only reached on the not-found/
        // no-access path) then throws ObjectDisposedException. `await` keeps `db` alive for the
        // whole call.
        using var db = NewContext();
        var handler = new ResolveNotificationVisitTargetQueryHandler(new FakeMediator(caller), db, caller);
        return await handler.Handle(
            new ResolveNotificationVisitTargetQuery { VisitRequestId = requestId, VisitInstanceId = instanceId },
            CancellationToken.None);
    }

    // ── Fixtures (mirrors RelationFilterEntryContextTests) ─────────────────────────

    private static CampusVisitFormDto Campus(string code, ulong actor)
    {
        var start = Now.AddDays(25);
        return new CampusVisitFormDto(
            code, start, start.AddMinutes(120), "Đoàn " + code, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Đầu mối " + code, "OrgB", "Trưởng phòng", "+84912345678",
                V2SeedActor.Email(actor)),
            "EN", null, "DECLINED", null, null);
    }

    private static async Task<ulong> CreateAsync(ulong actor, string actorRole, params CampusVisitFormDto[] campuses)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(actor, actorRole), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance,
            new PerCampusFormV2Options { Enabled = true }, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));

        var form = new VisitRequestFormDataV2(
            "NT" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(actor)),
            null, campuses.ToList());
        return (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
    }

    private sealed record CampusRow(ulong InstanceId, ulong CampusId, ulong LeaderId, string Status,
        ulong? HostUserId, ulong? ContactUserId);

    private static async Task<Dictionary<string, CampusRow>> StateAsync(ulong requestId)
    {
        using var db = NewContext();
        return await (
            from c in db.VisitRequestCampuses.AsNoTracking()
            join site in db.Campuses.AsNoTracking() on c.CampusId equals site.CampusId
            where c.VisitRequestId == requestId
            select new
            {
                site.CampusCode,
                Row = new CampusRow(c.VisitInstanceId, c.CampusId, c.CoordinatorUserId!.Value, c.Status,
                    c.CurrentHostUserId, c.OperationalContactUserId)
            })
            .ToDictionaryAsync(x => x.CampusCode, x => x.Row);
    }

    private static async Task ApproveAsync(ulong instanceId, ulong hostUserId)
    {
        using var db = NewContext();
        var leaderId = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitInstanceId == instanceId).Select(c => c.CoordinatorUserId!.Value).FirstAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_request_campuses SET status = 'ASSIGNED', decided_by = {1}, decided_at = {2}, " +
            "decision_actor_role = 'STAFF_LEADER', decision_source = 'STANDARD_CAMPUS_REVIEW', " +
            "current_host_user_id = {3}, host_assigned_by = {1}, host_assigned_at = {2} " +
            "WHERE visit_instance_id = {0}",
            instanceId, leaderId, Now.AddMinutes(-30), hostUserId);
    }

    private static async Task SetRequestStatusAsync(ulong requestId, string status)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_requests SET status = {1} WHERE visit_request_id = {0}", requestId, status);
    }

    private static async Task SetRegistrantAsync(ulong requestId, ulong userId)
    {
        using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE visit_requests SET registrant_user_id = {1} WHERE visit_request_id = {0}", requestId, userId);
    }

    private static async Task<ulong> OtherVisitorAsync()
    {
        using var db = NewContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Visitor && u.Status == UserStatuses.Active
                        && u.UserId != Registrant)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
    }

    private static async Task<ulong> ActiveHoAsync()
    {
        using var db = NewContext();
        return await db.Users.AsNoTracking()
            .Where(u => u.Role.RoleCode == RoleCodes.Ho && u.Status == UserStatuses.Active)
            .OrderBy(u => u.UserId).Select(u => u.UserId).FirstAsync();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE c FROM visit_instance_amendment_changes c JOIN visit_instance_amendments a ON a.amendment_id = c.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
        await Del("DELETE FROM email_action_tokens WHERE target_type = 'VISIT_REQUEST_IDENTITY_CHANGE' AND target_id IN (SELECT identity_change_id FROM visit_request_identity_changes WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── The reported bug: Visitor multi-campus, exact instance nested under a request-level
    //    summary row (top-level VisitInstanceId is null) — the resolver must still find it. ──────────

    [Fact]
    public async Task Visitor_multi_campus_resolves_the_exact_named_instance_even_though_the_list_row_has_no_top_level_instance_id()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant));
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];
            await ApproveAsync(hn.InstanceId, hn.LeaderId);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var caller = new FakeUser(Registrant);

            // Confirms the premise: the merged/list row for this request is a SUMMARY row (no single
            // instance of its own) — exactly the shape whose top-level `visitInstanceId` the old
            // frontend `items.find(it => it.visitInstanceId === instanceId)` could never match.
            using (var listDb = NewContext())
            {
                var listHandler = new ViewGuestDelegationListQueryHandler(listDb, caller, new FixedClock());
                var page = await listHandler.Handle(
                    new ViewGuestDelegationListQuery { Tab = "responsible", VisitRequestId = requestId, Page = 1, PageSize = 10 },
                    CancellationToken.None);
                var row = Assert.Single(page.Items);
                Assert.Null(row.VisitInstanceId);
                Assert.True(row.CampusProgressItems.Count == 2);
            }

            var hnTarget = await ResolveAsync(caller, requestId, hn.InstanceId);
            Assert.True(hnTarget.Exists);
            Assert.True(hnTarget.HasAccess);
            Assert.Equal(hn.InstanceId, hnTarget.VisitInstanceId);
            Assert.Equal(hn.CampusId, hnTarget.CampusId);
            Assert.Equal(VisitInstanceStatus.Assigned, hnTarget.CampusStatus);

            var dnTarget = await ResolveAsync(caller, requestId, dn.InstanceId);
            Assert.True(dnTarget.Exists);
            Assert.True(dnTarget.HasAccess);
            Assert.Equal(dn.InstanceId, dnTarget.VisitInstanceId);
            Assert.Equal(dn.CampusId, dnTarget.CampusId);
            Assert.Equal(VisitInstanceStatus.WaitingRequestApproval, dnTarget.CampusStatus);

            // Never each other's campus.
            Assert.NotEqual(hnTarget.CampusId, dnTarget.CampusId);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task HO_multi_campus_resolves_the_exact_instance_read_only()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant));
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            await ApproveAsync(hn.InstanceId, hn.LeaderId);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.PartiallyApproved);

            var ho = new FakeUser(await ActiveHoAsync(), RoleCodes.Ho);
            var target = await ResolveAsync(ho, requestId, hn.InstanceId);

            Assert.True(target.Exists);
            Assert.True(target.HasAccess);
            Assert.Equal(hn.InstanceId, target.VisitInstanceId);
            // HO is read-only monitoring — never a CampusReviewer/Host relation of their own.
            Assert.DoesNotContain(target.RelationContexts, c => c.Relation == VisitRowRelations.CampusReviewer);
            Assert.DoesNotContain(target.RelationContexts, c => c.Relation == VisitRowRelations.Host);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── The Staff-Leader multi-relation-across-instances case: a request-level relation
    //    (registrant) must never leak into an instance the caller holds no operational relation at. ──

    [Fact]
    public async Task Staff_leader_registrant_of_a_two_campus_request_gets_exact_relations_per_instance_never_leaked_from_the_other_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor,
                Campus("HN", Registrant), Campus("DN", Registrant));
            var state = await StateAsync(requestId);
            var hn = state["HN"];
            var dn = state["DN"];

            // Each campus is approved by, and handed to, its OWN Staff Leader — the only host the
            // schema will accept — then HN's leader is also made the registrant, so their own row
            // covers BOTH campuses via the accordion (the exact shape a merged/summary row takes).
            await ApproveAsync(hn.InstanceId, hn.LeaderId);
            await ApproveAsync(dn.InstanceId, dn.LeaderId);
            await SetRequestStatusAsync(requestId, VisitRequestStatuses.Approved);
            await SetRegistrantAsync(requestId, hn.LeaderId);

            var caller = new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId);

            var hnTarget = await ResolveAsync(caller, requestId, hn.InstanceId);
            Assert.True(hnTarget.HasAccess);
            Assert.Contains(hnTarget.RelationContexts, c => c.Relation == VisitRowRelations.Host && c.VisitInstanceId == hn.InstanceId);

            var dnTarget = await ResolveAsync(caller, requestId, dn.InstanceId);
            // Still resolvable — the caller CAN see it (they registered the whole request) — but they
            // hold NO Host/CampusReviewer relation at DN, and the resolver must never invent one just
            // because they hold it at HN (the exact leak an aggregated row's single `AllowedActions`
            // list risked before this fix).
            Assert.True(dnTarget.Exists);
            Assert.True(dnTarget.HasAccess);
            Assert.DoesNotContain(dnTarget.RelationContexts, c => c.Relation == VisitRowRelations.Host);
            Assert.DoesNotContain(dnTarget.RelationContexts, c => c.Relation == VisitRowRelations.CampusReviewer);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Not found / no access ───────────────────────────────────────────────────────

    [Fact]
    public async Task Nonexistent_instance_on_a_real_request_reports_not_exists_not_a_sibling_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var caller = new FakeUser(Registrant);

            var target = await ResolveAsync(caller, requestId, 999_999_999UL);
            Assert.False(target.Exists);
            Assert.False(target.HasAccess);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_caller_with_no_relation_to_a_real_instance_gets_no_access_not_a_guessed_fallback()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Registrant, RoleCodes.Visitor, Campus("HN", Registrant));
            var hn = (await StateAsync(requestId))["HN"];

            var stranger = new FakeUser(await OtherVisitorAsync());
            var target = await ResolveAsync(stranger, requestId, hn.InstanceId);

            Assert.True(target.Exists);
            Assert.False(target.HasAccess);
            Assert.Empty(target.RelationContexts);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Nonexistent_request_reports_not_exists()
    {
        RequireDb();
        var caller = new FakeUser(Registrant);
        var target = await ResolveAsync(caller, 999_999_999UL, null);
        Assert.False(target.Exists);
        Assert.False(target.HasAccess);
    }
}
