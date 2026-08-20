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
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Remediation plan 2026-08-20, Phase 2 (Issue B / Patch 2) — HR-1..HR-7.
///
/// <para><b>What was wrong.</b> The CREATE request-revision writer
/// (<c>VisitRequestV2CreateService</c>) hand-serialized its own anonymous object instead of going
/// through <see cref="VisitFormRevisionSnapshotBuilder.Request"/>, and it omitted
/// <c>RegistrantNationality</c> entirely. So revision 1 of every request never recorded a
/// nationality, and the FIRST edit's history diff reported "Không có dữ liệu lịch sử" for a field
/// that had simply never been written down — even though the exact old value existed all along in
/// that edit's own immutable <c>AuditLogChange</c> row.</para>
///
/// <para><b>Fix.</b> (1) CREATE now serializes through the same canonical builder as every other
/// writer (plan §7.1/7.2). (2) <c>GetVisitHistoryDetailQueryHandler.RequestRevisionDetailAsync</c>
/// recovers a BeforeUnknown field from the correlated AuditLogChange when — and only when — the
/// revision's <c>Reason</c> (a GUID minted once per write call) matches EXACTLY one AuditLog row
/// (plan §7.4): never nearest-timestamp, same-actor, or latest-audit guessing.</para>
/// </summary>
public sealed class VisitRequestRevisionSnapshotHistoryTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        private readonly ulong _id;
        public FakeUser(ulong id) => _id = id;
        public bool IsAuthenticated => true;
        public ulong? UserId => _id;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode => RoleCodes.Visitor;
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static SubmitVisitSafeEditCommandHandler SafeEditHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(), new VisitSafeEditService(db),
            new RecordingNotifications(), NullLogger<SubmitVisitSafeEditCommandHandler>.Instance, ReadOn, WriteOn);

    private static GetVisitHistoryDetailQueryHandler DetailHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), ReadOn);

    private static CampusVisitFormDto Campus(string code, DateTime start)
        => new(code, start, start.AddMinutes(120), "Đoàn Base", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);

    private static TimeSpan LeadTimeShift(IEnumerable<CampusVisitFormDto> campuses)
    {
        var earliest = campuses.Min(c => c.PlannedStartAt);
        var floor = Now.AddHours(PEMS.Domain.Policies.VisitMutationPolicy.MinScheduleLeadHours).AddMinutes(5);
        return earliest < floor ? floor - earliest : TimeSpan.Zero;
    }

    private static async Task RewindScheduleAsync(ulong requestId, TimeSpan by)
    {
        if (by == TimeSpan.Zero) return;
        using var db = NewContext();
        var instances = await db.VisitRequestCampuses.Where(c => c.VisitRequestId == requestId).ToListAsync();
        foreach (var instance in instances) { instance.PlannedStartAt -= by; instance.PlannedEndAt -= by; }
        await db.SaveChangesAsync();
    }

    private static async Task<ulong> CreateAsync(params CampusVisitFormDto[] campuses)
    {
        var shift = LeadTimeShift(campuses);
        using var db = NewContext();
        var handler = new CreateVisitRequestV2CommandHandler(
            db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
            new RecordingNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
            new UserProvisionService(db),
            NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
            new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
        var shifted = campuses.Select(c => c with { PlannedStartAt = c.PlannedStartAt + shift, PlannedEndAt = c.PlannedEndAt + shift }).ToList();
        var form = new VisitRequestFormDataV2(
            "HR" + Guid.NewGuid().ToString("N"),
            new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+84912345678", V2SeedActor.Email(Registrant)),
            null, shifted);
        var created = await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None);
        await RewindScheduleAsync(created.VisitRequestId, shift);
        return created.VisitRequestId;
    }

    /// <summary>Safe edit is a POST-decision correction — a still-pending campus belongs to
    /// pending-edit, so these tests must start from a decided campus to exercise it at all.</summary>
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
            instance.RowVersion += 1;
        }
        await db.SaveChangesAsync();
        visit.Status = VisitRequestStatuses.Approved;
        visit.RowVersion += 1;
        await db.SaveChangesAsync();
    }

    private static async Task<int> RequestVersionAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.VisitRequests.AsNoTracking()
            .Where(v => v.VisitRequestId == requestId).Select(v => v.RowVersion).SingleAsync();
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

    // ── HR-2 — CREATE snapshot contains nationality ─────────────────────────────────────────────

    [Fact]
    public async Task HR2_Create_snapshot_contains_nationality()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            using var db = NewContext();
            var revision1 = await db.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 1)
                .SingleAsync();
            // The DB stores snapshot_json as a native MySQL JSON column, which re-canonicalizes on
            // write (alphabetical keys, space after colon) — parse it rather than assume the exact
            // compact text C# originally serialized.
            using var doc = System.Text.Json.JsonDocument.Parse(revision1.SnapshotJson);
            // Patch 4: create resolves the seeded "VN" to its canonical Vietnamese short name.
            Assert.Equal("Việt Nam", doc.RootElement.GetProperty("registrantNationality").GetString());
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── HR-1 — create then first safe edit: 1 → 2 shows exact old/new for name+phone+nationality ─

    [Fact]
    public async Task HR1_Create_then_first_safe_edit_shows_exact_diff()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            var reqV = await RequestVersionAsync(requestId);

            using (var db = NewContext())
                await SafeEditHandler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant B", "Org", "Job", "+84987654321", "JP"), null)),
                    CancellationToken.None);

            using var check = NewContext();
            var rev2Id = await check.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2)
                .Select(r => r.RequestRevisionHistoryId).SingleAsync();
            var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, rev2Id);
            var detail = await DetailHandler(check, Registrant).Handle(
                new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None);

            Assert.Equal(1u, (uint)detail.BeforeRevision!);
            Assert.Equal(2u, (uint)detail.AfterRevision!);
            var nationality = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantNationality");
            Assert.False(nationality.BeforeUnknown);
            // Patch 4: both sides persist as canonical Vietnamese short names — "VN"/"JP" resolve on
            // write, so the diff never shows the raw codes the test sent.
            Assert.Equal("Việt Nam", nationality.BeforeValue);
            Assert.Equal("Nhật Bản", nationality.AfterValue);

            var name = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantFullName");
            Assert.Equal("Registrant", name.BeforeValue);
            Assert.Equal("Registrant B", name.AfterValue);

            var phone = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantPhone");
            Assert.Equal("+84912345678", phone.BeforeValue);
            Assert.Equal("+84987654321", phone.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── HR-4 — legacy partial snapshot + uniquely correlated audit recovers the exact old value ──

    [Fact]
    public async Task HR4_Legacy_partial_snapshot_with_unique_correlated_audit_recovers_old_value()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);

            // Simulate a LEGACY revision 1 that never recorded phone (the exact shape Issue B found in
            // production) — strip the key from the CREATE snapshot rather than editing SnapshotJson by
            // hand, so the fixture is the real created row minus one key, not an invented shape.
            using (var db = NewContext())
            {
                var rev1 = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 1).SingleAsync();
                rev1.SnapshotJson = System.Text.Json.Nodes.JsonNode.Parse(rev1.SnapshotJson)!.AsObject()
                    .Where(kv => kv.Key != "registrantPhone")
                    .Aggregate(new System.Text.Json.Nodes.JsonObject(), (o, kv) =>
                    {
                        o[kv.Key] = kv.Value?.DeepClone(); return o;
                    }).ToJsonString();
                await db.SaveChangesAsync();
            }

            var reqV = await RequestVersionAsync(requestId);
            using (var db = NewContext())
                await SafeEditHandler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84987654321", "VN"), null)),
                    CancellationToken.None);

            using var check = NewContext();
            var rev2Id = await check.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2)
                .Select(r => r.RequestRevisionHistoryId).SingleAsync();
            var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, rev2Id);
            var detail = await DetailHandler(check, Registrant).Handle(
                new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None);

            var phone = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantPhone");
            Assert.False(phone.BeforeUnknown);
            Assert.Equal("+84912345678", phone.BeforeValue); // recovered from AuditLogChange.OldValueText
            Assert.Equal("+84987654321", phone.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── HR-5/HR-7 — legacy partial snapshot with NO evidence stays unknown, never a fake blank ───

    [Fact]
    public async Task HR5_Legacy_partial_snapshot_without_evidence_stays_unknown()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);

            using (var db = NewContext())
            {
                var rev1 = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 1).SingleAsync();
                rev1.SnapshotJson = System.Text.Json.Nodes.JsonNode.Parse(rev1.SnapshotJson)!.AsObject()
                    .Where(kv => kv.Key != "registrantPhone")
                    .Aggregate(new System.Text.Json.Nodes.JsonObject(), (o, kv) =>
                    {
                        o[kv.Key] = kv.Value?.DeepClone(); return o;
                    }).ToJsonString();
                await db.SaveChangesAsync();
            }

            // This edit changes NATIONALITY only — phone is never touched, so no AuditLogChange for it
            // exists anywhere, correlated or not. There is no evidence to recover, legacy or otherwise.
            var reqV = await RequestVersionAsync(requestId);
            using (var db = NewContext())
                await SafeEditHandler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84912345678", "JP"), null)),
                    CancellationToken.None);

            using var check = NewContext();
            var rev2Id = await check.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2)
                .Select(r => r.RequestRevisionHistoryId).SingleAsync();
            var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, rev2Id);
            var detail = await DetailHandler(check, Registrant).Handle(
                new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None);

            // Revision 2's snapshot is a full snapshot (not a diff), so it carries phone's UNCHANGED
            // value regardless — and revision 1 never recorded the key at all, so the differ correctly
            // cannot tell whether it changed. This edit's own audit never touched phone either (only
            // nationality did), so there is nothing to recover it from: BeforeUnknown must stay true,
            // and the value must never be reported as a fabricated blank (HR-7).
            var phone = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantPhone");
            Assert.True(phone.BeforeUnknown);
            Assert.Null(phone.BeforeValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task HR5b_Field_missing_from_every_source_stays_BeforeUnknown_not_a_fake_blank()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);

            using (var db = NewContext())
            {
                var rev1 = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 1).SingleAsync();
                rev1.SnapshotJson = System.Text.Json.Nodes.JsonNode.Parse(rev1.SnapshotJson)!.AsObject()
                    .Where(kv => kv.Key != "registrantPhone")
                    .Aggregate(new System.Text.Json.Nodes.JsonObject(), (o, kv) =>
                    {
                        o[kv.Key] = kv.Value?.DeepClone(); return o;
                    }).ToJsonString();
                await db.SaveChangesAsync();
            }

            // The edit DOES change phone, but its AuditLogChange is then deleted — simulating a revision
            // whose correlated audit no longer carries the field (e.g. an older writer that did not audit
            // every safe field). No reliable immutable evidence exists anywhere.
            var reqV = await RequestVersionAsync(requestId);
            using (var db = NewContext())
                await SafeEditHandler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84987654321", "VN"), null)),
                    CancellationToken.None);
            using (var db = NewContext())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id " +
                    "WHERE al.visit_request_id = {0} AND alc.field_name = 'request.registrant.phone'", requestId);
            }

            using var check = NewContext();
            var rev2Id = await check.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2)
                .Select(r => r.RequestRevisionHistoryId).SingleAsync();
            var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, rev2Id);
            var detail = await DetailHandler(check, Registrant).Handle(
                new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None);

            var phone = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantPhone");
            Assert.True(phone.BeforeUnknown);
            Assert.Null(phone.BeforeValue); // never a fabricated empty string
            Assert.Equal("+84987654321", phone.AfterValue);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── HR-6 — ambiguous audit correlation: no recovery ─────────────────────────────────────────

    [Fact]
    public async Task HR6_Ambiguous_audit_correlation_skips_recovery()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            requestId = await CreateAsync(Campus("HN", Now.AddDays(20)));
            await ApproveAllAsync(requestId);
            using (var db = NewContext())
            {
                var rev1 = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 1).SingleAsync();
                rev1.SnapshotJson = System.Text.Json.Nodes.JsonNode.Parse(rev1.SnapshotJson)!.AsObject()
                    .Where(kv => kv.Key != "registrantPhone")
                    .Aggregate(new System.Text.Json.Nodes.JsonObject(), (o, kv) =>
                    {
                        o[kv.Key] = kv.Value?.DeepClone(); return o;
                    }).ToJsonString();
                await db.SaveChangesAsync();
            }

            var reqV = await RequestVersionAsync(requestId);
            using (var db = NewContext())
                await SafeEditHandler(db, Registrant).Handle(new SubmitVisitSafeEditCommand(requestId,
                    new VisitRequestSafeEditDto(reqV,
                        new SafeRegistrantPatchDto("Registrant", "Org", "Job", "+84987654321", "VN"), null)),
                    CancellationToken.None);

            // Corrupt the correlation deterministically: add a SECOND AuditLog row carrying the exact
            // same correlation id the real writer minted. The id can no longer uniquely identify one
            // call's facts, so recovery must refuse rather than pick one of the two arbitrarily.
            using (var db = NewContext())
            {
                var rev2 = await db.VisitRequestRevisionHistories
                    .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2).SingleAsync();
                db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = Registrant, Action = "UNRELATED_EVENT", EntityType = "VisitRequest",
                    EntityId = requestId, VisitRequestId = requestId, CorrelationId = rev2.Reason,
                    SourceType = "SAFE_EDIT", CreatedAt = Now,
                });
                await db.SaveChangesAsync();
            }

            using var check = NewContext();
            var rev2Id = await check.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == requestId && r.RequestRevision == 2)
                .Select(r => r.RequestRevisionHistoryId).SingleAsync();
            var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, rev2Id);
            var detail = await DetailHandler(check, Registrant).Handle(
                new GetVisitHistoryDetailQuery(requestId, eventId), CancellationToken.None);

            var phone = Assert.Single(detail.FieldChanges, f => f.FieldCode == "registrantPhone");
            Assert.True(phone.BeforeUnknown);
            Assert.Null(phone.BeforeValue);
        }
        finally { await CleanupAsync(requestId); }
    }
}
