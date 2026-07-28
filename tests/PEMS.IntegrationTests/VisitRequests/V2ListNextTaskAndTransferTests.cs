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
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Queries.ViewGuestDelegationList;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using PEMS.Application.Delegations.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The management list's next-task contract and its instance-scoped handover verdict (prompt §16).
///
/// Two properties are load-bearing here and both are asserted against the REAL query over MySQL:
///   • the next task is per-READER, not per-status — the same campus tells its Staff Leader to decide,
///     its Host to prepare, and the visitor nothing at all;
///   • the handover verdict is INSTANCE-scoped and campus-scoped — it names one visit_instance_id, it
///     is absent from a multi-campus summary row, and a campus the caller does not lead produces none.
/// Committed rows are cascade-cleaned in a finally.
/// </summary>
public sealed class V2ListNextTaskAndTransferTests
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

    /// <summary>A committed 2-campus request, left fully PENDING (every campus WAITING_REQUEST_APPROVAL).</summary>
    private static async Task<ulong> CreatePendingAsync(DateTime start, string tag)
    {
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db));
        var form = new VisitRequestFormDataV2(
            "NT" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
            new ContactPointDto("Registrant", "Org", "+8491", V2SeedActor.Email(Registrant)),
            null,
            new List<CampusVisitFormDto>
            {
                Campus("HN", start, $"Đoàn HN {tag}"),
                Campus("HCM", start.AddDays(1), $"Đoàn HCM {tag}"),
            });
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        return created.VisitRequestId;
    }

    /// <summary>Drives every campus of a request to ASSIGNED with its coordinator as the Host, request APPROVED.</summary>
    private static async Task ApproveAllAsync(ulong requestId)
    {
        using var db = NewContext();
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
            instance.DecisionNote = "Campus xác nhận tiếp nhận đoàn. Người phụ trách tiếp đón đã được phân công.";
            instance.RowVersion += 1;
        }
        await db.SaveChangesAsync();
        visit.Status = VisitRequestStatuses.Approved;
        visit.RowVersion += 1;
        await db.SaveChangesAsync();
    }

    private sealed record CampusFacts(ulong InstanceId, ulong CampusId, ulong LeaderId, DateTime Start);

    private static async Task<List<CampusFacts>> FactsAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .OrderBy(c => c.PlannedStartAt)
            .Select(c => new CampusFacts(c.VisitInstanceId, c.CampusId, c.CoordinatorUserId!.Value, c.PlannedStartAt))
            .ToListAsync();
    }

    private static async Task<string> RequestCodeAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RequestCode!).SingleAsync();
    }

    private static async Task<PEMS.Application.Common.Models.PaginatedResult<VisitRequestManagementItemDto>> ListAsync(
        ICurrentUserService caller, string keyword, string tab = "responsible")
    {
        using var db = NewContext();
        var handler = new ViewGuestDelegationListQueryHandler(db, caller, new FixedClock());
        return await handler.Handle(
            new ViewGuestDelegationListQuery { Tab = tab, Page = 1, PageSize = 200, Keyword = keyword },
            CancellationToken.None);
    }

    private static VisitActionCapabilityDto? TransferOf(IEnumerable<VisitActionCapabilityDto> caps)
        => caps.FirstOrDefault(c => c.Code == VisitFormActions.TransferHost);

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE c FROM visit_instance_amendment_changes c JOIN visit_instance_amendments a ON a.amendment_id = c.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
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

    // ── §16.2 / §16.1 the campus Staff Leader is told to decide ──────────────────────────────────

    [Fact]
    public async Task Campus_leader_of_a_pending_campus_is_told_to_decide_and_assign()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            var facts = await FactsAsync(requestId);
            var hn = facts[0];
            var code = await RequestCodeAsync(requestId);

            var res = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);
            var row = res.Items.Single(i => i.VisitInstanceId == hn.InstanceId);

            Assert.Equal(VisitNextTaskCodes.ReviewAndAssign, row.NextTask!.Code);
            Assert.True(row.NextTask.RequiresAction);
            Assert.Equal(VisitActionScopes.Instance, row.NextTask.Scope);
            Assert.Equal(hn.InstanceId, row.NextTask.VisitInstanceId);
            // The task points at an action the row genuinely offers — never at one it does not.
            Assert.Equal("APPROVE_AND_ASSIGN_HOST", row.NextTask.ActionCode);
            Assert.Contains("APPROVE_AND_ASSIGN_HOST", row.AllowedActions);
            // Status, relation and task are three separate values, none of them a copy of another.
            Assert.Equal("Chờ xử lý tại cơ sở", row.StatusLabel);
            Assert.Equal("Bạn có quyền duyệt tại cơ sở", row.RelationLabel);

            // No Host yet ⇒ nothing to hand over, and the verdict says exactly that rather than vanishing.
            var transfer = TransferOf(row.Capabilities);
            Assert.NotNull(transfer);
            Assert.False(transfer!.Enabled);
            Assert.DoesNotContain(VisitFormActions.TransferHost, row.AllowedActions);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §16.3 the Host is told to prepare ───────────────────────────────────────────────────────

    [Fact]
    public async Task Host_of_an_assigned_campus_is_told_to_finish_the_preparation()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            await ApproveAllAsync(requestId);
            var facts = await FactsAsync(requestId);
            var hn = facts[0];
            var code = await RequestCodeAsync(requestId);

            // The coordinator became the Host in ApproveAllAsync, and they are the campus leader too —
            // the leader's own tab is the instance view, so this row is both host and leader.
            var res = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);
            var row = res.Items.Single(i => i.VisitInstanceId == hn.InstanceId);

            Assert.True(row.CurrentUserIsHost);
            // No agenda rows exist, which is precisely what CompleteVisitStage(before) refuses on — so the
            // task is "finish preparing", not "confirm it is done".
            Assert.Equal(VisitNextTaskCodes.CompletePreparation, row.NextTask!.Code);
            Assert.Equal("OPEN_HOST_PROCESS", row.NextTask.ActionCode);
            Assert.Equal("Đã duyệt và phân công", row.StatusLabel);
            Assert.Equal("Bạn phụ trách tiếp đón", row.RelationLabel);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §16.4 / §16.5 the requester side and HO get no handover and no task ─────────────────────

    [Fact]
    public async Task Visitor_owner_and_HO_get_no_handover_verdict_and_no_task()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            await ApproveAllAsync(requestId);
            var code = await RequestCodeAsync(requestId);

            var owner = await ListAsync(new FakeUser(Registrant), code);
            var ownerRow = owner.Items.Single(i => i.VisitRequestId == requestId);
            Assert.Null(TransferOf(ownerRow.Capabilities));
            Assert.DoesNotContain(VisitFormActions.TransferHost, ownerRow.AllowedActions);
            Assert.Equal(VisitNextTaskCodes.None, ownerRow.NextTask!.Code);
            Assert.False(ownerRow.NextTask.RequiresAction);

            // The multi-campus SUMMARY row must not carry the handover either: it edits one campus, and
            // a summary row cannot say which. Every campus item is likewise refused for this caller.
            Assert.All(ownerRow.CampusProgressItems, c =>
            {
                Assert.Null(TransferOf(c.Capabilities));
                Assert.False(c.CanTransferHost);
            });

            var ho = await ListAsync(new FakeUser(999_999, RoleCodes.Ho), code);
            var hoRow = ho.Items.SingleOrDefault(i => i.VisitRequestId == requestId);
            Assert.NotNull(hoRow);
            Assert.Equal("HO_MONITOR", hoRow!.CurrentUserRelation);
            Assert.Equal("Chỉ theo dõi", hoRow.RelationLabel);
            Assert.Null(TransferOf(hoRow.Capabilities));
            Assert.Equal(VisitNextTaskCodes.None, hoRow.NextTask!.Code);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §16.6 / §16.7 / §16.9 per-campus scoping of the verdict ─────────────────────────────────

    [Fact]
    public async Task Handover_verdict_names_one_instance_and_only_the_campus_the_caller_leads()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            await ApproveAllAsync(requestId);
            var facts = await FactsAsync(requestId);
            var hn = facts[0];
            var hcm = facts[1];
            var code = await RequestCodeAsync(requestId);

            var res = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);

            // Scope-first: the HN leader sees ONE row — their campus. The sibling never appears, so its
            // task and its actions cannot leak through this surface at all.
            var row = Assert.Single(res.Items.Where(i => i.VisitRequestId == requestId));
            Assert.Equal(hn.InstanceId, row.VisitInstanceId);
            Assert.NotEqual(hcm.InstanceId, row.VisitInstanceId);

            var transfer = TransferOf(row.Capabilities);
            Assert.NotNull(transfer);
            Assert.True(transfer!.Enabled);
            Assert.Equal(VisitActionScopes.Instance, transfer.Scope);
            Assert.Equal((long)hn.InstanceId, transfer.VisitInstanceId);       // one instance, named
            Assert.Contains(VisitFormActions.TransferHost, row.AllowedActions);
            // The verdict is measured against THIS campus's start, and carries the deadline either way.
            Assert.Equal(hn.Start, transfer.PlannedStartAt);
            Assert.Equal(hn.Start.AddHours(-6), transfer.CutoffAt);
            Assert.Equal(6, transfer.RequiredLeadHours);
            // The concurrency token travels with the row so the handover can be started from the list.
            Assert.NotNull(row.RowVersion);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Handover_is_refused_with_the_cutoff_reason_once_the_campus_is_inside_six_hours()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            // Starts in five hours — inside the six-hour lead time, so the window has closed.
            requestId = await CreatePendingAsync(Now.AddHours(5), tag);
            await ApproveAllAsync(requestId);
            var facts = await FactsAsync(requestId);
            var hn = facts[0];
            var code = await RequestCodeAsync(requestId);

            var res = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);
            var row = res.Items.Single(i => i.VisitInstanceId == hn.InstanceId);

            var transfer = TransferOf(row.Capabilities);
            Assert.NotNull(transfer);
            Assert.False(transfer!.Enabled);
            Assert.Equal(VisitMutationErrorCodes.CutoffReached, transfer.DisabledReasonCode);
            Assert.Contains("6 giờ", transfer.DisabledReason);
            Assert.NotNull(transfer.CutoffAt);                                  // deadline still stated
            // Refused ⇒ absent from the flat list, so the two can never disagree.
            Assert.DoesNotContain(VisitFormActions.TransferHost, row.AllowedActions);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §16.8 a pending amendment outranks the Host's own preparation work ──────────────────────

    [Fact]
    public async Task Pending_amendment_takes_priority_over_the_preparation_task()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            await ApproveAllAsync(requestId);
            var facts = await FactsAsync(requestId);
            var hn = facts[0];
            var code = await RequestCodeAsync(requestId);

            // Before: the leader (also the Host here) is asked to prepare.
            var before = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);
            Assert.Equal(VisitNextTaskCodes.CompletePreparation,
                before.Items.Single(i => i.VisitInstanceId == hn.InstanceId).NextTask!.Code);

            using (var db = NewContext())
            {
                db.VisitInstanceAmendments.Add(new VisitInstanceAmendment
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = hn.InstanceId,
                    AmendmentNo = 1,
                    Status = AmendmentStatuses.PendingApproval,
                    BaseFormRevision = 1,
                    BaseApprovalRevision = 1,
                    RequestedBy = Registrant,
                    RequestedAt = Now,
                    Reason = "Đổi giờ bắt đầu",
                    ExpectedInstanceRowVersion = 2,
                    CreatedAt = Now,
                });
                await db.SaveChangesAsync();
            }

            var after = await ListAsync(
                new FakeUser(hn.LeaderId, RoleCodes.Staff, UserSubRoles.Leader, hn.CampusId), code);
            var row = after.Items.Single(i => i.VisitInstanceId == hn.InstanceId);
            Assert.Equal(VisitNextTaskCodes.ReviewAmendment, row.NextTask!.Code);
            Assert.True(row.NextTask.RequiresAction);
            // Deciding a multi-field proposal is a detail-screen job, so the list points there rather
            // than claiming an action it cannot perform.
            Assert.Null(row.NextTask.ActionCode);
            // The unread/pending signal stays a SEPARATE channel from the task.
            Assert.Equal(1, row.ChangeSummary!.PendingAmendmentCount);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── §16.10 the decision note is returned verbatim ───────────────────────────────────────────

    [Fact]
    public async Task Decision_note_is_returned_exactly_as_stored_per_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            requestId = await CreatePendingAsync(Now.AddDays(20), tag);
            await ApproveAllAsync(requestId);
            var facts = await FactsAsync(requestId);
            var code = await RequestCodeAsync(requestId);
            const string expected = "Campus xác nhận tiếp nhận đoàn. Người phụ trách tiếp đón đã được phân công.";

            // Owner sees every campus → each item carries its own stored note, unmodified and
            // un-embellished (nothing is generated on the read path).
            var owner = await ListAsync(new FakeUser(Registrant), code);
            var row = owner.Items.Single(i => i.VisitRequestId == requestId);
            Assert.Equal(facts.Count, row.CampusProgressItems.Count);
            Assert.All(row.CampusProgressItems, c => Assert.Equal(expected, c.DecisionNote));

            using var db = NewContext();
            var stored = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId)
                .Select(c => c.DecisionNote)
                .ToListAsync();
            Assert.All(stored, s => Assert.Equal(expected, s));
        }
        finally { await CleanupAsync(requestId); }
    }
}
