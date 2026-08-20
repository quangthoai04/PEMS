using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;
using PEMS.Shared;

namespace PEMS.Application.Delegations.Commands.BackfillVisitHistory;

/// <summary>
/// VISIT_HISTORY_INTEGRITY final phase, Phase C. See <see cref="BackfillVisitHistoryCommand"/> for the
/// governing principle. Three independent phases — decision, lifecycle, contact — each its own method,
/// each returning a <see cref="PhaseResult"/> the caller aggregates into the response. A dry run uses
/// <c>AsNoTracking</c> reads and never calls <c>SaveChangesAsync</c>, so it is structurally incapable of
/// writing — not merely told not to. A real run stages every mutation across all three phases on ONE
/// tracked context and flushes them in ONE transaction, so a mid-run failure leaves nothing partially
/// applied.
/// </summary>
public sealed class BackfillVisitHistoryCommandHandler
    : IRequestHandler<BackfillVisitHistoryCommand, BackfillVisitHistoryResponse>
{
    /// <summary>
    /// Provenance marker on a row this backfill CREATED outright (never on an enriched pre-existing
    /// row — enrichment only adds columns/Changes to something a live handler already wrote). Purely
    /// internal: the EventCode a backfilled row renders under on the timeline is the exact same code a
    /// live event of that kind uses (e.g. VisitHistoryEventCodes.InstanceRejected) — a reader has no
    /// way to tell a recovered event from a live one, by design (plan §C7).
    /// </summary>
    public const string RecoveredHistoryReason = "RECOVERED_HISTORY";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] DecisionCandidateActions =
        { CampusDecisionAudit.Approved, CampusDecisionAudit.ApprovedWithScheduleWarning, CampusDecisionAudit.Rejected };

    private static readonly string[] ContactCandidateActions =
        { OperationalContactHistoryAudit.ProfileUpdated, OperationalContactHistoryAudit.Replaced };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly ILogger<BackfillVisitHistoryCommandHandler> _logger;

    public BackfillVisitHistoryCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, IDateTimeService clock,
        ILogger<BackfillVisitHistoryCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackfillVisitHistoryResponse> Handle(
        BackfillVisitHistoryCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();
        if (!string.Equals(_currentUser.RoleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Chỉ ADMIN mới được chạy backfill lịch sử.");

        if (request.DryRun)
        {
            var decisionDry = await BackfillDecisionsAsync(dryRun: true, ct);
            var lifecycleDry = await BackfillLifecycleAsync(dryRun: true, ct);
            var contactDry = await BackfillContactScopeAsync(dryRun: true, ct);
            return BuildResponse(true, decisionDry, lifecycleDry, contactDry);
        }

        var actorId = _currentUser.UserId.Value;
        var now = _clock.VietnamNow;

        await using var tx = await _db.BeginTransactionAsync(ct);

        var decision = await BackfillDecisionsAsync(dryRun: false, ct);
        var lifecycle = await BackfillLifecycleAsync(dryRun: false, ct);
        var contact = await BackfillContactScopeAsync(dryRun: false, ct);

        var totalErrors = decision.Errors + lifecycle.Errors + contact.Errors;
        var summaryAudit = new AuditLog
        {
            ActorUserId = actorId,
            Action = "BACKFILL_VISIT_HISTORY",
            EntityType = "VisitRequest",
            SourceType = "MAINTENANCE",
            Reason = RecoveredHistoryReason,
            CreatedAt = now,
        };
        summaryAudit.Changes.Add(new AuditLogChange
        {
            FieldName = "Summary",
            NewValueText = $"decisionEnriched={decision.Enriched};decisionCreated={decision.Created};" +
                $"decisionAmbiguous={decision.Ambiguous};decisionInsufficient={decision.Insufficient};" +
                $"lifecycleEnriched={lifecycle.Enriched};lifecycleCreated={lifecycle.Created};" +
                $"lifecycleInsufficient={lifecycle.Insufficient};" +
                $"contactScoped={contact.Enriched};contactInsufficient={contact.Insufficient};" +
                $"errors={totalErrors}",
            CreatedAt = now,
        });
        _db.AuditLogs.Add(summaryAudit);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Visit history backfill EXECUTE: decision enriched={DecEnriched} created={DecCreated} " +
            "ambiguous={DecAmb} insufficient={DecIns}; lifecycle enriched={LcEnriched} created={LcCreated} " +
            "insufficient={LcIns}; contact scoped={CtScoped} insufficient={CtIns}; errors={Errors}",
            decision.Enriched, decision.Created, decision.Ambiguous, decision.Insufficient,
            lifecycle.Enriched, lifecycle.Created, lifecycle.Insufficient,
            contact.Enriched, contact.Insufficient, totalErrors);

        return BuildResponse(false, decision, lifecycle, contact);
    }

    private static BackfillVisitHistoryResponse BuildResponse(
        bool dryRun, PhaseResult decision, PhaseResult lifecycle, PhaseResult contact) => new()
    {
        DryRun = dryRun,
        DecisionCandidatesScanned = decision.Scanned,
        DecisionAuditsEnriched = decision.Enriched,
        DecisionAuditsCreated = decision.Created,
        DecisionSkippedAmbiguous = decision.Ambiguous,
        DecisionSkippedInsufficientEvidence = decision.Insufficient,
        DecisionAlreadyComplete = decision.AlreadyComplete,
        LifecycleCandidatesScanned = lifecycle.Scanned,
        LifecycleAuditsEnriched = lifecycle.Enriched,
        LifecycleCloseEventsCreated = lifecycle.Created,
        LifecycleSkippedInsufficientEvidence = lifecycle.Insufficient,
        LifecycleAlreadyComplete = lifecycle.AlreadyComplete,
        ContactCandidatesScanned = contact.Scanned,
        ContactAuditsScoped = contact.Enriched,
        ContactSkippedInsufficientEvidence = contact.Insufficient,
        ContactAlreadyComplete = contact.AlreadyComplete,
        Errors = decision.Errors + lifecycle.Errors + contact.Errors,
        Message = dryRun
            ? "Dry-run hoàn tất — chưa ghi gì vào database."
            : "Backfill hoàn tất.",
    };

    private readonly record struct PhaseResult(
        int Scanned, int Enriched, int Created, int Ambiguous, int Insufficient, int AlreadyComplete, int Errors);

    // ── Phase C2 — campus decisions ─────────────────────────────────────────────────────────

    /// <summary>One decision fact recovered from a resubmit's pre-clear snapshot.</summary>
    private sealed record SnapshotEntry(
        ulong VisitInstanceId, ulong? CampusId, string? OldStatus,
        ulong? DecidedBy, DateTime? DecidedAt, string? DecisionNote, ulong? VisitRequestId);

    private async Task<PhaseResult> BackfillDecisionsAsync(bool dryRun, CancellationToken ct)
    {
        // ── Load every resubmit snapshot (BF-12: a malformed one is skipped + counted, never thrown) ──
        var singular = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "RESUBMIT_REJECTED_VISIT_INSTANCE_V2")
            .Select(a => new
            {
                a.VisitRequestId,
                Json = a.Changes.Where(c => c.FieldName == "campus_decision_before_resubmit_json")
                    .Select(c => c.OldValueText).FirstOrDefault(),
            })
            .Where(x => x.Json != null)
            .ToListAsync(ct);
        var plural = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == "RESUBMIT_REJECTED_VISIT_REQUEST_V2")
            .Select(a => new
            {
                a.VisitRequestId,
                Json = a.Changes.Where(c => c.FieldName == "campus_decisions_before_resubmit_json")
                    .Select(c => c.OldValueText).FirstOrDefault(),
            })
            .Where(x => x.Json != null)
            .ToListAsync(ct);

        var entries = new List<SnapshotEntry>();
        var parseErrors = 0;
        foreach (var s in singular)
        {
            var parsed = TryParseSnapshotObject(s.Json!, s.VisitRequestId);
            if (parsed is null) { parseErrors++; continue; }
            entries.Add(parsed);
        }
        foreach (var p in plural)
        {
            var parsed = TryParseSnapshotArray(p.Json!, p.VisitRequestId);
            if (parsed is null) { parseErrors++; continue; }
            entries.AddRange(parsed);
        }

        // Only entries carrying a REAL decision (both decidedBy and decidedAt present) are usable
        // evidence — a never-decided sibling swept into a whole-request resubmit snapshot proves
        // nothing about that campus (plan §C3: no genuine decision markers → no backfill).
        var byKey = entries
            .Where(e => e.DecidedBy is not null && e.DecidedAt is not null)
            .GroupBy(e => (e.VisitInstanceId, Actor: e.DecidedBy!.Value, At: e.DecidedAt!.Value))
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── C2.1 — enrich an existing legacy audit (Approved/Rejected, zero Changes rows) ──
        var scanned = await _db.AuditLogs.AsNoTracking()
            .Where(a => DecisionCandidateActions.Contains(a.Action)).CountAsync(ct);

        var candidateQuery = dryRun ? _db.AuditLogs.AsNoTracking() : (IQueryable<AuditLog>)_db.AuditLogs.Include(a => a.Changes);
        var candidates = await candidateQuery
            .Where(a => DecisionCandidateActions.Contains(a.Action) && a.VisitInstanceId != null && !a.Changes.Any())
            .ToListAsync(ct);

        var enriched = 0;
        var ambiguous = 0;
        var insufficient = 0;
        var now = _clock.VietnamNow;

        foreach (var audit in candidates)
        {
            var key = (audit.VisitInstanceId!.Value, Actor: audit.ActorUserId ?? 0, At: audit.CreatedAt);
            if (audit.ActorUserId is null || !byKey.TryGetValue(key, out var matches))
            {
                insufficient++;
                continue;
            }

            var isApproval = CampusDecisionAudit.IsApproval(audit.Action);
            var consistent = matches.Where(m => isApproval
                ? m.OldStatus is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit
                    or VisitInstanceStatuses.DuringVisit or VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed
                : m.OldStatus == VisitInstanceStatuses.Rejected)
                .ToList();

            if (consistent.Count > 1) { ambiguous++; continue; }
            if (consistent.Count == 0) { insufficient++; continue; }

            var note = consistent[0].DecisionNote;
            if (string.IsNullOrWhiteSpace(note)) { insufficient++; continue; } // matched, nothing new to add

            if (!dryRun)
                audit.Changes.Add(new AuditLogChange
                {
                    FieldName = "decision_note", NewValueText = note, CreatedAt = now,
                });
            enriched++;
        }

        // ── C2.2 — orphaned snapshot entry, no matching audit at all (defensive: expected to find none
        //    on this schema, since the same handler that decides a campus always writes SOME AuditLog
        //    row for it, even pre-Commit-2 — see the final report's Phase C section) ──
        var existingKeys = (await _db.AuditLogs.AsNoTracking()
            .Where(a => DecisionCandidateActions.Contains(a.Action) && a.VisitInstanceId != null)
            .Select(a => new { InstanceId = a.VisitInstanceId!.Value, Actor = a.ActorUserId ?? 0, a.CreatedAt })
            .ToListAsync(ct))
            .Select(x => (x.InstanceId, x.Actor, x.CreatedAt))
            .ToHashSet();

        var created = 0;
        foreach (var (keyTuple, matchList) in byKey)
        {
            if (existingKeys.Contains(keyTuple)) continue;
            if (matchList.Count != 1) { ambiguous++; continue; }

            var e = matchList[0];
            if (e.CampusId is null || e.VisitRequestId is null || e.OldStatus is null)
            {
                insufficient++;
                continue;
            }

            string action;
            if (e.OldStatus == VisitInstanceStatuses.Rejected) action = CampusDecisionAudit.Rejected;
            else if (e.OldStatus is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit
                     or VisitInstanceStatuses.DuringVisit or VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed)
                action = CampusDecisionAudit.Approved;
            else { insufficient++; continue; } // unrecognized outcome — do not guess

            if (!dryRun)
            {
                var backfilled = new AuditLog
                {
                    ActorUserId = e.DecidedBy,
                    Action = action,
                    EntityType = "VisitRequestCampus",
                    EntityId = e.VisitInstanceId,
                    CampusId = e.CampusId,
                    VisitRequestId = e.VisitRequestId,
                    VisitInstanceId = e.VisitInstanceId,
                    SourceType = CampusDecisionAudit.SourceType,
                    Reason = RecoveredHistoryReason,
                    CreatedAt = e.DecidedAt!.Value, // the ORIGINAL decision time, never migration time
                };
                backfilled.Changes.Add(new AuditLogChange
                {
                    FieldName = "visit_request_campuses.status",
                    NewValueText = e.OldStatus, CreatedAt = e.DecidedAt.Value,
                });
                if (!string.IsNullOrWhiteSpace(e.DecisionNote))
                    backfilled.Changes.Add(new AuditLogChange
                    {
                        FieldName = "decision_note", NewValueText = e.DecisionNote, CreatedAt = e.DecidedAt.Value,
                    });
                _db.AuditLogs.Add(backfilled);
            }
            created++;
        }

        return new PhaseResult(scanned, enriched, created, ambiguous, insufficient, scanned - candidates.Count, parseErrors);
    }

    private static SnapshotEntry? TryParseSnapshotObject(string json, ulong? visitRequestId)
    {
        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(json, Json);
            return FromElement(el, visitRequestId);
        }
        catch (JsonException) { return null; }
    }

    private static List<SnapshotEntry>? TryParseSnapshotArray(string json, ulong? visitRequestId)
    {
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement>(json, Json);
            if (arr.ValueKind != JsonValueKind.Array) return null;
            var list = new List<SnapshotEntry>();
            foreach (var el in arr.EnumerateArray())
            {
                var entry = FromElement(el, visitRequestId);
                if (entry is not null) list.Add(entry);
            }
            return list;
        }
        catch (JsonException) { return null; }
    }

    private static SnapshotEntry? FromElement(JsonElement el, ulong? visitRequestId)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        if (!el.TryGetProperty("visitInstanceId", out var vi) || !vi.TryGetUInt64(out var instanceId))
            return null;

        ulong? campusId = el.TryGetProperty("campusId", out var c)
            && c.ValueKind != JsonValueKind.Null && c.TryGetUInt64(out var cv) ? cv : null;
        var oldStatus = el.TryGetProperty("oldStatus", out var os) ? os.GetString() : null;
        ulong? decidedBy = el.TryGetProperty("decidedBy", out var db)
            && db.ValueKind != JsonValueKind.Null && db.TryGetUInt64(out var dbv) ? dbv : null;
        DateTime? decidedAt = el.TryGetProperty("decidedAt", out var da)
            && da.ValueKind != JsonValueKind.Null && da.TryGetDateTime(out var dav) ? dav : null;
        var decisionNote = el.TryGetProperty("decisionNote", out var dn) ? dn.GetString() : null;

        return new SnapshotEntry(instanceId, campusId, oldStatus, decidedBy, decidedAt, decisionNote, visitRequestId);
    }

    // ── Phase C4 — lifecycle transitions ────────────────────────────────────────────────────

    private async Task<PhaseResult> BackfillLifecycleAsync(bool dryRun, CancellationToken ct)
    {
        var lifecycleActions = VisitLifecycleHistoryAudit.LifecycleActions;

        var scanned = await _db.AuditLogs.AsNoTracking()
            .Where(a => lifecycleActions.Contains(a.Action)).CountAsync(ct);

        // ── C4.1 — legacy row missing scope columns and/or the structured status AuditLogChange.
        //    EntityId (EntityType="VisitRequestCampus") has always been set by every version of every
        //    lifecycle writer — it is the deterministic key back to the campus row. ──
        var candidateQuery = dryRun ? _db.AuditLogs.AsNoTracking() : (IQueryable<AuditLog>)_db.AuditLogs.Include(a => a.Changes);
        var candidates = await candidateQuery
            .Where(a => lifecycleActions.Contains(a.Action) && a.EntityType == "VisitRequestCampus"
                        && (a.VisitRequestId == null || a.VisitInstanceId == null || a.CampusId == null
                            || a.SourceType == null || !a.Changes.Any()))
            .ToListAsync(ct);

        var instanceIds = candidates.Where(a => a.EntityId != null).Select(a => a.EntityId!.Value).Distinct().ToList();
        var campusRows = instanceIds.Count == 0
            ? new Dictionary<ulong, (ulong VisitRequestId, ulong CampusId)>()
            : await _db.VisitRequestCampuses.AsNoTracking()
                .Where(c => instanceIds.Contains(c.VisitInstanceId))
                .ToDictionaryAsync(c => c.VisitInstanceId, c => (c.VisitRequestId, c.CampusId), ct);

        var enriched = 0;
        var insufficient = 0;
        var now = _clock.VietnamNow;

        foreach (var audit in candidates)
        {
            if (audit.EntityId is null || !campusRows.TryGetValue(audit.EntityId.Value, out var row))
            {
                insufficient++;
                continue;
            }

            // The action DETERMINISTICALLY implies the transition — this is the one case in this whole
            // command where the outcome is not read from a snapshot, because the action name IS the
            // fact (plan §C4.1).
            (string Old, string New)? transition = audit.Action switch
            {
                var a when a == VisitLifecycleHistoryAudit.PreparationStarted
                    => (VisitInstanceStatuses.Assigned, VisitInstanceStatuses.BeforeVisit),
                var a when a == VisitLifecycleHistoryAudit.CompleteBeforeVisit
                    => (VisitInstanceStatuses.BeforeVisit, VisitInstanceStatuses.DuringVisit),
                var a when a == VisitLifecycleHistoryAudit.CompleteDuringVisit
                    => (VisitInstanceStatuses.DuringVisit, VisitInstanceStatuses.AfterVisit),
                var a when a == VisitLifecycleHistoryAudit.CloseVisitInstance
                    => (VisitInstanceStatuses.AfterVisit, VisitInstanceStatuses.Closed),
                _ => null,
            };
            if (transition is null) { insufficient++; continue; } // unreachable — lifecycleActions is exhaustive

            if (!dryRun)
            {
                audit.VisitRequestId ??= row.VisitRequestId;
                audit.VisitInstanceId ??= audit.EntityId;
                audit.CampusId ??= row.CampusId;
                audit.SourceType ??= VisitLifecycleHistoryAudit.SourceType;
                if (!audit.Changes.Any(c => c.FieldName == "visit_request_campuses.status"))
                    audit.Changes.Add(new AuditLogChange
                    {
                        FieldName = "visit_request_campuses.status",
                        OldValueText = transition.Value.Old, NewValueText = transition.Value.New, CreatedAt = now,
                    });
            }
            enriched++;
        }

        // ── C4.2 — CLOSED with ClosedAt/ClosedBy set (the ONLY writer of those two columns is the real
        //    close transition in CompleteVisitStageCommandHandler) but no CLOSE_VISIT_INSTANCE audit at
        //    all. Never infers VISIT_STARTED/VISIT_COMPLETED from this — only the close itself. ──
        var closedInstanceIdsWithAudit = (await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == VisitLifecycleHistoryAudit.CloseVisitInstance && a.VisitInstanceId != null)
            .Select(a => a.VisitInstanceId!.Value)
            .Distinct().ToListAsync(ct))
            .ToHashSet();

        var closedWithoutAudit = await _db.VisitRequestCampuses.AsNoTracking()
            .Where(c => c.Status == VisitInstanceStatuses.Closed && c.ClosedAt != null && c.ClosedBy != null)
            .Select(c => new { c.VisitInstanceId, c.VisitRequestId, c.CampusId, c.ClosedAt, c.ClosedBy })
            .ToListAsync(ct);

        var created = 0;
        foreach (var c in closedWithoutAudit)
        {
            if (closedInstanceIdsWithAudit.Contains(c.VisitInstanceId)) continue;

            if (!dryRun)
            {
                var backfilled = new AuditLog
                {
                    ActorUserId = c.ClosedBy,
                    Action = VisitLifecycleHistoryAudit.CloseVisitInstance,
                    EntityType = "VisitRequestCampus",
                    EntityId = c.VisitInstanceId,
                    CampusId = c.CampusId,
                    VisitRequestId = c.VisitRequestId,
                    VisitInstanceId = c.VisitInstanceId,
                    SourceType = VisitLifecycleHistoryAudit.SourceType,
                    Reason = RecoveredHistoryReason,
                    CreatedAt = c.ClosedAt!.Value, // the ORIGINAL close time, never migration time
                };
                backfilled.Changes.Add(new AuditLogChange
                {
                    FieldName = "visit_request_campuses.status",
                    OldValueText = VisitInstanceStatuses.AfterVisit,
                    NewValueText = VisitInstanceStatuses.Closed,
                    CreatedAt = c.ClosedAt.Value,
                });
                _db.AuditLogs.Add(backfilled);
            }
            created++;
        }

        return new PhaseResult(scanned, enriched, created, 0, insufficient, scanned - candidates.Count, Errors: 0);
    }

    // ── Phase C5 — contact profile/replacement scope ────────────────────────────────────────

    private async Task<PhaseResult> BackfillContactScopeAsync(bool dryRun, CancellationToken ct)
    {
        var scanned = await _db.AuditLogs.AsNoTracking()
            .Where(a => ContactCandidateActions.Contains(a.Action)).CountAsync(ct);

        var candidateQuery = dryRun ? _db.AuditLogs.AsNoTracking() : (IQueryable<AuditLog>)_db.AuditLogs;
        var candidates = await candidateQuery
            .Where(a => ContactCandidateActions.Contains(a.Action) && a.EntityType == "VisitRequestCampus"
                        && (a.VisitRequestId == null || a.VisitInstanceId == null || a.CampusId == null))
            .ToListAsync(ct);

        var instanceIds = candidates.Where(a => a.EntityId != null).Select(a => a.EntityId!.Value).Distinct().ToList();
        var campusRows = instanceIds.Count == 0
            ? new Dictionary<ulong, (ulong VisitRequestId, ulong CampusId)>()
            : await _db.VisitRequestCampuses.AsNoTracking()
                .Where(c => instanceIds.Contains(c.VisitInstanceId))
                .ToDictionaryAsync(c => c.VisitInstanceId, c => (c.VisitRequestId, c.CampusId), ct);

        var scoped = 0;
        var insufficient = 0;
        foreach (var audit in candidates)
        {
            // No inference from email/name — ONLY the deterministic EntityId → VisitRequestCampus
            // relation (plan §C5.1/§C5.2: "không infer cross-campus association từ email").
            if (audit.EntityId is null || !campusRows.TryGetValue(audit.EntityId.Value, out var row))
            {
                insufficient++;
                continue;
            }

            if (!dryRun)
            {
                audit.VisitRequestId ??= row.VisitRequestId;
                audit.VisitInstanceId ??= audit.EntityId;
                audit.CampusId ??= row.CampusId;
            }
            scoped++;
        }

        return new PhaseResult(scanned, scoped, 0, 0, insufficient, scanned - candidates.Count, Errors: 0);
    }
}
