using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// The scoped history timeline. These exist because the previous shape shipped assembled audit strings
/// ("source=CREATE;approvalRevision=1", "Cơ sở: REJECTED") straight to whoever opened the page, and
/// because the handler built an actor-name dictionary and then never applied it — so every entry went
/// out with ActorName = null and nothing failed. Both are the kind of defect only a test catches.
///
/// Seed ids in pems_pr3_test: visitor owner = 8, Staff Leader campus1 = 3, Staff Leader campus2 = 9,
/// IC Staff campus1 (host) = 4, HO = 2.
/// </summary>
public sealed class VisitRequestHistoryV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong VisitorOwner = 8, SlCampus1 = 3, SlCampus2 = 9, IcStaffC1 = 4, HoUser = 2;
    /// <summary>ACTIVE Student — invited to SUPPORT a campus, and nothing more than that.</summary>
    private const ulong SupportingStudent = 152;
    /// <summary>ACTIVE IC Staff who hosts nothing in this fixture — a pure supporting participant.</summary>
    private const ulong IcStaffSupporter = 101;
    private const ulong Campus1 = 1, Campus2 = 2;

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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable — import the PR-2 master to run these tests.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Owner() => new() { UserId = VisitorOwner, RoleCode = RoleCodes.Visitor };
    private static FakeUser Ho() => new() { UserId = HoUser, RoleCode = RoleCodes.Ho };
    private static FakeUser StaffLeader(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };
    private static FakeUser Host(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };
    private static FakeUser Student(ulong userId, ulong campusId) => new()
        { UserId = userId, RoleCode = RoleCodes.Student, PrimaryCampusId = campusId };

    private static GetVisitRequestHistoryQueryHandler Handler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    private static GetVisitHistoryDetailQueryHandler DetailHandler(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new PerCampusFormV2Options { Enabled = true });

    /// <summary>
    /// The V2 detail read model — the OTHER half of every capability test here. The capability and the
    /// endpoint have to be asked in the same test, because the bug being locked down is precisely that
    /// they disagreed.
    /// </summary>
    private static VisitFormReadService Resolver(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, NullLogger<VisitFormReadService>.Instance);

    /// <summary>Two campuses, campus 1 decided (approved + hosted) with a note, campus 2 still waiting.</summary>
    private static async Task<(VisitRequest Request, List<VisitRequestCampus> Instances)> SeedAsync(ApplicationDbContext db)
    {
        var now = DateTime.Now;
        var req = new VisitRequest
        {
            RequestCode = "HIST-" + Guid.NewGuid().ToString("N")[..12],
            RegistrantUserId = VisitorOwner,
            CreatedSource = "VISITOR_SUBMITTED",
            HasMixedCampusDetails = true,
            RegistrantFullName = "Reg", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
            RegistrantPhone = "+8490", RegistrantEmail = "reg@example.com", RegistrantNationality = "VN",
            VisitScope = "MULTI_CAMPUS",
            Status = "PARTIALLY_APPROVED", SubmittedAt = now, CreatedAt = now,
        };

        foreach (var (campusId, host) in new[] { (Campus1, (ulong?)IcStaffC1), (Campus2, (ulong?)null) })
        {
            req.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campusId,
                PlannedStartAt = now.AddDays(20),
                PlannedEndAt = now.AddDays(20).AddHours(2),
                Status = host is null ? "WAITING_REQUEST_APPROVAL" : "ASSIGNED",
                // Self-matched: the registrant is each campus's operational contact, so both campuses sit
                // past the confirmation gate. A campus beyond WAITING_CONTACT_CONFIRMATION with no
                // contact is refused by trg_visit_campuses_op_contact_guard_bi.
                OperationalContactUserId = VisitorOwner,
                OperationalContactConfirmedAt = now,
                OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
                CurrentHostUserId = host,
                HostAssignedBy = host is null ? null : SlCampus1,
                HostAssignedAt = host is null ? null : now,
                DecidedBy = host is null ? null : SlCampus1,
                DecidedAt = host is null ? null : now,
                DecisionActorRole = host is null ? null : "STAFF_LEADER",
                DecisionSource = host is null ? null : "STANDARD_CAMPUS_REVIEW",
                DecisionNote = host is null ? null : "Tiếp nhận bình thường",
                CreatedAt = now,
                FormDetail = new VisitInstanceFormDetail
                {
                    DelegationName = $"DELEG-{campusId}", VisitType = "MEETING", Purpose = "P",
                    WorkingContent = "C",
                    OperationalContactFullName = "Op", OperationalContactOrganization = "OpOrg",
                    OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                    OperationalContactPhone = "+8410", OperationalContactEmail = "op@example.com",
                    WorkingLanguage = "VI", MediaConsentStatus = "AGREED",
                    FormRevision = 1, ApprovalRevision = 1, CreatedAt = now,
                },
            });
        }

        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();

        // Every campus gets a CREATE revision when the request is written, so seed one each — otherwise
        // a campus with no decision produces no timeline entry at all and the fixture is unrealistic.
        foreach (var inst in ordered)
        {
            db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = inst.VisitInstanceId,
                FormRevision = 1,
                ApprovalRevision = 1,
                SourceType = "CREATE",
                // The snapshot is what the row archives; the timeline never reads or exposes it.
                SnapshotJson = "{}",
                AppliedBy = VisitorOwner,
                AppliedAt = now,
            });
        }
        await db.SaveChangesAsync();

        return (req, ordered);
    }

    // ── Structure, not prose ─────────────────────────────────────────────────

    [Fact]
    public async Task Decision_entries_carry_the_actor_name_the_handler_looked_up()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var decision = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.InstanceApproved));
        // The name dictionary used to be built and then dropped on the floor.
        Assert.False(string.IsNullOrWhiteSpace(decision.ActorName));
        Assert.Equal("Tiếp nhận bình thường", decision.Reason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Decision_entries_name_their_campus_so_multi_campus_rows_differ()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var perInstance = result.Entries
            .Where(e => e.VisitInstanceId != null)
            .GroupBy(e => e.VisitInstanceId!.Value)
            .ToDictionary(g => g.Key, g => g.First().CampusName);

        Assert.Equal(2, perInstance.Count);
        Assert.All(perInstance.Values, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        // Two campuses, two DIFFERENT names — otherwise the rows are indistinguishable to a reader.
        Assert.Equal(2, perInstance.Values.Distinct().Count());
        Assert.Contains(instances[0].VisitInstanceId, perInstance.Keys);
        await tx.RollbackAsync();
    }

    // ── VISIT_HISTORY_INTEGRITY plan, Fix Group B §D — legacy fallback ────────────────────────────

    /// <summary>
    /// Case B7. SeedAsync already leaves campus 1 ASSIGNED with DecidedAt/DecidedBy/DecisionNote set
    /// directly on the row and NO audit_logs row — the exact legacy shape (data older than the
    /// immutable-audit capture) the fallback in GetVisitRequestHistoryQueryHandler exists for.
    /// </summary>
    [Fact]
    public async Task B7_Legacy_decision_with_no_immutable_audit_still_produces_one_history_entry()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        Assert.False(await db.AuditLogs.AnyAsync(a => a.VisitInstanceId == instances[0].VisitInstanceId
            && a.SourceType == CampusDecisionAudit.SourceType));

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var approved = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved
            && e.VisitInstanceId == instances[0].VisitInstanceId).ToList();
        Assert.Single(approved);
        Assert.Equal("Tiếp nhận bình thường", approved[0].Reason);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case B8. Same legacy current-row metadata as B7, but this instance ALSO has a canonical
    /// immutable decision audit (as every decision would going forward under Fix Group B). The two
    /// must never both surface — an instance with an immutable audit is covered by it alone.
    /// </summary>
    [Fact]
    public async Task B8_An_instance_with_both_legacy_metadata_and_an_immutable_audit_reports_only_the_audit()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var decided = instances[0];

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus1,
            Action = CampusDecisionAudit.Approved,
            EntityType = "VisitRequestCampus",
            EntityId = decided.VisitInstanceId,
            CampusId = decided.CampusId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = decided.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = $"decision=ASSIGNED;host={IcStaffC1}",
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var approved = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved
            && e.VisitInstanceId == decided.VisitInstanceId).ToList();
        Assert.Single(approved); // never both the audit AND the legacy current-row fallback
        await tx.RollbackAsync();
    }

    // ── Legacy-audit enrichment continuation (plan Cases C2-L1..L5) ───────────────────────────────
    //
    // B7/B8 covered "no audit at all" (pure legacy) and "audit + matching current row, no duplicate".
    // These cover the sharper case: an audit ROW EXISTS (written by pre-Fix-Group-B code, so it has
    // no AuditLogChange rows of its own) and the reader must decide, per-field, whether the CURRENT
    // row is still describing that exact decision before borrowing its DecisionNote — never by
    // proximity or by "a decision of this kind exists somewhere".

    [Fact]
    public async Task C2_L1_Legacy_rejected_audit_with_current_decision_still_intact_is_enriched()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1]; // WAITING_REQUEST_APPROVAL in the base fixture
        var decidedAt = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = decidedAt;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Legacy reason";
        await db.SaveChangesAsync();

        // The legacy shape: an audit row exists (action + actor + timestamp) but carries NO
        // AuditLogChange rows — exactly what CampusApprovalExecutor/RejectCampusInstanceCommandHandler
        // wrote before Fix Group B's structured capture existed.
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2,
            Action = CampusDecisionAudit.Rejected,
            EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId,
            CampusId = target.CampusId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED",
            CreatedAt = decidedAt, // same instant as the current row — exactly what the real writers do
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(rejected);
        Assert.Equal("Legacy reason", entry.Reason);
        Assert.NotNull(entry.EventId); // still audit-backed — not the standalone current-row fallback

        var detail = await DetailHandler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, entry.EventId!), CancellationToken.None);
        Assert.Equal("Legacy reason", detail.Reason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task C2_L2_Legacy_approved_audit_with_current_decision_still_intact_is_enriched()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0]; // already ASSIGNED, DecisionNote="Tiếp nhận bình thường"

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = target.DecidedBy!.Value,
            Action = CampusDecisionAudit.Approved,
            EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId,
            CampusId = target.CampusId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            SourceType = CampusDecisionAudit.SourceType,
            Reason = $"decision=ASSIGNED;host={target.CurrentHostUserId}",
            CreatedAt = target.DecidedAt!.Value,
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var approved = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceApproved
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(approved);
        Assert.Equal("Tiếp nhận bình thường", entry.Reason);

        var detail = await DetailHandler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, entry.EventId!), CancellationToken.None);
        Assert.Equal("Tiếp nhận bình thường", detail.Reason);
        // Host old→new is NOT invented here: the writer's current_host_user_id AuditLogChange is
        // exactly the piece this legacy row is missing, and there is no "before" value recoverable
        // from the current row (it only ever holds the CURRENT host, never a history of hosts) — so
        // this stays absent rather than guessed.
        Assert.DoesNotContain(detail.FieldChanges, f => f.FieldCode == "host");
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-L3. Simulates two decision cycles on the SAME instance: an old rejection with a
    /// legacy (change-less) audit, then the current row moving on to a SECOND, unrelated decision at
    /// a different instant. The legacy audit must never borrow the second cycle's reason.
    /// </summary>
    [Fact]
    public async Task C2_L3_Legacy_audit_is_never_enriched_from_a_different_decision_cycle()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];

        var t1 = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Lý do lần 1 (legacy)";
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        // A second cycle overwrites the current row — different instant, different reason. No new
        // audit is added for it (this test's whole point is what happens to the OLD one).
        target.DecidedAt = t1.AddMinutes(5);
        target.DecisionNote = "Lý do lần 2 (không liên quan tới audit cũ)";
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(rejected); // one audit row → one event, current row is not a second one
        Assert.Null(entry.Reason); // timestamps disagree, so nothing is borrowed
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task C2_L4_Legacy_audit_survives_a_resubmit_without_inventing_a_reason()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Lý do cũ (legacy, không lưu trong audit)";
        await db.SaveChangesAsync();
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        // Resubmit clears the current row exactly like VisitRequestV2EditService's real resubmit path.
        target.Status = VisitInstanceStatuses.WaitingRequestApproval;
        target.DecidedBy = null;
        target.DecidedAt = null;
        target.DecisionActorRole = null;
        target.DecisionNote = null;
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(rejected); // the audit event survives the resubmit
        Assert.Null(entry.Reason); // nothing left to enrich from — never invented
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-L5. The current row deliberately holds a DIFFERENT note than the canonical audit, to
    /// prove the reader always prefers the audit's own immutable capture and never lets a later,
    /// possibly-mutated current value override it — canonical audits never need enrichment at all.
    /// </summary>
    [Fact]
    public async Task C2_L5_A_canonical_audit_with_its_own_note_is_never_overridden_by_current_row()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Giá trị hiện tại KHÁC (không được dùng)";
        await db.SaveChangesAsync();

        var audit = new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = VisitInstanceStatuses.WaitingRequestApproval, NewValueText = VisitInstanceStatuses.Rejected,
            CreatedAt = t1,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "decision_note", OldValueText = null,
            NewValueText = "Lý do canonical đã ghi lúc quyết định", CreatedAt = t1,
        });
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(rejected);
        Assert.Equal("Lý do canonical đã ghi lúc quyết định", entry.Reason);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-S1. MySQL DATETIME is whole-second precision, so two DIFFERENT legacy rejections on the
    /// same instance, by the same actor, can land in the very same second (reject → resubmit → reject
    /// again, all within one second). Both audits then satisfy CurrentRowMatchesAuditedDecision equally
    /// — the current row cannot prove which one it reflects, so NEITHER may borrow its DecisionNote.
    /// Unknown beats a coin-flip guess.
    /// </summary>
    [Fact]
    public async Task C2_S1_Same_second_same_actor_duplicate_rejection_is_never_enriched()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "reason #2";
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        Assert.Equal(2, rejected.Count);
        Assert.All(rejected, e => Assert.Null(e.Reason));
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-S2. Regression guard for the uniqueness fix itself: a SINGLE legacy audit matching the
    /// current row's tuple must still be enriched — the ambiguity check must not turn into a blanket
    /// refusal. Same fixture shape as C2-L1, run through the uniqueness-aware helper.
    /// </summary>
    [Fact]
    public async Task C2_S2_Unique_same_second_candidate_is_still_enriched()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Unique legacy reason";
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        var entry = Assert.Single(rejected);
        Assert.Equal("Unique legacy reason", entry.Reason);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-S3. A canonical audit (own decision_note captured) is unaffected by the mere presence of
    /// a same-timestamp/same-actor legacy sibling — its reason always comes from its own AuditLogChange
    /// row, never from the current row, so the ambiguity question never even applies to it.
    /// </summary>
    [Fact]
    public async Task C2_S3_Canonical_audit_unaffected_by_a_same_second_legacy_sibling()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Giá trị hiện tại (không liên quan)";
        await db.SaveChangesAsync();

        // Legacy sibling — same second, same actor, no decision_note of its own.
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        // Canonical audit — same second, same actor, own decision_note.
        var canonical = new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        };
        canonical.Changes.Add(new AuditLogChange
        {
            FieldName = "decision_note", OldValueText = null,
            NewValueText = "Lý do canonical riêng", CreatedAt = t1,
        });
        db.AuditLogs.Add(canonical);
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var rejected = result.Entries.Where(e => e.EventCode == VisitHistoryEventCodes.InstanceRejected
            && e.VisitInstanceId == target.VisitInstanceId).ToList();
        Assert.Equal(2, rejected.Count);
        Assert.Contains(rejected, e => e.Reason == "Lý do canonical riêng");
        Assert.Contains(rejected, e => e.Reason is null);
        await tx.RollbackAsync();
    }

    /// <summary>Case C2-S4. Same ambiguity as C2-S1, asserted through the detail endpoint.</summary>
    [Fact]
    public async Task C2_S4_Detail_endpoint_reports_null_reason_for_an_ambiguous_same_second_pair()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;

        var audit1 = new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        };
        db.AuditLogs.Add(audit1);
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "reason #2";
        await db.SaveChangesAsync();

        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit1.AuditLogId);
        var detail = await DetailHandler(db, Owner()).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);
        Assert.Null(detail.Reason);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C2-S5. An approval audit and a rejection audit share the exact same timestamp/actor (a
    /// contrived same-second tie), but the current row can only be in ONE status. Only the outcome
    /// class the current row actually matches may be treated as a candidate — the other decision type
    /// must never be enriched from a status it does not describe.
    /// </summary>
    [Fact]
    public async Task C2_S5_Different_decision_types_same_second_only_the_matching_outcome_is_a_candidate()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[1];
        var t1 = DateTime.Now;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Approved, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=ASSIGNED", CreatedAt = t1,
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = SlCampus2, Action = CampusDecisionAudit.Rejected, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = "decision=REJECTED", CreatedAt = t1,
        });
        await db.SaveChangesAsync();

        // Current row is REJECTED — only the rejection audit's outcome class matches it.
        target.Status = VisitInstanceStatuses.Rejected;
        target.DecidedBy = SlCampus2;
        target.DecidedAt = t1;
        target.DecisionActorRole = "STAFF_LEADER";
        target.DecisionNote = "Chỉ áp dụng cho reject";
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var forInstance = result.Entries.Where(e => e.VisitInstanceId == target.VisitInstanceId
            && (e.EventCode == VisitHistoryEventCodes.InstanceRejected
                || e.EventCode == VisitHistoryEventCodes.InstanceApproved)).ToList();
        Assert.Equal(2, forInstance.Count);
        var rejectedEntry = Assert.Single(forInstance, e => e.EventCode == VisitHistoryEventCodes.InstanceRejected);
        var approvedEntry = Assert.Single(forInstance, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved);
        Assert.Equal("Chỉ áp dụng cho reject", rejectedEntry.Reason);
        Assert.Null(approvedEntry.Reason);
        await tx.RollbackAsync();
    }

    // ── Commit 3 — Contact History Integrity (Fix Group C/D), reader regression + privacy ─────────
    //
    // C3-1..C3-6 (the writer-triggered outcomes: profile update, external replace, self-match
    // replace) live in OperationalContactManagementTests.cs, which already has the real-handler
    // harness they need. These cover what is specific to THIS reader: that the pre-existing
    // identity-change-event types still surface unchanged after Commit 3 touched the same query
    // (C3-7/C3-8), and that the identity/contact-profile visibility policy is exactly as strict for
    // the new audit-backed events as it always was for the VisitRequestIdentityChangeEvent-backed
    // ones (C3-9..C3-13).

    /// <summary>A user distinct from the registrant/HO/Staff Leaders/Hosts above — an external person
    /// holding ONE campus's operational-contact role, for the multi-campus isolation case.</summary>
    private const ulong ContactCampus1 = 201;

    private static async Task<AuditLog> SeedProfileUpdateAuditAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus target, ulong actorId)
    {
        var now = DateTime.Now;
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = OperationalContactHistoryAudit.ProfileUpdated,
            EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId,
            CampusId = target.CampusId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            SourceType = OperationalContactHistoryAudit.SourceType,
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "operational_contact_phone", OldValueText = "+8410", NewValueText = "+8499",
            CreatedAt = now,
        });
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        return audit;
    }

    /// <summary>
    /// Hand-builds a TRANSFER identity-change row to hang a pending/applied event off, matching the
    /// shape InitiateOperationalContactTransferCommandHandler/AcceptOperationalContactConfirmation
    /// CommandHandler actually write (old_user_id required for TRANSFER by trg_ivc_bi/trg_ivc_bu).
    /// </summary>
    private static async Task<VisitRequestIdentityChange> SeedTransferAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus target,
        string status, DateTime now, ulong? newUserId = null, DateTime? appliedAt = null)
    {
        var transfer = new VisitRequestIdentityChange
        {
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            ChangeKind = IdentityChangeKinds.Transfer,
            TokenVersion = 1,
            ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
            OldUserId = target.OperationalContactUserId,
            NewUserId = newUserId,
            NewEmailNormalized = "newcontact@example.com",
            NewEmailMasked = "n***@example.com",
            Status = status,
            ExpectedRequestRowVersion = (uint)target.RowVersion,
            RequestedBy = target.OperationalContactUserId!.Value,
            RequestedAt = now,
            ExpiresAt = now.AddHours(24),
            AppliedAt = appliedAt,
            ResendCount = 0,
            CreatedAt = now,
        };
        db.VisitRequestIdentityChanges.Add(transfer);
        await db.SaveChangesAsync(); // resolve the id its event points at
        return transfer;
    }

    /// <summary>Case C3-7. Transfer-requested is untouched by Commit 3 and must still surface.</summary>
    [Fact]
    public async Task C3_7_Transfer_requested_still_produces_its_existing_event()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0]; // ASSIGNED, contact = VisitorOwner
        var now = DateTime.Now;

        var transfer = await SeedTransferAsync(db, req, target, IdentityChangeStatuses.Pending, now);
        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = transfer.IdentityChangeId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
            FromStatus = null,
            ToStatus = IdentityChangeStatuses.Pending,
            ActorUserId = VisitorOwner,
            EmailMasked = "n***@example.com",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactTransferRequested
            && e.VisitInstanceId == target.VisitInstanceId);
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-8. Transfer-accepted is untouched by Commit 3, and applying it never bumps FormRevision.</summary>
    [Fact]
    public async Task C3_8_Transfer_accepted_still_produces_its_existing_event_and_formrevision_unchanged()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var now = DateTime.Now;
        var revisionBefore = target.FormDetail!.FormRevision;

        var transfer = await SeedTransferAsync(
            db, req, target, IdentityChangeStatuses.Applied, now, newUserId: SupportingStudent, appliedAt: now);
        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = transfer.IdentityChangeId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_TRANSFER_APPLIED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Applied,
            ActorUserId = SupportingStudent,
            EmailMasked = "n***@example.com",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactTransferAccepted
            && e.VisitInstanceId == target.VisitInstanceId);

        var detailAfter = await db.VisitInstanceFormDetails.AsNoTracking()
            .FirstAsync(d => d.VisitInstanceId == target.VisitInstanceId);
        Assert.Equal(revisionBefore, detailAfter.FormRevision);
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-9. A Staff Leader who can read their campus's decisions must not gain contact-profile history.</summary>
    [Fact]
    public async Task C3_9_Staff_leader_cannot_see_contact_profile_history_or_open_it_by_guessed_id()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0]; // campus 1
        var audit = await SeedProfileUpdateAuditAsync(db, req, target, VisitorOwner);

        var result = await Handler(db, StaffLeader(SlCampus1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);

        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, StaffLeader(SlCampus1, Campus1))
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-10. Same refusal for the current Host of the campus.</summary>
    [Fact]
    public async Task C3_10_Host_cannot_see_contact_profile_history_or_open_it_by_guessed_id()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0]; // hosted by IcStaffC1
        var audit = await SeedProfileUpdateAuditAsync(db, req, target, VisitorOwner);

        var result = await Handler(db, Host(IcStaffC1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);

        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, Host(IcStaffC1, Campus1))
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-11. The operational contact of campus A sees campus A's contact history and nothing of campus B's.</summary>
    [Fact]
    public async Task C3_11_Contact_of_campus_A_sees_own_campus_history_but_not_campus_Bs()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0];
        var campusB = instances[1];
        campusA.OperationalContactUserId = ContactCampus1;
        await db.SaveChangesAsync();

        var auditA = await SeedProfileUpdateAuditAsync(db, req, campusA, ContactCampus1);
        var auditB = await SeedProfileUpdateAuditAsync(db, req, campusB, VisitorOwner);

        var contactUser = new FakeUser { UserId = ContactCampus1, RoleCode = RoleCodes.Visitor };
        var result = await Handler(db, contactUser).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        Assert.Contains(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == campusB.VisitInstanceId);

        var eventIdB = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditB.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, contactUser)
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventIdB), CancellationToken.None));

        var eventIdA = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditA.AuditLogId);
        var drawer = await DetailHandler(db, contactUser).Handle(
            new GetVisitHistoryDetailQuery(req.VisitRequestId, eventIdA), CancellationToken.None);
        Assert.Equal(VisitHistoryEventCodes.ContactProfileUpdated, drawer.EventCode);
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-12. The registrant and HO — both already entitled to identity history — still see it.</summary>
    [Fact]
    public async Task C3_12_Registrant_and_HO_see_contact_profile_history()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var audit = await SeedProfileUpdateAuditAsync(db, req, target, VisitorOwner);
        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit.AuditLogId);

        foreach (var actor in new[] { Owner(), Ho() })
        {
            var result = await Handler(db, actor).Handle(
                new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
            Assert.Contains(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated
                && e.VisitInstanceId == target.VisitInstanceId);

            var drawer = await DetailHandler(db, actor).Handle(
                new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.ContactProfileUpdated, drawer.EventCode);
        }
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-13. A profile update, a transfer request and its acceptance never touch FormRevision.</summary>
    [Fact]
    public async Task C3_13_Contact_events_never_bump_formrevision()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var revisionBefore = target.FormDetail!.FormRevision;
        var now = DateTime.Now;

        await SeedProfileUpdateAuditAsync(db, req, target, VisitorOwner);
        var transfer = await SeedTransferAsync(
            db, req, target, IdentityChangeStatuses.Applied, now, newUserId: VisitorOwner, appliedAt: now);
        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = transfer.IdentityChangeId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_TRANSFER_APPLIED",
            FromStatus = IdentityChangeStatuses.Pending,
            ToStatus = IdentityChangeStatuses.Applied,
            ActorUserId = VisitorOwner,
            EmailMasked = "n***@example.com",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var detailAfter = await db.VisitInstanceFormDetails.AsNoTracking()
            .FirstAsync(d => d.VisitInstanceId == target.VisitInstanceId);
        Assert.Equal(revisionBefore, detailAfter.FormRevision);
        await tx.RollbackAsync();
    }

    // ── Reinvite-vs-Created semantic-fix patch — privacy/scope regression ─────────────────────────
    //
    // Reinvite is still identity-event-sourced (VisitRequestIdentityChangeEvents), the same table
    // every other contact event in this section reads from — this patch changed only the EventType
    // string one writer stores, not the reader's scoping. These two tests exist to prove that
    // holds: the privacy/multi-campus mechanics were never touched, only re-confirmed.

    private static async Task<VisitRequestIdentityChange> SeedReinviteAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus target, DateTime now)
    {
        var reinvite = new VisitRequestIdentityChange
        {
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            ChangeKind = IdentityChangeKinds.InitialConfirmation,
            TokenVersion = 1,
            ConfirmationMethod = IdentityConfirmationMethods.GoogleSso,
            OldUserId = null,
            NewUserId = null,
            NewEmailNormalized = "reinvited@example.com",
            NewEmailMasked = "r***d@example.com",
            Status = IdentityChangeStatuses.Pending,
            ExpectedRequestRowVersion = (uint)target.RowVersion,
            RequestedBy = VisitorOwner,
            RequestedAt = now,
            ExpiresAt = now.AddHours(72),
            ResendCount = 0,
            CreatedAt = now,
        };
        db.VisitRequestIdentityChanges.Add(reinvite);
        await db.SaveChangesAsync(); // resolve the id its event points at

        db.VisitRequestIdentityChangeEvents.Add(new VisitRequestIdentityChangeEvent
        {
            IdentityChangeId = reinvite.IdentityChangeId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            EventType = "OPERATIONAL_CONTACT_REINVITED",
            FromStatus = null,
            ToStatus = IdentityChangeStatuses.Pending,
            ActorUserId = VisitorOwner,
            EmailMasked = "r***d@example.com",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return reinvite;
    }

    /// <summary>Case C3-R8. Reinvite is identity data like every other contact event — Staff Leader/Host stay refused.</summary>
    [Fact]
    public async Task C3_R8_Staff_leader_and_host_cannot_see_reinvite_history_or_open_it_by_guessed_id()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0]; // campus 1, hosted by IcStaffC1
        var now = DateTime.Now;
        var reinvite = await SeedReinviteAsync(db, req, target, now);
        var eventRow = await db.VisitRequestIdentityChangeEvents.AsNoTracking()
            .FirstAsync(e => e.IdentityChangeId == reinvite.IdentityChangeId);
        var eventId = VisitHistoryEventSources.Build(
            VisitHistoryEventSources.IdentityChange, eventRow.IdentityChangeEventId);

        foreach (var actor in new ICurrentUserService[] { StaffLeader(SlCampus1, Campus1), Host(IcStaffC1, Campus1) })
        {
            var result = await Handler(db, actor).Handle(
                new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
            Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReinvited);

            await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, actor)
                .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));
        }
        await tx.RollbackAsync();
    }

    /// <summary>Case C3-R9. A reinvite on campus A must not leak to a viewer scoped only to campus B.</summary>
    [Fact]
    public async Task C3_R9_Reinvite_on_campus_A_does_not_leak_to_a_viewer_scoped_to_campus_B()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0];
        var campusB = instances[1];
        campusB.OperationalContactUserId = ContactCampus1;
        await db.SaveChangesAsync();

        var now = DateTime.Now;
        var reinviteA = await SeedReinviteAsync(db, req, campusA, now);

        var contactUserOfB = new FakeUser { UserId = ContactCampus1, RoleCode = RoleCodes.Visitor };
        var result = await Handler(db, contactUserOfB).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == campusA.VisitInstanceId);

        var eventRow = await db.VisitRequestIdentityChangeEvents.AsNoTracking()
            .FirstAsync(e => e.IdentityChangeId == reinviteA.IdentityChangeId);
        var eventId = VisitHistoryEventSources.Build(
            VisitHistoryEventSources.IdentityChange, eventRow.IdentityChangeEventId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, contactUserOfB)
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    // ── Commit 4 — Lifecycle History Integrity (Fix Group F), scope/privacy/legacy regression ─────
    //
    // C4-1..C4-5, C4-11, C4-12, C4-D1..D3 (the writer-triggered outcomes, run through the REAL
    // CompleteVisitStageCommandHandler chain) live in CompleteVisitStageV2Tests.cs, which has the
    // create→approve→start-prep→agenda harness they need. These cover what is specific to THIS
    // reader: multi-campus isolation, role-based scope (Staff Leader/Host/registrant/HO/operational
    // contact), coexistence with decision/contact events, cancellation non-collapse, and — the
    // commit's central rule — that a legacy CLOSED campus with no lifecycle audit never gets a
    // fabricated chain of events invented from its current status.

    private static async Task<AuditLog> SeedLifecycleAuditAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus target,
        string action, string oldStatus, string newStatus, ulong actorId, DateTime now)
    {
        var audit = new AuditLog
        {
            ActorUserId = actorId,
            Action = action,
            EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId,
            CampusId = target.CampusId,
            VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId,
            SourceType = VisitLifecycleHistoryAudit.SourceType,
            CreatedAt = now,
        };
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "visit_request_campuses.status",
            OldValueText = oldStatus, NewValueText = newStatus, CreatedAt = now,
        });
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        return audit;
    }

    /// <summary>Case C4-6. A lifecycle event on campus A never appears for campus B.</summary>
    [Fact]
    public async Task C4_6_Lifecycle_event_on_campus_A_does_not_appear_for_campus_B()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0];
        var campusB = instances[1];
        var now = DateTime.Now;
        await SeedLifecycleAuditAsync(db, req, campusA, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, now);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
            && e.VisitInstanceId == campusB.VisitInstanceId);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case A-C4-6 (VISIT_HISTORY_INTEGRITY final phase, Phase A). The ASSIGNED → BEFORE_VISIT
    /// preparation event follows the exact same campus-scope rule the three CompleteVisitStage
    /// transitions already proved in C4-6: it never appears for a viewer scoped to a different campus,
    /// and a guessed EventId for campus B's own preparation event is refused as not-found, not leaked.
    /// </summary>
    [Fact]
    public async Task A_C4_6_Preparation_event_on_campus_A_does_not_leak_to_campus_B_and_guessed_id_is_notfound()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0];
        var campusB = instances[1];
        var now = DateTime.Now;
        await SeedLifecycleAuditAsync(db, req, campusA, VisitLifecycleHistoryAudit.PreparationStarted,
            VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit, IcStaffC1, now);
        var auditB = await SeedLifecycleAuditAsync(db, req, campusB, VisitLifecycleHistoryAudit.PreparationStarted,
            VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit, SupportingStudent, now);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted
            && e.VisitInstanceId == campusB.VisitInstanceId);

        var scopedResult = await Handler(db, StaffLeader(SlCampus1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(scopedResult.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitPreparationStarted
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.DoesNotContain(scopedResult.Entries, e => e.VisitInstanceId == campusB.VisitInstanceId);

        var eventIdB = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditB.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, StaffLeader(SlCampus1, Campus1))
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventIdB), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>Case C4-7. Staff Leader sees their own campus's lifecycle but cannot open another campus's by guessed id.</summary>
    [Fact]
    public async Task C4_7_Staff_leader_sees_own_campus_lifecycle_but_not_other_campus_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0]; // Campus1 — Staff Leader SlCampus1
        var campusB = instances[1]; // Campus2
        var now = DateTime.Now;
        await SeedLifecycleAuditAsync(db, req, campusA, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, now);
        var auditB = await SeedLifecycleAuditAsync(db, req, campusB, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, SupportingStudent, now);

        var result = await Handler(db, StaffLeader(SlCampus1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == campusB.VisitInstanceId);

        var eventIdB = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditB.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, StaffLeader(SlCampus1, Campus1))
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventIdB), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>Case C4-8. Same scope test for the current Host.</summary>
    [Fact]
    public async Task C4_8_Host_sees_own_campus_lifecycle_but_not_other_campus_detail()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var campusA = instances[0]; // hosted by IcStaffC1
        var campusB = instances[1];
        var now = DateTime.Now;
        await SeedLifecycleAuditAsync(db, req, campusA, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, now);
        var auditB = await SeedLifecycleAuditAsync(db, req, campusB, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, VisitorOwner, now);

        var result = await Handler(db, Host(IcStaffC1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
            && e.VisitInstanceId == campusA.VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == campusB.VisitInstanceId);

        var eventIdB = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, auditB.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, Host(IcStaffC1, Campus1))
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventIdB), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C4-9. Registrant, HO and the campus's own operational contact all see the lifecycle
    /// event per the existing visibility resolver; a totally unrelated user is refused entirely
    /// (the SAME 403 every other history section already gives them).
    /// </summary>
    [Fact]
    public async Task C4_9_Registrant_HO_and_operational_contact_see_lifecycle_per_existing_visibility_rules()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        target.OperationalContactUserId = ContactCampus1; // a distinct account, not the registrant
        await db.SaveChangesAsync();

        var now = DateTime.Now;
        var audit = await SeedLifecycleAuditAsync(db, req, target, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, now);
        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, audit.AuditLogId);

        var contactUser = new FakeUser { UserId = ContactCampus1, RoleCode = RoleCodes.Visitor };
        foreach (var actor in new ICurrentUserService[] { Owner(), Ho(), contactUser })
        {
            var result = await Handler(db, actor).Handle(
                new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
            Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
                && e.VisitInstanceId == target.VisitInstanceId);

            var drawer = await DetailHandler(db, actor).Handle(
                new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None);
            Assert.Equal(VisitHistoryEventCodes.VisitStarted, drawer.EventCode);
        }

        var stranger = new FakeUser { UserId = 9999, RoleCode = RoleCodes.Visitor };
        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, stranger)
            .Handle(new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C4-10 (also covers C4-D5). An unrelated, non-whitelisted AuditLogs row on the SAME
    /// instance stays inaccessible — the lifecycle whitelist growing must not widen what AuditDetailAsync
    /// will open.
    /// </summary>
    [Fact]
    public async Task C4_10_Unrelated_generic_audit_log_remains_hidden()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var now = DateTime.Now;
        var unrelated = new AuditLog
        {
            ActorUserId = VisitorOwner, Action = "SOME_UNRELATED_TECHNICAL_ACTION", EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = "TECHNICAL", CreatedAt = now,
        };
        db.AuditLogs.Add(unrelated);
        await db.SaveChangesAsync();

        var eventId = VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, unrelated.AuditLogId);
        await Assert.ThrowsAsync<NotFoundException>(() => DetailHandler(db, Owner())
            .Handle(new GetVisitHistoryDetailQuery(req.VisitRequestId, eventId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C4-13. Decision (Commit 2), contact-profile (Commit 3) and lifecycle (Commit 4) audits
    /// coexist on the SAME instance with distinct EventIds, no collision, and lifecycle visibility
    /// follows the normal campus scope while the contact event keeps its own identity gate.
    /// </summary>
    [Fact]
    public async Task C4_13_Lifecycle_coexists_with_decision_and_contact_events_without_collision()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var now = DateTime.Now;

        var decisionAudit = new AuditLog
        {
            ActorUserId = SlCampus1, Action = CampusDecisionAudit.Approved, EntityType = "VisitRequestCampus",
            EntityId = target.VisitInstanceId, CampusId = target.CampusId, VisitRequestId = req.VisitRequestId,
            VisitInstanceId = target.VisitInstanceId, SourceType = CampusDecisionAudit.SourceType,
            Reason = $"decision=ASSIGNED;host={IcStaffC1}", CreatedAt = now,
        };
        decisionAudit.Changes.Add(new AuditLogChange { FieldName = "visit_request_campuses.status", OldValueText = "WAITING_REQUEST_APPROVAL", NewValueText = "ASSIGNED", CreatedAt = now });
        decisionAudit.Changes.Add(new AuditLogChange { FieldName = "decision_note", OldValueText = null, NewValueText = "OK", CreatedAt = now });
        db.AuditLogs.Add(decisionAudit);
        await db.SaveChangesAsync();

        await SeedProfileUpdateAuditAsync(db, req, target, VisitorOwner);
        await SeedLifecycleAuditAsync(db, req, target, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, now);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved && e.VisitInstanceId == target.VisitInstanceId);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated && e.VisitInstanceId == target.VisitInstanceId);
        Assert.Single(result.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted && e.VisitInstanceId == target.VisitInstanceId);
        var ids = result.Entries.Where(e => e.VisitInstanceId == target.VisitInstanceId)
            .Select(e => e.EventId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        // Identity privacy unaffected by the new lifecycle whitelist: Staff Leader sees decision +
        // lifecycle (ordinary campus-scoped business events) but NOT the profile update (identity).
        var slResult = await Handler(db, StaffLeader(SlCampus1, Campus1)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.Contains(slResult.Entries, e => e.EventCode == VisitHistoryEventCodes.InstanceApproved);
        Assert.Contains(slResult.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);
        Assert.DoesNotContain(slResult.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactProfileUpdated);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Case C4-14. A real lifecycle transition happened first; the campus was cancelled later.
    /// Both must remain as distinct, separate timeline events — never collapsed into one, and
    /// never fabricated into a fake close.
    /// </summary>
    [Fact]
    public async Task C4_14_A_prior_lifecycle_event_and_a_later_cancellation_both_remain_distinct_no_fake_close()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        // ASSIGNED (not the WAITING_REQUEST_APPROVAL/"pending" bucket) — a DB trigger refuses to
        // cancel a pending instance except as a consequence of cancelling the whole pending request,
        // which is not what this test is about.
        var target = instances[0];
        var t1 = DateTime.Now;
        var t2 = t1.AddMinutes(30);

        await SeedLifecycleAuditAsync(db, req, target, VisitLifecycleHistoryAudit.CompleteBeforeVisit,
            VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit, IcStaffC1, t1);

        // Separately: the campus was cancelled. Whatever real business rule allowed this is
        // CancelVisitRequestCommandHandler's own concern — this test is about the READER not
        // collapsing the two events, not about re-deriving the cancellation guard.
        target.CancelledAt = t2;
        target.CancelledBy = VisitorOwner;
        target.CancellationActorType = "VISITOR";
        target.CancellationSource = "SELF_SERVICE";
        target.CancellationReason = "Đổi lịch";
        target.Status = VisitInstanceStatuses.Cancelled;
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var forInstance = result.Entries.Where(e => e.VisitInstanceId == target.VisitInstanceId).ToList();

        Assert.Single(forInstance, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);
        Assert.Single(forInstance, e => e.EventCode == VisitHistoryEventCodes.InstanceCancelled);
        Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.VisitCompleted);
        Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.InstanceClosed);
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Legacy strategy test. A campus is currently CLOSED but has NO lifecycle AuditLog rows at all
    /// — exactly the shape a pre-Commit-4 CompleteVisitStage write left (Action set, but never
    /// scoped by VisitInstanceId, so this query could never have found it anyway). The reader must
    /// show NONE of the three lifecycle events — never infer VISIT_STARTED/VISIT_COMPLETED/
    /// INSTANCE_CLOSED from the current status alone. Backfill (if ever done, from ClosedAt/ClosedBy
    /// as a plausible partial signal) is explicitly Commit 5's decision, not this reader's.
    /// </summary>
    [Fact]
    public async Task Legacy_closed_campus_with_no_lifecycle_audit_does_not_invent_a_fake_chain()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var target = instances[0];
        var now = DateTime.Now;
        // DURING_VISIT/AFTER_VISIT/CLOSED all require ≥1 agenda row at the DB level — unrelated to
        // this test's subject, just a precondition to reach the status legally.
        db.VisitAgendas.Add(new VisitAgenda
        {
            VisitInstanceId = target.VisitInstanceId, Title = "Khai mạc",
            StartTime = now.AddDays(-1), EndTime = now.AddDays(-1).AddHours(1),
            SequenceOrder = 1, CreatedAt = now, CreatedBy = VisitorOwner,
        });
        target.Status = VisitInstanceStatuses.Closed;
        target.ClosedAt = now;
        target.ClosedBy = IcStaffC1;
        await db.SaveChangesAsync();

        Assert.False(await db.AuditLogs.AnyAsync(a => a.VisitInstanceId == target.VisitInstanceId
            && VisitLifecycleHistoryAudit.LifecycleActions.Contains(a.Action)));

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        var forInstance = result.Entries.Where(e => e.VisitInstanceId == target.VisitInstanceId).ToList();
        Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.VisitStarted);
        Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.VisitCompleted);
        Assert.DoesNotContain(forInstance, e => e.EventCode == VisitHistoryEventCodes.InstanceClosed);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Entries_expose_facts_not_pre_assembled_audit_strings()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        Assert.NotEmpty(result.Entries);
        // Every code is a known member of the vocabulary the client maps to a sentence.
        Assert.All(result.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.EventCode)));
        // The old glued fragments are gone from every string-bearing field.
        var strings = result.Entries.SelectMany(e => new[] { e.Reason, e.CampusName, e.ActorName })
            .Where(s => s is not null)!;
        Assert.All(strings, s =>
        {
            Assert.DoesNotContain("source=", s);
            Assert.DoesNotContain("approvalRevision=", s);
            Assert.DoesNotContain("→", s);
        });
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Content_creation_is_reported_per_campus_with_its_revision()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var created = result.Entries
            .Where(e => e.EventCode == VisitHistoryEventCodes.InstanceContentCreated).ToList();
        Assert.Equal(2, created.Count);
        Assert.All(created, e =>
        {
            Assert.Equal(1u, e.FormRevision);
            Assert.Equal("CREATE", e.SourceType);      // a FACT the client may or may not render
            Assert.False(string.IsNullOrWhiteSpace(e.CampusName));
        });
        await tx.RollbackAsync();
    }

    // ── The three events the timeline used to be blind to ────────────────────
    // The rows were already being written by VisitSafeEditService / VisitRequestV2EditService and the
    // cancellation columns were already on the request — the handler simply collapsed every revision
    // into "created / revised" by its NUMBER and never looked at the request row at all. So a quick
    // update, a resubmission and a cancellation were invisible or indistinguishable.

    /// <summary>
    /// Adds a request-level revision with the given source type, plus the matching per-campus row when
    /// an instance is supplied. `(visit_instance_id, form_revision)` is unique, so SeedAsync's own
    /// revision 1 must never be re-used here.
    /// </summary>
    private static async Task SeedRevisionAsync(
        ApplicationDbContext db, VisitRequest req, VisitRequestCampus? instance,
        string sourceType, uint formRevision, uint requestRevision, ulong actorId)
    {
        var now = DateTime.Now;
        if (instance is not null)
        {
            db.VisitInstanceFormRevisionHistories.Add(new VisitInstanceFormRevisionHistory
            {
                VisitRequestId = req.VisitRequestId,
                VisitInstanceId = instance.VisitInstanceId,
                FormRevision = formRevision,
                ApprovalRevision = 1,
                SourceType = sourceType,
                SnapshotJson = "{}",
                AppliedBy = actorId,
                AppliedAt = now,
                // The writers store a correlation id here. It must never reach the reader as a "reason".
                Reason = Guid.NewGuid().ToString("N"),
            });
        }
        db.VisitRequestRevisionHistories.Add(new VisitRequestRevisionHistory
        {
            VisitRequestId = req.VisitRequestId,
            RequestRevision = requestRevision,
            SourceType = sourceType,
            SnapshotJson = "{}",
            AppliedBy = actorId,
            AppliedAt = now,
            Reason = Guid.NewGuid().ToString("N"),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_safe_edit_is_reported_as_a_quick_update_at_both_levels()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedRevisionAsync(db, req, instances[0], "SAFE_EDIT", 2, 2, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var instanceEdit = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.InstanceSafeEditApplied));
        Assert.Equal(instances[0].VisitInstanceId, instanceEdit.VisitInstanceId);
        Assert.Equal(2u, instanceEdit.FormRevision);
        Assert.False(string.IsNullOrWhiteSpace(instanceEdit.ActorName));
        Assert.False(string.IsNullOrWhiteSpace(instanceEdit.CampusName));

        var requestEdit = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.RequestSafeEditApplied));
        Assert.False(string.IsNullOrWhiteSpace(requestEdit.ActorName));

        // The correlation id the writer parks in `reason` is plumbing, not a business reason.
        Assert.Null(instanceEdit.Reason);
        Assert.Null(requestEdit.Reason);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_resubmission_is_reported_once_for_the_request_and_once_per_campus()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedRevisionAsync(db, req, instances[0], "RESUBMIT", 2, 2, VisitorOwner);
        await SeedRevisionAsync(db, req, instances[1], "RESUBMIT", 2, 3, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // The request-level sentence is the one a reader is looking for ("đã gửi lại đơn"); the
        // per-campus rows say WHICH content went back in.
        Assert.Equal(2, result.Entries.Count(
            e => e.EventCode == VisitHistoryEventCodes.RequestResubmitted));
        var perCampus = result.Entries
            .Where(e => e.EventCode == VisitHistoryEventCodes.InstanceContentResubmitted).ToList();
        Assert.Equal(2, perCampus.Count);
        Assert.Equal(2, perCampus.Select(e => e.VisitInstanceId).Distinct().Count());
        Assert.All(perCampus, e => Assert.False(string.IsNullOrWhiteSpace(e.ActorName)));
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_cancelled_request_reports_who_cancelled_it_when_and_why()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var cancelledAt = DateTime.Now.AddMinutes(-5);
        req.Status = VisitRequestStatuses.Cancelled;
        req.CancelledBy = VisitorOwner;
        req.CancelledAt = cancelledAt;
        req.CancellationReason = "Đoàn thay đổi lịch bay";
        await db.SaveChangesAsync();

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        var cancelled = Assert.Single(result.Entries.Where(
            e => e.EventCode == VisitHistoryEventCodes.RequestCancelled));
        Assert.False(string.IsNullOrWhiteSpace(cancelled.ActorName));
        Assert.Equal("Đoàn thay đổi lịch bay", cancelled.Reason);
        Assert.Equal(VisitRequestStatuses.Cancelled, cancelled.StatusCode);
        Assert.Null(cancelled.VisitInstanceId); // request-level, not a campus event
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task A_scoped_leader_does_not_receive_the_request_level_cancellation()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        req.Status = VisitRequestStatuses.Cancelled;
        req.CancelledBy = VisitorOwner;
        req.CancelledAt = DateTime.Now;
        req.CancellationReason = "Lý do nội bộ của đoàn";
        await db.SaveChangesAsync();

        var result = await Handler(db, StaffLeader(SlCampus2, Campus2)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // Request-level entries stay with the managers/HO — a campus leader's timeline is their campus.
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestCancelled);
        Assert.DoesNotContain(result.Entries, e => e.Reason == "Lý do nội bộ của đoàn");
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_first_request_revision_reads_as_a_submission_not_an_update()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        // Request level only: SeedAsync already wrote each campus's own CREATE revision 1.
        await SeedRevisionAsync(db, req, null, "CREATE", 1, 1, VisitorOwner);

        var result = await Handler(db, Owner()).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        Assert.Contains(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestCreated);
        // "đã cập nhật thông tin chung" was the sentence a CREATE row used to produce.
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);
        await tx.RollbackAsync();
    }

    // ── Scope ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_scoped_leader_sees_only_their_own_campus_and_no_identity_events()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var result = await Handler(db, StaffLeader(SlCampus2, Campus2)).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);

        // Campus 1's decision belongs to a campus this leader cannot see.
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == instances[0].VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactIdentityChanged);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_unrelated_actor_is_refused()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        var stranger = new FakeUser { UserId = 22, RoleCode = RoleCodes.Visitor };

        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, stranger).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None));
        await tx.RollbackAsync();
    }

    // ── Detail visibility and history visibility are two permissions ──────────
    //
    // They have always had different answers on the backend; what did not exist was any way for the
    // detail screen to KNOW that, so it mounted the history section for everyone who could open the
    // page and painted the resulting 403 as a technical failure with a Retry button. These tests pin
    // the mismatch as the intended rule and pin the capability that reports it, in the same test —
    // asserting either one alone is how the two drifted apart in the first place.

    /// <summary>Invites a user to SUPPORT one campus. Support, and nothing else.</summary>
    private static async Task SeedSupportingParticipantAsync(
        ApplicationDbContext db, VisitRequestCampus instance, ulong userId, string role)
    {
        var now = DateTime.Now;
        db.VisitParticipants.Add(new VisitParticipant
        {
            VisitInstanceId = instance.VisitInstanceId,
            UserId = userId,
            ParticipantRole = role,
            IsHost = false,
            Status = ParticipantStatuses.Accepted,
            InvitedBy = SlCampus1, InvitedAt = now,
            AssignedBy = SlCampus1, AssignedAt = now,
            RespondedAt = now, CreatedAt = now, CreatedBy = SlCampus1,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_supporting_participant_reads_the_detail_and_is_refused_the_history()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        await SeedSupportingParticipantAsync(db, instances[0], SupportingStudent, ParticipantRoles.Student);
        var participant = Student(SupportingStudent, Campus1);

        // The detail they were invited to is theirs to read — that half must keep working.
        var resolved = await Resolver(db, participant).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.NotEmpty(resolved.CampusVisits);
        Assert.Contains(VisitFormActions.View, resolved.Viewer.AllowedActions);

        // The history is not. The capability says so, so the client never mounts the section...
        Assert.DoesNotContain(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        // ...and the endpoint says the same thing, which is the point: a capability that disagreed
        // with the API would just move the bug from one side of the wire to the other.
        await Assert.ThrowsAsync<ForbiddenException>(() => Handler(db, participant).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None));

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task An_ic_support_participant_gains_no_history_either()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        // IC_SUPPORT on campus 2 — a STAFF account, so this also proves the refusal is about the
        // RELATION and not about the role name on the badge. Deliberately NOT IcStaffC1, who hosts
        // campus 1 in this fixture and therefore has history there on merit.
        await SeedSupportingParticipantAsync(db, instances[1], IcStaffSupporter, ParticipantRoles.IcSupport);
        // Not the host of campus 2 and not its leader: supporting it is their whole connection to it.
        var supporter = Host(IcStaffSupporter, Campus2);

        var resolved = await Resolver(db, supporter).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.NotEmpty(resolved.CampusVisits);
        Assert.DoesNotContain(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        await tx.RollbackAsync();
    }

    [Fact]
    public async Task The_current_host_is_granted_the_capability_and_their_scoped_history()
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, instances) = await SeedAsync(db);
        var host = Host(IcStaffC1, Campus1);   // hosts campus 1 only

        var resolved = await Resolver(db, host).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        var result = await Handler(db, host).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.NotEmpty(result.Entries);
        // Scoping is unchanged: the campus they do NOT host stays out, and so do the request-level
        // events. Granting the capability must not have widened what it grants access TO.
        Assert.DoesNotContain(result.Entries, e => e.VisitInstanceId == instances[1].VisitInstanceId);
        Assert.DoesNotContain(result.Entries, e => e.EventCode == VisitHistoryEventCodes.RequestRevision);

        await tx.RollbackAsync();
    }

    [Theory]
    [InlineData("REGISTRANT")]
    [InlineData("HO")]
    [InlineData("STAFF_LEADER")]
    public async Task The_existing_history_actors_keep_the_capability(string who)
    {
        RequireDb();
        using var db = NewContext();
        using var tx = await db.Database.BeginTransactionAsync();

        var (req, _) = await SeedAsync(db);
        FakeUser actor = who switch
        {
            "REGISTRANT" => Owner(),
            "HO" => Ho(),
            _ => StaffLeader(SlCampus1, Campus1),
        };

        var resolved = await Resolver(db, actor).ResolveAsync(req.VisitRequestId, CancellationToken.None);
        Assert.Contains(VisitFormActions.ViewChangeHistory, resolved.Viewer.AllowedActions);

        // And the endpoint still serves them, so the capability is not a claim nobody checked.
        var result = await Handler(db, actor).Handle(
            new GetVisitRequestHistoryQuery(req.VisitRequestId), CancellationToken.None);
        Assert.NotEmpty(result.Entries);

        await tx.RollbackAsync();
    }
}
