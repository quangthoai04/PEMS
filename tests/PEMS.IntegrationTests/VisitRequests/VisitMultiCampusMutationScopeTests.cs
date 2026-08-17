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
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The multi-campus half of the mutation rules (§10), which is where the real bugs lived.
///
/// A request-level change touches data every campus shares, so it is all-or-nothing and takes its
/// deadline from the earliest campus. An instance-level change touches one campus, so a sibling that
/// is under way must not close it. The two used to be conflated in both directions: "Sửa nhanh" was
/// offered on a request whose delegation was already on site, and the request-level guard skipped
/// itself entirely once no campus was active.
/// </summary>
public sealed class VisitMultiCampusMutationScopeTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8;
    private static readonly DateTime Now = DateTime.Now;
    private static bool? _dbUp;

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
        Assert.True(_dbUp!.Value, "disposable test database is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; } = RoleCodes.Visitor;
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
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

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static CampusVisitFormDto Campus(string code, DateTime start)
        => new(code, start, start.AddMinutes(120), "Đoàn Mixed", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);

    private static SubmitVisitSafeEditCommandHandler SafeEdit(ApplicationDbContext db)
        => new(db, new FakeUser { UserId = Registrant }, new FixedClock(), new VisitSafeEditService(db),
            new NoopNotifications(), NullLogger<SubmitVisitSafeEditCommandHandler>.Instance, ReadOn, WriteOn);

    /// <summary>
    /// A two-campus request where HN is <paramref name="hnStatus"/> and CT is <paramref name="ctStatus"/>,
    /// built through the real transition order so the DB triggers see legitimate moves.
    ///
    /// <para>
    /// The request is FILED with distant dates and the schedule is then moved onto the ones the test
    /// asked for. A visit cannot be created inside
    /// <see cref="VisitMutationPolicy.MinScheduleLeadHours"/> — a campus reaches "an hour ago" or "five
    /// hours away" by the date arriving, not by being filed that way. What these cases test is the
    /// ACTION cutoff and the lifecycle scope, which are different rules and still need those states.
    /// </para>
    /// </summary>
    private static async Task<(ulong RequestId, ulong Hn, ulong Ct)> CreateMixedAsync(
        string hnStatus, string ctStatus, DateTime hnStart, DateTime ctStart)
    {
        // Far enough out that create accepts them whatever dates the test is aiming for.
        var filedHn = Now.AddDays(40);
        var filedCt = Now.AddDays(41);
        ulong requestId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser { UserId = Registrant }, new FixedClock(), new VisitRequestV2CreateService(db),
                new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
            var form = new VisitRequestFormDataV2(
                "MC" + Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                null, new List<CampusVisitFormDto> { Campus("HN", filedHn), Campus("HCM", filedCt) });
            requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        }

        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);
            var ordered = visit.CampusInstances.OrderBy(c => c.CampusId).ToList();

            // Time passing, as it does: the filed dates move onto the ones this test is about, keeping
            // each campus's own duration. Done before the decisions below so the agenda rows they may
            // insert carry the real schedule.
            void Reschedule(Domain.Entities.Delegations.VisitRequestCampus instance, DateTime start)
            {
                var duration = instance.PlannedEndAt - instance.PlannedStartAt;
                instance.PlannedStartAt = start;
                instance.PlannedEndAt = start + duration;
            }

            Reschedule(ordered[0], hnStart);
            Reschedule(ordered[1], ctStart);
            await db.SaveChangesAsync();

            // Approving always lands on ASSIGNED, whatever the test ultimately wants: BEFORE_VISIT may
            // only be entered from ASSIGNED and DURING_VISIT only from BEFORE_VISIT, and the DB enforces
            // both. The steps that follow walk the campus the rest of the way, one save each.
            void Decide(Domain.Entities.Delegations.VisitRequestCampus instance, string status)
            {
                if (status == VisitInstanceStatuses.WaitingRequestApproval) return;
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

            Decide(ordered[0], hnStatus);
            Decide(ordered[1], ctStatus);
            await db.SaveChangesAsync();

            visit.Status = VisitRequestStatuses.Approved;
            visit.RowVersion += 1;
            await db.SaveChangesAsync();

            // The Host's own step: ASSIGNED → BEFORE_VISIT, for anything that must end up at or past it.
            foreach (var (instance, wanted) in new[] { (ordered[0], hnStatus), (ordered[1], ctStatus) })
            {
                if (wanted != VisitInstanceStatuses.BeforeVisit && wanted != VisitInstanceStatuses.DuringVisit)
                    continue;
                instance.Status = VisitInstanceStatuses.BeforeVisit;
                instance.RowVersion += 1;
            }
            await db.SaveChangesAsync();

            // DURING_VISIT needs an agenda row before the trigger will admit it.
            foreach (var (instance, wanted) in new[] { (ordered[0], hnStatus), (ordered[1], ctStatus) })
            {
                if (wanted != VisitInstanceStatuses.DuringVisit) continue;
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO visit_agendas (visit_instance_id, sequence_order, title, start_time, end_time, created_at) " +
                    "VALUES ({0}, 1, 'Tiếp đón', {1}, {2}, {3})",
                    instance.VisitInstanceId, instance.PlannedStartAt, instance.PlannedEndAt, Now);
                instance.Status = VisitInstanceStatuses.DuringVisit;
                instance.RowVersion += 1;
            }
            await db.SaveChangesAsync();

            return (requestId, ordered[0].VisitInstanceId, ordered[1].VisitInstanceId);
        }
    }

    private static async Task<(int RequestVersion, Dictionary<ulong, int> InstanceVersions)> VersionsAsync(ulong requestId)
    {
        using var db = NewContext();
        var reqV = await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RowVersion).SingleAsync();
        var instV = await db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.VisitRequestId == requestId)
            .ToDictionaryAsync(c => c.VisitInstanceId, c => c.RowVersion);
        return (reqV, instV);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE ac FROM visit_instance_amendment_changes ac JOIN visit_instance_amendments a ON a.amendment_id = ac.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── Journey A: HN before the visit, CT under way ─────────────────────────

    [Fact]
    public async Task A_campus_under_way_blocks_the_request_level_edit_but_not_its_sibling()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var hn, var ct) = await CreateMixedAsync(
                VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit,
                Now.AddDays(20), Now.AddHours(-1));
            var (reqV, instV) = await VersionsAsync(requestId);

            // Request-level: refused. The registrant block is shared, and CT's delegation is already
            // on site reading that name.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                        new VisitRequestSafeEditDto(reqV,
                            new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84999999", "VN"),
                            new List<SafeInstancePatchDto>())), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
            }

            // Instance-level on HN: allowed. CT being under way says nothing about a campus 20 days out.
            using (var db = NewContext())
            {
                var res = await SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(hn, instV[hn], null, "Chuẩn bị phiên dịch.", null, null),
                    })), CancellationToken.None);
                Assert.Single(res.AppliedChanges);
                Assert.Equal(hn, res.AppliedChanges.Single().VisitInstanceId);
            }

            // CT is untouched — it was never in the payload.
            using (var db = NewContext())
            {
                var ctDetail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == ct);
                Assert.Equal(1u, ctDetail.FormRevision);
                var hnDetail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == hn);
                Assert.Equal(2u, hnDetail.FormRevision);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Editing_the_campus_that_is_under_way_is_refused_outright()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, _, var ct) = await CreateMixedAsync(
                VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit,
                Now.AddDays(20), Now.AddHours(-1));
            var (reqV, instV) = await VersionsAsync(requestId);

            // The headline defect, from the API side: calling the endpoint directly must be refused
            // exactly as the hidden button implies.
            using var db = NewContext();
            var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(ct, instV[ct], null, "Sửa khi đang diễn ra", null, null),
                    })), CancellationToken.None));
            Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Every_campus_finished_still_refuses_the_request_level_edit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // The exact hole in the old guard: it asked "is the earliest ACTIVE campus too close" and
            // read an EMPTY active set as nothing to object to, so once every campus had moved on the
            // shared registrant block became editable again.
            (requestId, _, _) = await CreateMixedAsync(
                VisitInstanceStatuses.DuringVisit, VisitInstanceStatuses.DuringVisit,
                Now.AddHours(-2), Now.AddHours(-1));
            var (reqV, _) = await VersionsAsync(requestId);

            using var db = NewContext();
            var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84777777", "VN"),
                        new List<SafeInstancePatchDto>())), CancellationToken.None));
            Assert.Equal(VisitRequestErrorCodes.VisitRequestNotEditable, ex.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task The_deadline_comes_from_the_earliest_campus_not_the_one_being_edited()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // HN is hours away, CT is weeks away. A request-level edit has to answer to HN.
            (requestId, _, _) = await CreateMixedAsync(
                VisitInstanceStatuses.Assigned, VisitInstanceStatuses.Assigned,
                Now.AddHours(VisitMutationPolicy.RequiredLeadHours - 1), Now.AddDays(30));
            var (reqV, _) = await VersionsAsync(requestId);

            using var db = NewContext();
            var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84555555", "VN"),
                        new List<SafeInstancePatchDto>())), CancellationToken.None));
            Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
            Assert.NotNull(ex.CampusName);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Capability agreement across a mixed request (§12/§13) ────────────────

    [Fact]
    public async Task A_mixed_request_offers_no_request_level_action_but_keeps_the_per_campus_one()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var hn, var ct) = await CreateMixedAsync(
                VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit,
                Now.AddDays(20), Now.AddHours(-1));

            using var db = NewContext();
            var reader = new VisitFormReadService(
                db, new FakeUser { UserId = Registrant }, NullLogger<VisitFormReadService>.Instance,
                new FixedClock(), WriteOn);
            var dto = await reader.ResolveAsync(requestId, CancellationToken.None);

            // Nothing at request level — only SOME campuses qualify, so there is no single answer.
            Assert.DoesNotContain(VisitFormActions.SubmitSafeEdit, dto.Viewer.AllowedActions);
            Assert.DoesNotContain(VisitFormActions.EditPendingRequest, dto.Viewer.AllowedActions);

            // HN keeps its own instance-scoped actions.
            var hnCampus = dto.CampusVisits.Single(c => (ulong)c.VisitInstanceId == hn);
            Assert.Contains(VisitFormActions.SubmitSafeEdit, hnCampus.AllowedActions);
            Assert.Contains(VisitFormActions.SubmitAmendment, hnCampus.AllowedActions);

            // CT offers nothing, and says why.
            var ctCampus = dto.CampusVisits.Single(c => (ulong)c.VisitInstanceId == ct);
            Assert.DoesNotContain(VisitFormActions.SubmitSafeEdit, ctCampus.AllowedActions);
            var ctSafe = ctCampus.Capabilities.Single(c => c.Code == VisitFormActions.SubmitSafeEdit);
            Assert.False(ctSafe.Enabled);
            Assert.Equal(VisitMutationErrorCodes.LifecycleNotAllowed, ctSafe.DisabledReasonCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task An_untouched_campus_keeps_its_revision_and_row_version()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var hn, var ct) = await CreateMixedAsync(
                VisitInstanceStatuses.Assigned, VisitInstanceStatuses.Assigned,
                Now.AddDays(20), Now.AddDays(21));
            var (reqV, instV) = await VersionsAsync(requestId);

            using (var db = NewContext())
                await SafeEdit(db).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV, null, new List<SafeInstancePatchDto>
                    {
                        new(hn, instV[hn], null, "Xe 45 chỗ", null, null),
                    })), CancellationToken.None);

            using (var db = NewContext())
            {
                var after = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId)
                    .ToDictionaryAsync(c => c.VisitInstanceId, c => c.RowVersion);
                // A campus nobody edited must not have its optimistic-concurrency token moved, or
                // every other client holding it would 409 for no reason.
                Assert.Equal(instV[ct], after[ct]);
                Assert.True(after[hn] > instV[hn]);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
