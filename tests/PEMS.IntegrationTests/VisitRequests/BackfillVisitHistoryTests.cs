using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Admin.Queries.GetAdminAuditLogs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.BackfillVisitHistory;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Users;
using PEMS.Infrastructure.Persistence;
using PEMS.Shared;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// VISIT_HISTORY_INTEGRITY final phase, Phase C — BackfillVisitHistoryCommandHandler, against real
/// MySQL. Every scenario hand-builds the exact "legacy" shape the backfill exists to recover (an audit
/// row missing scope/Changes, a resubmit's pre-clear snapshot, a CLOSED campus with ClosedAt/ClosedBy
/// and no close audit) — none of this can be produced by today's writers any more, which is exactly
/// why it has to be hand-built rather than driven through the real command handlers.
///
/// Seed ids match VisitRequestHistoryV2Tests.cs: registrant = 8, Staff Leader campus1 = 3, HO = 2,
/// IC Staff campus1 = 4, ADMIN = 1 (seed admin account). Campus1 = 1, Campus2 = 2.
/// </summary>
public sealed class BackfillVisitHistoryTests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager
        .GetDisposableConnectionString(
            "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    private const ulong Registrant = 8, SlCampus1 = 3, HoUser = 2, IcStaffC1 = 4, AdminUser = 1;
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
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode) { UserId = id; RoleCode = roleCode; }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole => null;
        public ulong? PrimaryCampusId => null;
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private sealed class FixedClock : IDateTimeService
    {
        private readonly DateTime _at;
        public FixedClock(DateTime at) => _at = at;
        public DateTime UtcNow => _at;
        public DateTime VietnamNow => _at;
    }

    /// <summary>
    /// MySQL DATETIME columns are whole-second precision — audit.CreatedAt is truncated the instant it
    /// round-trips through the database. A resubmit snapshot's <c>decidedAt</c> JSON value, in real
    /// production data, is always built from an entity ALREADY loaded from that same column (so it is
    /// already truncated); a test that embeds a fresh in-memory DateTime.Now straight into JSON without
    /// this truncation is comparing a sub-second value against a whole-second one and would never match
    /// — a fixture bug, not evidence about the matching logic itself.
    /// </summary>
    private static DateTime TruncateToSecond(DateTime dt)
        => new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);

    private static FakeUser Admin() => new(AdminUser, RoleCodes.Admin);
    private static FakeUser NonAdmin() => new(HoUser, RoleCodes.Ho);
    /// <summary>For reading the timeline back through GetVisitRequestHistoryQueryHandler — ADMIN is
    /// not one of that resolver's recognized business roles, so these read-through sanity checks use
    /// the request's own registrant, who always has full visibility on their own request.</summary>
    private static FakeUser Registrant_() => new(Registrant, RoleCodes.Visitor);

    private static async Task<BackfillVisitHistoryResponse> RunAsync(bool dryRun, ICurrentUserService? user = null)
    {
        using var db = NewContext();
        var handler = new BackfillVisitHistoryCommandHandler(
            db, user ?? Admin(), new FixedClock(DateTime.Now), NullLogger<BackfillVisitHistoryCommandHandler>.Instance);
        return await handler.Handle(new BackfillVisitHistoryCommand(dryRun), CancellationToken.None);
    }

    // ── Fixture ──────────────────────────────────────────────────────────────────────

    private static async Task<(ulong RequestId, List<ulong> InstanceIds)> SeedRequestAsync(
        ApplicationDbContext db, params (ulong CampusId, string Status)[] campuses)
    {
        var now = DateTime.Now;
        var req = new VisitRequest
        {
            RequestCode = "BF-" + Guid.NewGuid().ToString("N")[..12],
            RegistrantUserId = Registrant,
            CreatedSource = "VISITOR_SUBMITTED",
            HasMixedCampusDetails = campuses.Length > 1,
            RegistrantFullName = "Reg BF", RegistrantOrganization = "Org", RegistrantJobTitle = "Job",
            RegistrantPhone = "+8490", RegistrantEmail = "reg-bf-" + Guid.NewGuid().ToString("N")[..6] + "@example.com",
            RegistrantNationality = "VN",
            VisitScope = campuses.Length > 1 ? "MULTI_CAMPUS" : "SINGLE_CAMPUS",
            Status = "PARTIALLY_APPROVED", SubmittedAt = now, CreatedAt = now,
        };
        // Any "operational" status (ASSIGNED and beyond) requires an official host on the row — a DB
        // trigger refuses otherwise ("Approved/operational campus instance requires official host
        // assignment"), and "decided_by must be Staff Leader of the SAME campus" refuses a mismatched
        // leader/campus pairing. Every fixture in this file that needs an operational status uses
        // Campus1 (whose Staff Leader/IC Staff seed ids are already proven throughout this test
        // suite); a fixture that needs a second, genuinely different campus (BF11) keeps it at a
        // non-operational status instead, since that test is about EntityId→campus mapping, not
        // about exercising the decision/host machinery on two campuses at once.
        var operationalStatuses = new[]
        {
            VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit,
            VisitInstanceStatuses.AfterVisit, VisitInstanceStatuses.Closed,
        };

        foreach (var (campusId, status) in campuses)
        {
            var needsHost = operationalStatuses.Contains(status);
            // Every instance is INSERTED at ASSIGNED (the first operational status, needing a host but
            // no agenda) and stepped up to its real target afterward — the DB enforces the ladder one
            // hop at a time ("can only enter DURING_VISIT from BEFORE_VISIT" etc.), so no status past
            // ASSIGNED can ever be reached in the initial INSERT itself.
            var insertStatus = needsHost ? VisitInstanceStatuses.Assigned : status;
            req.CampusInstances.Add(new VisitRequestCampus
            {
                CampusId = campusId,
                PlannedStartAt = now.AddDays(20),
                PlannedEndAt = now.AddDays(20).AddHours(2),
                Status = insertStatus,
                OperationalContactUserId = Registrant,
                OperationalContactConfirmedAt = now,
                OperationalContactConfirmationSource = "REGISTRANT_SELF_MATCH",
                CurrentHostUserId = needsHost ? IcStaffC1 : null,
                HostAssignedBy = needsHost ? SlCampus1 : null,
                HostAssignedAt = needsHost ? now : null,
                DecidedBy = needsHost ? SlCampus1 : null,
                DecidedAt = needsHost ? now : null,
                DecisionActorRole = needsHost ? DecisionActorRole.StaffLeader : null,
                DecisionSource = needsHost ? DecisionSources.StandardCampusReview : null,
                CreatedAt = now,
                FormDetail = new VisitInstanceFormDetail
                {
                    DelegationName = $"DELEG-{campusId}", VisitType = "MEETING", Purpose = "P",
                    WorkingContent = "C",
                    OperationalContactFullName = "Op", OperationalContactOrganization = "OpOrg",
                    OperationalContactJobTitle = "Trưởng phòng Hợp tác",
                    OperationalContactPhone = "+8410", OperationalContactEmail = "op-bf@example.com",
                    WorkingLanguage = "VI", MediaConsentStatus = "AGREED",
                    FormRevision = 1, ApprovalRevision = 1, CreatedAt = now,
                },
            });
        }
        db.VisitRequests.Add(req);
        await db.SaveChangesAsync();

        var ordered = req.CampusInstances.OrderBy(c => c.CampusId).ToList();
        var instanceIds = ordered.Select(c => c.VisitInstanceId).ToList();

        // The agenda-required-for-DURING/AFTER/CLOSED trigger applies regardless of which test needs
        // it — one harmless agenda row per instance keeps every status reachable uniformly, added
        // BEFORE the status ladder below ever reaches DURING_VISIT.
        foreach (var instanceId in instanceIds)
        {
            db.VisitAgendas.Add(new VisitAgenda
            {
                VisitInstanceId = instanceId, Title = "Đón khách",
                StartTime = now.AddDays(20), EndTime = now.AddDays(20).AddHours(1),
                SequenceOrder = 1, CreatedAt = now, CreatedBy = Registrant,
            });
        }
        await db.SaveChangesAsync();

        // Step every instance up its own status ladder ONE HOP AT A TIME, each hop its own
        // SaveChanges — the DB only allows the immediate-predecessor transition ("can only enter
        // DURING_VISIT from BEFORE_VISIT"), so jumping straight from ASSIGNED to DURING_VISIT in one
        // UPDATE is refused exactly like a real skipped stage would be.
        var ladder = new[]
        {
            VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit,
            VisitInstanceStatuses.AfterVisit, VisitInstanceStatuses.Closed,
        };
        var targetStatusByCampus = campuses.ToDictionary(c => c.CampusId, c => c.Status);
        var targetIndexByCampus = ordered.ToDictionary(c => c.CampusId, c => Array.IndexOf(ladder, targetStatusByCampus[c.CampusId]));
        for (var step = 1; step < ladder.Length; step++)
        {
            var any = false;
            foreach (var campus in ordered)
            {
                if (targetIndexByCampus[campus.CampusId] < step) continue; // this instance is not going that far
                campus.Status = ladder[step];
                any = true;
            }
            if (any) await db.SaveChangesAsync();
        }

        return (req.VisitRequestId, instanceIds);
    }

    /// <summary>A legacy decision AuditLog — Action set, scope set, ZERO AuditLogChange rows (the
    /// exact pre-Commit-2 shape).</summary>
    private static async Task<AuditLog> SeedLegacyDecisionAuditAsync(
        ApplicationDbContext db, ulong requestId, ulong instanceId, ulong campusId,
        string action, ulong actorId, DateTime createdAt)
    {
        var audit = new AuditLog
        {
            ActorUserId = actorId, Action = action, EntityType = "VisitRequestCampus", EntityId = instanceId,
            CampusId = campusId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            SourceType = CampusDecisionAudit.SourceType, CreatedAt = createdAt,
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        return audit;
    }

    /// <summary>The immutable snapshot RESUBMIT_REJECTED_VISIT_INSTANCE_V2 writes before clearing one
    /// campus's decision fields (singular shape — VisitRequestV2EditService.cs).</summary>
    private static async Task SeedSingularResubmitSnapshotAsync(
        ApplicationDbContext db, ulong requestId, ulong instanceId, ulong campusId,
        string oldStatus, ulong? decidedBy, DateTime? decidedAt, string? decisionNote, DateTime now,
        string? rawJsonOverride = null)
    {
        var audit = new AuditLog
        {
            Action = "RESUBMIT_REJECTED_VISIT_INSTANCE_V2", EntityType = "VisitRequestCampus", EntityId = instanceId,
            CampusId = campusId, VisitRequestId = requestId, VisitInstanceId = instanceId,
            SourceType = "RESUBMIT", CreatedAt = now,
        };
        var json = rawJsonOverride ?? System.Text.Json.JsonSerializer.Serialize(new
        {
            visitInstanceId = instanceId, campusId, oldStatus, decidedBy, decidedAt, decisionNote,
        });
        audit.Changes.Add(new AuditLogChange
        {
            FieldName = "campus_decision_before_resubmit_json", OldValueText = json,
            NewValueText = "cleared_for_resubmission", CreatedAt = now,
        });
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
    }

    /// <summary>A legacy lifecycle AuditLog — Action set, EntityId set, everything else (scope,
    /// SourceType, Changes) missing (the exact pre-fix shape both StartVisitPreparationCommandHandler
    /// and CompleteVisitStageCommandHandler had before this session's writer fixes).</summary>
    private static async Task<AuditLog> SeedLegacyLifecycleAuditAsync(
        ApplicationDbContext db, ulong instanceId, string action, ulong actorId, DateTime createdAt)
    {
        var audit = new AuditLog
        {
            ActorUserId = actorId, Action = action, EntityType = "VisitRequestCampus", EntityId = instanceId,
            CreatedAt = createdAt,
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();
        return audit;
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM visit_agendas WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    private static async Task<AuditLog> LoadAuditAsync(ulong auditLogId)
    {
        using var db = NewContext();
        return await db.AuditLogs.AsNoTracking().Include(a => a.Changes).SingleAsync(a => a.AuditLogId == auditLogId);
    }

    // ── Security: only ADMIN may run this ───────────────────────────────────────────────

    [Fact]
    public async Task NonAdmin_caller_is_refused()
    {
        RequireDb();
        await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ForbiddenException>(
            () => RunAsync(dryRun: true, user: NonAdmin()));
    }

    // ── BF-1: old rejection audit + resubmit snapshot → note recovered ─────────────────

    [Fact]
    public async Task BF1_Old_rejection_audit_with_matching_resubmit_snapshot_recovers_decision_note()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var legacyAudit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                "Không đủ giấy tờ", DateTime.Now.AddMinutes(5));

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(1, dry.DecisionAuditsEnriched);

            var result = await RunAsync(dryRun: false);
            Assert.Equal(1, result.DecisionAuditsEnriched);

            var reloaded = await LoadAuditAsync(legacyAudit.AuditLogId);
            var noteChange = Assert.Single(reloaded.Changes.Where(c => c.FieldName == "decision_note"));
            Assert.Equal("Không đủ giấy tờ", noteChange.NewValueText);

            // Provenance: enrichment must NOT stamp the original audit's Reason with the backfill
            // marker — only a wholly-CREATED row carries that.
            Assert.NotEqual(BackfillVisitHistoryCommandHandler.RecoveredHistoryReason, reloaded.Reason);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-2: old rejection audit already complete → no change ─────────────────────────

    [Fact]
    public async Task BF2_Old_rejection_audit_already_enriched_is_left_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var audit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            audit.Changes.Add(new AuditLogChange
            {
                FieldName = "decision_note", NewValueText = "Đã có sẵn", CreatedAt = decidedAt,
            });
            await db.SaveChangesAsync();

            var result = await RunAsync(dryRun: false);
            Assert.Equal(0, result.DecisionAuditsEnriched);

            var reloaded = await LoadAuditAsync(audit.AuditLogId);
            var note = Assert.Single(reloaded.Changes.Where(c => c.FieldName == "decision_note"));
            Assert.Equal("Đã có sẵn", note.NewValueText); // unchanged
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-3: ambiguous decision snapshot → skipped ─────────────────────────────────────

    [Fact]
    public async Task BF3_Two_resubmit_snapshots_matching_the_same_decision_are_skipped_as_ambiguous()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var legacyAudit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            // TWO snapshots resolving to the identical (instance, actor, decidedAt) key, with DIFFERENT
            // notes — genuinely ambiguous, no tolerance, no "first/newest wins".
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                "Lý do A", DateTime.Now.AddMinutes(5));
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                "Lý do B", DateTime.Now.AddMinutes(6));

            var result = await RunAsync(dryRun: false);
            Assert.Equal(0, result.DecisionAuditsEnriched);
            Assert.Equal(1, result.DecisionSkippedAmbiguous);

            var reloaded = await LoadAuditAsync(legacyAudit.AuditLogId);
            Assert.DoesNotContain(reloaded.Changes, c => c.FieldName == "decision_note");
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-4: old lifecycle audit → scoped + status diff recovered ─────────────────────

    [Fact]
    public async Task BF4_Old_lifecycle_audit_recovers_scope_and_structured_status_change()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.DuringVisit));
            requestId = id;
            var instanceId = instances[0];
            var at = DateTime.Now;

            var legacyAudit = await SeedLegacyLifecycleAuditAsync(
                db, instanceId, VisitLifecycleHistoryAudit.CompleteBeforeVisit, IcStaffC1, at);

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(1, dry.LifecycleAuditsEnriched);

            var result = await RunAsync(dryRun: false);
            Assert.Equal(1, result.LifecycleAuditsEnriched);

            var reloaded = await LoadAuditAsync(legacyAudit.AuditLogId);
            Assert.Equal(requestId, reloaded.VisitRequestId);
            Assert.Equal(instanceId, reloaded.VisitInstanceId);
            Assert.Equal(Campus1, reloaded.CampusId);
            Assert.Equal(VisitLifecycleHistoryAudit.SourceType, reloaded.SourceType);
            var change = Assert.Single(reloaded.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.BeforeVisit, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.DuringVisit, change.NewValueText);

            // Now readable through the normal timeline, under the normal event code — no special
            // "recovered" marker visible to the reader.
            using var db2 = NewContext();
            var history = await new GetVisitRequestHistoryQueryHandler(db2, Registrant_(), new PerCampusFormV2Options { Enabled = true })
                .Handle(new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            Assert.Single(history.Entries, e => e.EventCode == VisitHistoryEventCodes.VisitStarted
                && e.VisitInstanceId == instanceId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-5: CLOSED + ClosedAt/ClosedBy only → close-only event created ───────────────

    [Fact]
    public async Task BF5_Closed_campus_with_only_ClosedAt_ClosedBy_gets_a_close_only_backfilled_event()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.AfterVisit));
            requestId = id;
            var instanceId = instances[0];

            var target = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceId);
            var closedAt = TruncateToSecond(DateTime.Now.AddDays(-1));
            target.Status = VisitInstanceStatuses.Closed;
            target.ClosedAt = closedAt;
            target.ClosedBy = IcStaffC1;
            await db.SaveChangesAsync();

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(1, dry.LifecycleCloseEventsCreated);

            var result = await RunAsync(dryRun: false);
            Assert.Equal(1, result.LifecycleCloseEventsCreated);

            using var check = NewContext();
            var created = Assert.Single(await check.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .Where(a => a.VisitInstanceId == instanceId && a.Action == VisitLifecycleHistoryAudit.CloseVisitInstance)
                .ToListAsync());
            Assert.Equal(closedAt, created.CreatedAt); // original close time, not migration time
            Assert.Equal(IcStaffC1, created.ActorUserId);
            Assert.Equal(BackfillVisitHistoryCommandHandler.RecoveredHistoryReason, created.Reason);
            var change = Assert.Single(created.Changes.Where(c => c.FieldName == "visit_request_campuses.status"));
            Assert.Equal(VisitInstanceStatuses.AfterVisit, change.OldValueText);
            Assert.Equal(VisitInstanceStatuses.Closed, change.NewValueText);

            // No VISIT_STARTED / VISIT_COMPLETED / VISIT_PREPARATION_STARTED were invented alongside it.
            Assert.False(await check.AuditLogs.AnyAsync(a => a.VisitInstanceId == instanceId
                && a.Action != VisitLifecycleHistoryAudit.CloseVisitInstance
                && VisitLifecycleHistoryAudit.LifecycleActions.Contains(a.Action)));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-6: CLOSED without evidence → no fake events ──────────────────────────────────

    [Fact]
    public async Task BF6_Closed_campus_with_no_evidence_at_all_gets_nothing_invented()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.AfterVisit));
            requestId = id;
            var instanceId = instances[0];

            var target = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceId);
            target.Status = VisitInstanceStatuses.Closed; // no ClosedAt/ClosedBy, no audit at all
            await db.SaveChangesAsync();

            var result = await RunAsync(dryRun: false);
            Assert.Equal(0, result.LifecycleCloseEventsCreated);

            using var check = NewContext();
            Assert.False(await check.AuditLogs.AnyAsync(a => a.VisitInstanceId == instanceId
                && VisitLifecycleHistoryAudit.LifecycleActions.Contains(a.Action)));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-7: old self-match contact audit, deterministic scope recovered ──────────────

    [Fact]
    public async Task BF7_Old_contact_profile_audit_with_deterministic_instance_link_recovers_scope()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var now = DateTime.Now;

            var legacy = new AuditLog
            {
                ActorUserId = Registrant, Action = OperationalContactHistoryAudit.ProfileUpdated,
                EntityType = "VisitRequestCampus", EntityId = instanceId,
                SourceType = "IDENTITY", CreatedAt = now,
                // Scope columns deliberately left null — the exact pre-Commit-3 shape.
            };
            legacy.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_full_name", OldValueText = "Old Name", NewValueText = "New Name", CreatedAt = now,
            });
            db.AuditLogs.Add(legacy);
            await db.SaveChangesAsync();

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(1, dry.ContactAuditsScoped);

            var result = await RunAsync(dryRun: false);
            Assert.Equal(1, result.ContactAuditsScoped);

            var reloaded = await LoadAuditAsync(legacy.AuditLogId);
            Assert.Equal(requestId, reloaded.VisitRequestId);
            Assert.Equal(instanceId, reloaded.VisitInstanceId);
            Assert.Equal(Campus1, reloaded.CampusId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-8: external replace does not create a duplicate timeline event ──────────────

    [Fact]
    public async Task BF8_External_replace_audit_scope_backfill_does_not_duplicate_the_timeline()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var now = DateTime.Now;

            // External-address outcome: operational_contact_user_id change lands NULL. The reader
            // (GetVisitRequestHistoryQueryHandler) already skips rendering this outcome — the
            // invitation's own events tell it in full — and scope backfill must not change that.
            var legacy = new AuditLog
            {
                ActorUserId = Registrant, Action = OperationalContactHistoryAudit.Replaced,
                EntityType = "VisitRequestCampus", EntityId = instanceId,
                SourceType = "IDENTITY", CreatedAt = now,
            };
            legacy.Changes.Add(new AuditLogChange
            {
                FieldName = "operational_contact_user_id", OldValueText = Registrant.ToString(), NewValueText = null, CreatedAt = now,
            });
            db.AuditLogs.Add(legacy);
            await db.SaveChangesAsync();

            var result = await RunAsync(dryRun: false);
            Assert.Equal(1, result.ContactAuditsScoped); // scope IS recovered

            using var db2 = NewContext();
            var history = await new GetVisitRequestHistoryQueryHandler(db2, Registrant_(), new PerCampusFormV2Options { Enabled = true })
                .Handle(new GetVisitRequestHistoryQuery(requestId), CancellationToken.None);
            // Still not rendered — scope backfill recovers METADATA, never changes what the reader
            // chooses to surface.
            Assert.DoesNotContain(history.Entries, e => e.EventCode == VisitHistoryEventCodes.ContactReplacedWithRegistrant);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-7B: legacy transfer-requested audit, scope recovered and isolated per campus ──

    /// <summary>
    /// Before the fix, InitiateOperationalContactTransferCommandHandler set VisitRequestId but never
    /// CampusId/VisitInstanceId on its own audit row — invisible to GetAdminAuditLogsQueryHandler's
    /// campus filter. Two campuses on one request, each with its own legacy unscoped row, prove the
    /// backfill recovers each one from ITS OWN campus (via the deterministic EntityId join) rather than
    /// cross-attributing — the exact multi-campus isolation "OPERATIONAL_CONTACT_TRANSFER_REQUESTED" is
    /// not named in OperationalContactHistoryAudit needs to keep proving.
    /// </summary>
    [Fact]
    public async Task BF7B_Old_transfer_requested_audit_scope_recovered_and_isolated_per_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(
                db,
                (Campus1, VisitInstanceStatuses.WaitingRequestApproval),
                (Campus2, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceHn = instances[0];
            var instanceHcm = instances[1];
            var now = DateTime.Now;

            var legacyHn = new AuditLog
            {
                ActorUserId = Registrant, Action = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
                EntityType = "VisitRequestCampus", EntityId = instanceHn,
                VisitRequestId = requestId, SourceType = "IDENTITY", CreatedAt = now,
                // CampusId / VisitInstanceId deliberately left null — the exact pre-fix shape.
            };
            var legacyHcm = new AuditLog
            {
                ActorUserId = Registrant, Action = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED",
                EntityType = "VisitRequestCampus", EntityId = instanceHcm,
                VisitRequestId = requestId, SourceType = "IDENTITY", CreatedAt = now,
            };
            db.AuditLogs.AddRange(legacyHn, legacyHcm);
            await db.SaveChangesAsync();

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(2, dry.ContactAuditsScoped);

            var result = await RunAsync(dryRun: false);
            Assert.Equal(2, result.ContactAuditsScoped);

            var reloadedHn = await LoadAuditAsync(legacyHn.AuditLogId);
            var reloadedHcm = await LoadAuditAsync(legacyHcm.AuditLogId);
            Assert.Equal(instanceHn, reloadedHn.VisitInstanceId);
            Assert.Equal(Campus1, reloadedHn.CampusId);
            Assert.Equal(instanceHcm, reloadedHcm.VisitInstanceId);
            Assert.Equal(Campus2, reloadedHcm.CampusId);

            // The multi-campus proof: filtering the admin audit trail by ONE campus returns only that
            // campus's transfer-requested row — the very query this scope gap made blind.
            using var read = NewContext();
            var hnOnly = await new GetAdminAuditLogsQueryHandler(read, Admin()).Handle(
                new GetAdminAuditLogsQuery
                {
                    CampusId = Campus1, Action = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED", PageSize = 100,
                },
                CancellationToken.None);
            var hnIds = hnOnly.Items.Select(i => i.AuditLogId).ToList();
            Assert.Contains(legacyHn.AuditLogId, hnIds);
            Assert.DoesNotContain(legacyHcm.AuditLogId, hnIds);

            var hcmOnly = await new GetAdminAuditLogsQueryHandler(read, Admin()).Handle(
                new GetAdminAuditLogsQuery
                {
                    CampusId = Campus2, Action = "OPERATIONAL_CONTACT_TRANSFER_REQUESTED", PageSize = 100,
                },
                CancellationToken.None);
            var hcmIds = hcmOnly.Items.Select(i => i.AuditLogId).ToList();
            Assert.Contains(legacyHcm.AuditLogId, hcmIds);
            Assert.DoesNotContain(legacyHn.AuditLogId, hcmIds);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-9 / BF-10: dry-run writes nothing; execute twice writes once ─────────────────

    [Fact]
    public async Task BF9_Dry_run_performs_zero_writes()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var legacyAudit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                "Ghi chú", DateTime.Now.AddMinutes(5));

            var beforeAuditCount = await db.AuditLogs.CountAsync(a => a.VisitRequestId == requestId);

            var dry = await RunAsync(dryRun: true);
            Assert.Equal(1, dry.DecisionAuditsEnriched);
            Assert.True(dry.DryRun);

            using var check = NewContext();
            var afterAuditCount = await check.AuditLogs.CountAsync(a => a.VisitRequestId == requestId);
            Assert.Equal(beforeAuditCount, afterAuditCount); // no new audit rows
            var reloaded = await check.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .SingleAsync(a => a.AuditLogId == legacyAudit.AuditLogId);
            Assert.Empty(reloaded.Changes); // no Changes row was actually written
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task BF10_Executing_twice_writes_once_second_run_reports_zero_new_changes()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var legacyAudit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                "Ghi chú lần đầu", DateTime.Now.AddMinutes(5));

            var first = await RunAsync(dryRun: false);
            Assert.Equal(1, first.DecisionAuditsEnriched);

            var second = await RunAsync(dryRun: false);
            Assert.Equal(0, second.DecisionAuditsEnriched);
            Assert.Equal(0, second.DecisionSkippedAmbiguous);

            var reloaded = await LoadAuditAsync(legacyAudit.AuditLogId);
            var notes = reloaded.Changes.Where(c => c.FieldName == "decision_note").ToList();
            Assert.Single(notes); // not duplicated
            Assert.Equal("Ghi chú lần đầu", notes[0].NewValueText);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-11: multi-campus mapping cannot cross-wire instances ─────────────────────────

    [Fact]
    public async Task BF11_Multi_campus_request_never_cross_wires_a_backfilled_scope_to_the_wrong_campus()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            // Neither campus needs an operational status here — this test is purely about the
            // deterministic EntityId → (VisitRequestId, CampusId) mapping the lifecycle enrichment
            // uses, which does not depend on the campus's current status at all.
            var (id, instances) = await SeedRequestAsync(
                db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval),
                (Campus2, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceA = instances[0]; // Campus1
            var instanceB = instances[1]; // Campus2
            var at = DateTime.Now;

            var legacyA = await SeedLegacyLifecycleAuditAsync(
                db, instanceA, VisitLifecycleHistoryAudit.CompleteBeforeVisit, IcStaffC1, at);
            var legacyB = await SeedLegacyLifecycleAuditAsync(
                db, instanceB, VisitLifecycleHistoryAudit.PreparationStarted, IcStaffC1, at.AddMinutes(1));

            var result = await RunAsync(dryRun: false);
            Assert.Equal(2, result.LifecycleAuditsEnriched);

            var reloadedA = await LoadAuditAsync(legacyA.AuditLogId);
            var reloadedB = await LoadAuditAsync(legacyB.AuditLogId);

            Assert.Equal(Campus1, reloadedA.CampusId);
            Assert.Equal(instanceA, reloadedA.VisitInstanceId);
            Assert.Equal(Campus2, reloadedB.CampusId);
            Assert.Equal(instanceB, reloadedB.VisitInstanceId);
            Assert.NotEqual(reloadedA.CampusId, reloadedB.CampusId);
            Assert.NotEqual(reloadedA.VisitInstanceId, reloadedB.VisitInstanceId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── BF-12: malformed legacy JSON → skipped/report error, no transaction corruption ─

    [Fact]
    public async Task BF12_Malformed_resubmit_snapshot_json_is_counted_as_an_error_not_thrown()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            using var db = NewContext();
            var (id, instances) = await SeedRequestAsync(db, (Campus1, VisitInstanceStatuses.WaitingRequestApproval));
            requestId = id;
            var instanceId = instances[0];
            var decidedAt = TruncateToSecond(DateTime.Now);

            var legacyAudit = await SeedLegacyDecisionAuditAsync(
                db, requestId, instanceId, Campus1, CampusDecisionAudit.Rejected, SlCampus1, decidedAt);
            // Malformed JSON — not valid at all.
            await SeedSingularResubmitSnapshotAsync(
                db, requestId, instanceId, Campus1, VisitInstanceStatuses.Rejected, SlCampus1, decidedAt,
                null, DateTime.Now.AddMinutes(5), rawJsonOverride: "{not-valid-json,,,");

            // Must not throw / abort the whole run — an unhandled exception here would fail the test on
            // its own, which is the correct failure mode for "malformed JSON corrupted the transaction".
            var result = await RunAsync(dryRun: false);
            // The malformed row is reported as an error and produces no match — nothing enriched from it.
            Assert.Equal(0, result.DecisionAuditsEnriched);
            Assert.True(result.Errors >= 1);

            using var check = NewContext();
            // The rest of the DB is untouched — no partial/corrupted rows from the failed parse.
            var reloaded = await check.AuditLogs.AsNoTracking().Include(a => a.Changes)
                .SingleAsync(a => a.AuditLogId == legacyAudit.AuditLogId);
            Assert.Empty(reloaded.Changes);
        }
        finally { await CleanupAsync(requestId); }
    }
}
