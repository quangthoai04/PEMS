using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

using PEMS.Application.Delegations.Common;
namespace PEMS.Application.Delegations.Commands.VisitAmendments;

/// <summary>
/// Scoped business-history timeline (plan §16.6 / handoff §7.7). METADATA ONLY — no snapshot JSON, no
/// tokens/IP/UA, no full emails (identity entries surface the MASKED email). Scope:
///   • registrant / ACTIVE primary contact → whole own request (incl. identity timeline);
///   • HO → whole request read-only (incl. masked identity timeline);
///   • Staff Leader → only instances of their primary campus (no identity timeline);
///   • current Host → only their instance(s) (no identity timeline);
///   • anyone else → 403.
/// Proposed amendments are clearly distinct from applied revisions — a proposal is never presented as
/// active content.
/// </summary>
public sealed class GetVisitRequestHistoryQueryHandler
    : IRequestHandler<GetVisitRequestHistoryQuery, VisitRequestHistoryResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PerCampusFormV2Options _readFlag;

    public GetVisitRequestHistoryQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PerCampusFormV2Options readFlag)
    {
        _db = db;
        _currentUser = currentUser;
        _readFlag = readFlag;
    }

    public async Task<VisitRequestHistoryResponse> Handle(
        GetVisitRequestHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_readFlag.Enabled)
            throw new NotFoundException("Không tìm thấy.");
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new ForbiddenException();

        var visit = await _db.VisitRequests.AsNoTracking()
            .Include(v => v.CampusInstances)
            .FirstOrDefaultAsync(v => v.VisitRequestId == request.VisitRequestId, cancellationToken)
            ?? throw new NotFoundException("Đơn đăng ký tham quan", request.VisitRequestId);

        // ── Scope resolution BEFORE any projection ──
        // Resolved by VisitHistoryVisibility, which the V2 detail read model also asks before it offers
        // the section at all — so "the page showed Change History" and "the endpoint served it" cannot
        // drift apart the way they did when this decision lived here alone.
        var scope = VisitHistoryVisibility.Resolve(visit, _currentUser);
        if (!scope.CanViewHistory)
            throw new ForbiddenException("Bạn không có quyền xem lịch sử của đơn này.");

        // Materialized as a List so the `Contains` below keeps binding to the overload EF has always
        // translated into an IN clause.
        var visibleInstanceIds = scope.VisibleInstanceIds.ToList();
        var includeIdentity = scope.IncludeIdentity;
        var includeRequestLevel = scope.IncludeRequestLevel;

        // ── Campus names for the visible instances ──
        // Without these, a three-campus request emits three identical "content created" rows and the
        // reader cannot tell which campus each one belongs to.
        var visibleCampusIds = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .Select(c => c.CampusId).Distinct().ToList();
        var campusNameById = visibleCampusIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Campuses.AsNoTracking()
                .Where(c => visibleCampusIds.Contains(c.CampusId))
                .ToDictionaryAsync(c => c.CampusId, c => c.Name, cancellationToken);
        var campusNameByInstance = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .ToDictionary(
                c => c.VisitInstanceId,
                c => campusNameById.TryGetValue(c.CampusId, out var n) ? n : null);
        string? CampusOf(ulong? instanceId) =>
            instanceId is { } id && campusNameByInstance.TryGetValue(id, out var n) ? n : null;

        // Raw facts are collected first, then names are attached in one pass — the previous version
        // built the name dictionary and then never used it, so every entry went out with a null actor.
        var raw = new List<(VisitHistoryEntryDto Entry, ulong? ActorId)>();
        var actorIds = new HashSet<ulong>();

        void Add(VisitHistoryEntryDto entry, ulong? actorId)
        {
            if (actorId is { } id) actorIds.Add(id);
            raw.Add((entry, actorId));
        }

        // ── Instance revisions (applied, immutable) ──
        // Recovered baselines are excluded. They are written by VisitRevisionBaselineGuard purely so
        // the NEXT revision has something to diff against on data whose chain was never complete —
        // nobody performed them, and rendering one as "đã sửa nội dung" would put a user action in
        // the timeline that never happened. They remain fully readable AS the before-side of the real
        // revision that follows them, which is the only job they have.
        var instanceRevisions = await _db.VisitInstanceFormRevisionHistories.AsNoTracking()
            .Where(r => r.VisitRequestId == visit.VisitRequestId
                        && visibleInstanceIds.Contains(r.VisitInstanceId)
                        && r.Reason != RecoveredBaselineReason)
            .Select(r => new { r.RevisionHistoryId, r.VisitInstanceId, r.FormRevision, r.ApprovalRevision, r.SourceType, r.AppliedBy, r.AppliedAt })
            .ToListAsync(cancellationToken);
        foreach (var r in instanceRevisions)
        {
            Add(new VisitHistoryEntryDto(
                r.AppliedAt, InstanceRevisionCode(r.SourceType, r.FormRevision),
                VisitHistoryEventSources.Build(VisitHistoryEventSources.InstanceRevision, r.RevisionHistoryId),
                r.VisitInstanceId, CampusOf(r.VisitInstanceId), null,
                r.FormRevision, r.ApprovalRevision, null, null, r.SourceType, null, null, null, null),
                r.AppliedBy);
        }

        // ── Request-level revisions ──
        if (includeRequestLevel)
        {
            var requestRevisions = await _db.VisitRequestRevisionHistories.AsNoTracking()
                .Where(r => r.VisitRequestId == visit.VisitRequestId
                            && r.Reason != RecoveredBaselineReason)
                .Select(r => new { r.RequestRevisionHistoryId, r.RequestRevision, r.SourceType, r.AppliedBy, r.AppliedAt })
                .ToListAsync(cancellationToken);
            foreach (var r in requestRevisions)
            {
                Add(new VisitHistoryEntryDto(
                    r.AppliedAt, RequestRevisionCode(r.SourceType),
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.RequestRevision, r.RequestRevisionHistoryId),
                    null, null, null,
                    r.RequestRevision, null, null, null, r.SourceType, null, null, null, null),
                    r.AppliedBy);
            }

            // Cancelling the whole request is recorded on the request row itself, not in a revision
            // table — so without this the timeline simply stopped at the last edit and never said the
            // request had been called off, by whom, or why.
            if (visit.CancelledAt is { } requestCancelledAt)
            {
                // No eventId: a cancellation carries its reason on the line itself, so an eye button
                // would open a drawer that repeats what is already on screen.
                Add(new VisitHistoryEntryDto(
                    requestCancelledAt, VisitHistoryEventCodes.RequestCancelled, null, null, null, null,
                    null, null, null, VisitRequestStatuses.Cancelled, null,
                    visit.CancellationReason, null, null, null),
                    visit.CancelledBy);
            }
        }

        // ── Amendments (proposals + decisions — NEVER presented as active content) ──
        var amendments = await _db.VisitInstanceAmendments.AsNoTracking()
            .Where(a => a.VisitRequestId == visit.VisitRequestId
                        && visibleInstanceIds.Contains(a.VisitInstanceId))
            .Select(a => new { a.AmendmentId, a.VisitInstanceId, a.AmendmentNo, a.Status, a.RequestedBy, a.RequestedAt, a.DecidedBy, a.DecidedAt, a.DecisionNote })
            .ToListAsync(cancellationToken);
        foreach (var a in amendments)
        {
            Add(new VisitHistoryEntryDto(
                a.RequestedAt, VisitHistoryEventCodes.AmendmentSubmitted,
                VisitHistoryEventSources.Build(VisitHistoryEventSources.AmendmentSubmitted, a.AmendmentId),
                a.VisitInstanceId,
                CampusOf(a.VisitInstanceId), null, null, null, a.AmendmentNo,
                null, null, null, null, null, null),
                a.RequestedBy);

            if (a.DecidedAt is { } decidedAt)
            {
                var code = a.Status switch
                {
                    AmendmentStatuses.Approved => VisitHistoryEventCodes.AmendmentApproved,
                    AmendmentStatuses.Rejected => VisitHistoryEventCodes.AmendmentRejected,
                    AmendmentStatuses.Withdrawn => VisitHistoryEventCodes.AmendmentWithdrawn,
                    _ => VisitHistoryEventCodes.AmendmentDecided,
                };
                Add(new VisitHistoryEntryDto(
                    decidedAt, code,
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.AmendmentDecided, a.AmendmentId),
                    a.VisitInstanceId, CampusOf(a.VisitInstanceId), null,
                    null, null, a.AmendmentNo, a.Status, null, a.DecisionNote, null, null, null),
                    a.DecidedBy);
            }
        }

        // ── Campus decisions (immutable, append-only — VISIT_HISTORY_INTEGRITY plan Fix Group B) ──
        //
        // Approval/rejection decisions are read from the immutable audit CampusApprovalExecutor and
        // RejectCampusInstanceCommandHandler write at the moment of the decision, never from the
        // current campus row: a resubmit clears DecidedAt/DecisionNote off the current row (the DB
        // refuses decision metadata on a campus back in review), so a decision that only ever lived on
        // the current row would vanish the instant the registrant tried again. The current row is used
        // ONLY as a legacy fallback below, for an instance whose decision predates this audit capture.
        var decidableInstances = visit.CampusInstances
            .Where(c => visibleInstanceIds.Contains(c.VisitInstanceId))
            .ToDictionary(c => c.VisitInstanceId);

        var instancesWithImmutableDecision = new HashSet<ulong>();
        if (decidableInstances.Count > 0)
        {
            var decisionAudits = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == visit.VisitRequestId
                            && a.VisitInstanceId != null
                            && decidableInstances.Keys.Contains(a.VisitInstanceId!.Value)
                            && CampusDecisionAudit.DecisionActions.Contains(a.Action))
                .Select(a => new
                {
                    a.AuditLogId, a.VisitInstanceId, a.Action, a.ActorUserId, a.CreatedAt,
                    Changes = a.Changes
                        .Select(c => new { c.FieldName, c.NewValueText })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            // Every decision audit this instance has ever recorded, grouped so uniqueness can be
            // proven per-instance below — a legacy audit may only borrow the current row's note when
            // it is the ONE audit (of possibly several sharing the same whole-second timestamp; see
            // CampusDecisionAudit.CanEnrichFromCurrentRow) that row's tuple resolves to.
            var decisionAuditsByInstance = decisionAudits
                .GroupBy(d => d.VisitInstanceId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<CampusDecisionAudit.DecisionAuditFacts>)g
                        .Select(d => new CampusDecisionAudit.DecisionAuditFacts(d.Action, d.CreatedAt, d.ActorUserId))
                        .ToList());

            foreach (var d in decisionAudits)
            {
                var instanceId = d.VisitInstanceId!.Value;
                instancesWithImmutableDecision.Add(instanceId);

                // Three distinct decision outcomes, not two — a host-proposal activation is a genuine
                // decision (same decided_by/decided_at/status shape as an approval) but a DIFFERENT
                // event, never rendered as InstanceApproved (VisitHistoryEventCodes.HostProposalActivated
                // remarks explain why).
                var isHostActivation = d.Action == CampusDecisionAudit.HostProposalActivated;
                var isApproval = !isHostActivation && CampusDecisionAudit.IsApproval(d.Action);
                var eventCode = isHostActivation
                    ? VisitHistoryEventCodes.HostProposalActivated
                    : isApproval ? VisitHistoryEventCodes.InstanceApproved : VisitHistoryEventCodes.InstanceRejected;
                var statusCode = d.Changes.FirstOrDefault(c => c.FieldName == "visit_request_campuses.status")?.NewValueText
                    ?? (isApproval || isHostActivation ? VisitInstanceStatuses.Assigned : VisitInstanceStatuses.Rejected);
                var note = d.Changes.FirstOrDefault(c => c.FieldName == "decision_note")?.NewValueText;

                // Legacy enrichment (Fix Group B §D continuation): this audit predates AuditLogChange
                // capture, so it has no decision_note of its own — but if the CURRENT campus row is
                // verifiably and UNIQUELY still describing THIS exact decision (never a later resubmit
                // cycle's, and never ambiguous against a same-second sibling), borrow its DecisionNote
                // rather than reporting the reason as permanently unknown. Not applicable to a host
                // activation: it has always written its own AuditLogChange (never a legacy gap), sets no
                // decision_note on the campus row, and CanEnrichFromCurrentRow's approve/reject status-
                // class check has no branch for it.
                if (note is null && !isHostActivation && decidableInstances.TryGetValue(instanceId, out var current)
                    && CampusDecisionAudit.CanEnrichFromCurrentRow(
                        new CampusDecisionAudit.DecisionAuditFacts(d.Action, d.CreatedAt, d.ActorUserId),
                        decisionAuditsByInstance[instanceId],
                        current.Status, current.DecidedAt, current.DecidedBy))
                    note = current.DecisionNote;

                Add(new VisitHistoryEntryDto(
                    d.CreatedAt, eventCode,
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, d.AuditLogId),
                    instanceId, CampusOf(instanceId), null,
                    null, null, null, statusCode, null, note, null, null, null),
                    d.ActorUserId);
            }
        }

        // ── Lifecycle transitions (immutable, append-only — VISIT_HISTORY_INTEGRITY plan Fix Group F) ──
        //
        // BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED are read from the immutable audit
        // CompleteVisitStageCommandHandler writes at the moment of EACH transition — never
        // reconstructed from the campus's current status. The current row only ever holds the
        // LATEST stage: a campus that is currently CLOSED does not prove it was ever audited through
        // DURING_VISIT, so — unlike campus decisions above — there is deliberately NO current-row
        // fallback here. A legacy stage transition with no audit row is unknown, not inferred.
        if (visibleInstanceIds.Count > 0)
        {
            var lifecycleAudits = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == visit.VisitRequestId
                            && a.VisitInstanceId != null
                            && visibleInstanceIds.Contains(a.VisitInstanceId!.Value)
                            && VisitLifecycleHistoryAudit.LifecycleActions.Contains(a.Action))
                .Select(a => new
                {
                    a.AuditLogId, a.VisitInstanceId, a.Action, a.ActorUserId, a.CreatedAt,
                    Changes = a.Changes
                        .Select(c => new { c.FieldName, c.OldValueText, c.NewValueText })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            foreach (var l in lifecycleAudits)
            {
                var statusChange = l.Changes.FirstOrDefault(c => c.FieldName == "visit_request_campuses.status");
                var eventCode = l.Action switch
                {
                    VisitLifecycleHistoryAudit.PreparationStarted => VisitHistoryEventCodes.VisitPreparationStarted,
                    VisitLifecycleHistoryAudit.CompleteBeforeVisit => VisitHistoryEventCodes.VisitStarted,
                    VisitLifecycleHistoryAudit.CompleteDuringVisit => VisitHistoryEventCodes.VisitCompleted,
                    VisitLifecycleHistoryAudit.CloseVisitInstance => VisitHistoryEventCodes.InstanceClosed,
                    _ => VisitHistoryEventCodes.InstanceDecided, // unreachable — LifecycleActions is exhaustive
                };
                Add(new VisitHistoryEntryDto(
                    l.CreatedAt, eventCode,
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, l.AuditLogId),
                    l.VisitInstanceId, CampusOf(l.VisitInstanceId), null,
                    null, null, null, statusChange?.NewValueText, null, null, null,
                    statusChange?.OldValueText, statusChange?.NewValueText),
                    l.ActorUserId);
            }
        }

        // ── Campus cancellations (unaffected by the above — cancellation is not decision) ──
        foreach (var c in visit.CampusInstances.Where(c => visibleInstanceIds.Contains(c.VisitInstanceId)))
        {
            // LEGACY FALLBACK ONLY: an instance with at least one immutable decision audit is fully
            // covered above, and rendering the current row too would duplicate the very decision the
            // audit already describes. This branch exists solely for data that predates the audit
            // capture above (Fix Group B §D) — never for anything decided by today's writers.
            if (c.DecidedAt is { } decidedAt && !instancesWithImmutableDecision.Contains(c.VisitInstanceId))
            {
                var code = c.Status switch
                {
                    VisitInstanceStatuses.Rejected => VisitHistoryEventCodes.InstanceRejected,
                    VisitInstanceStatuses.Assigned => VisitHistoryEventCodes.InstanceApproved,
                    // A campus that has moved on (BEFORE/DURING/AFTER/CLOSED) was approved to get there;
                    // the decision event still describes that approval.
                    VisitInstanceStatuses.BeforeVisit or VisitInstanceStatuses.DuringVisit
                        or VisitInstanceStatuses.AfterVisit or VisitInstanceStatuses.Closed
                        => VisitHistoryEventCodes.InstanceApproved,
                    _ => VisitHistoryEventCodes.InstanceDecided,
                };
                // A decision states its own outcome and note inline — nothing further to open.
                Add(new VisitHistoryEntryDto(
                    decidedAt, code, null, c.VisitInstanceId, CampusOf(c.VisitInstanceId), null,
                    null, null, null, c.Status, null, c.DecisionNote, null, null, null),
                    c.DecidedBy);
            }

            // Cancelling a campus is a separate event from deciding it, and used to be invisible here.
            if (c.CancelledAt is { } cancelledAt)
            {
                Add(new VisitHistoryEntryDto(
                    cancelledAt, VisitHistoryEventCodes.InstanceCancelled, null, c.VisitInstanceId,
                    CampusOf(c.VisitInstanceId), null, null, null, null,
                    VisitInstanceStatuses.Cancelled, c.CancellationSource, c.CancellationReason, null, null, null),
                    c.CancelledBy);
            }
        }

        // ── Host handovers. These live in audit_logs rather than a revision table: a transfer changes
        //    who runs the visit, not what the visit IS, so it writes no form revision by design. The
        //    entry is read from the immutable audit row, scoped to the instances this viewer may see. ──
        if (visibleInstanceIds.Count > 0)
        {
            var transfers = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == visit.VisitRequestId
                            && a.Action == VisitAuditActions.HostTransferred
                            && a.VisitInstanceId != null
                            && visibleInstanceIds.Contains(a.VisitInstanceId!.Value))
                .Select(a => new { a.AuditLogId, a.VisitInstanceId, a.ActorUserId, a.Reason, a.CreatedAt })
                .ToListAsync(cancellationToken);
            foreach (var tr in transfers)
            {
                Add(new VisitHistoryEntryDto(
                    tr.CreatedAt, VisitHistoryEventCodes.HostTransferred,
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, tr.AuditLogId),
                    tr.VisitInstanceId,
                    CampusOf(tr.VisitInstanceId), null, null, null, null, null,
                    VisitAuditActions.HostTransferSourceType, tr.Reason, null, null, null),
                    tr.ActorUserId);
            }
        }

        // ── Identity timeline (managers + HO only; masked emails only) ──
        //
        // Scoped to the campuses this viewer may see, like every other section. It used to return every
        // identity event of the whole request the moment `includeIdentity` was true — which it is for an
        // operational contact — so the contact of one campus could read who had been invited to, and had
        // declined, a campus that was never theirs.
        if (includeIdentity && visibleInstanceIds.Count > 0)
        {
            var identityEvents = await _db.VisitRequestIdentityChangeEvents.AsNoTracking()
                .Where(e => e.VisitRequestId == visit.VisitRequestId
                            && visibleInstanceIds.Contains(e.VisitInstanceId))
                .Select(e => new
                {
                    e.IdentityChangeEventId, e.VisitInstanceId, e.EventType,
                    e.FromStatus, e.ToStatus, e.ActorUserId, e.EmailMasked, e.CreatedAt,
                })
                .ToListAsync(cancellationToken);
            foreach (var e in identityEvents)
            {
                // The campus is named and the event is openable: an entry that said only "the contact
                // role changed", with no campus and no eye button, was unactionable on a request with
                // three campuses and three separate contacts.
                Add(new VisitHistoryEntryDto(
                    e.CreatedAt, VisitContactIdentityEventCodes.For(e.EventType),
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.IdentityChange, e.IdentityChangeEventId),
                    e.VisitInstanceId, CampusOf(e.VisitInstanceId), null,
                    // `reason` on these rows is plumbing more often than prose — "EXPIRY_JOB",
                    // "token_version=2;resend_count=1", "fields=…". Surfacing it would put internals
                    // under a heading that says "Lý do", so it stays out, as the correlation id does
                    // on revision rows.
                    null, null, null, e.EventType, null, null, e.EmailMasked, e.FromStatus, e.ToStatus),
                    e.ActorUserId);
            }
        }

        // ── Contact profile corrections + immediate self-match replacement ──
        // (Commit 3, Fix Group C/D — VISIT_HISTORY_INTEGRITY plan)
        //
        // Sourced from AuditLogs, not VisitRequestIdentityChangeEvents, because neither action ever
        // opens a VisitRequestIdentityChange row to hang an event off: a profile correction touches
        // no identity/token state at all (UpdateOperationalContactProfileCommandHandler's own remarks
        // say so explicitly), and the self-match branch of Replace links the registrant immediately
        // with no invitation. Scoped by the SAME includeIdentity + visibleInstanceIds gate as the
        // identity block above — this adds no visibility Staff Leader/Host did not already have.
        if (includeIdentity && visibleInstanceIds.Count > 0)
        {
            var contactAudits = await _db.AuditLogs.AsNoTracking()
                .Where(a => a.VisitRequestId == visit.VisitRequestId
                            && a.VisitInstanceId != null
                            && visibleInstanceIds.Contains(a.VisitInstanceId!.Value)
                            && (a.Action == OperationalContactHistoryAudit.ProfileUpdated
                                || a.Action == OperationalContactHistoryAudit.RelationUpdated
                                || a.Action == OperationalContactHistoryAudit.Replaced))
                .Select(a => new
                {
                    a.AuditLogId, a.VisitInstanceId, a.Action, a.ActorUserId, a.CreatedAt,
                    Changes = a.Changes.Select(c => new { c.FieldName, c.NewValueText }).ToList(),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in contactAudits)
            {
                string eventCode;
                if (a.Action == OperationalContactHistoryAudit.ProfileUpdated)
                {
                    eventCode = VisitHistoryEventCodes.ContactProfileUpdated;
                }
                else if (a.Action == OperationalContactHistoryAudit.RelationUpdated)
                {
                    eventCode = VisitHistoryEventCodes.ContactRelationUpdated;
                }
                else
                {
                    // Replaced covers TWO outcomes under one Action string. The external-address
                    // outcome (operational_contact_user_id cleared to null) is already told in full
                    // by that invitation's own INVITATION_CREATED/_SUPERSEDED events above —
                    // surfacing it again here would render one business action as two timeline rows.
                    // Only the self-match outcome (landed non-null) has no other event, so only it
                    // is ever emitted.
                    var newContactId = a.Changes
                        .FirstOrDefault(c => c.FieldName == "operational_contact_user_id")?.NewValueText;
                    if (string.IsNullOrEmpty(newContactId))
                        continue;
                    eventCode = VisitHistoryEventCodes.ContactReplacedWithRegistrant;
                }

                Add(new VisitHistoryEntryDto(
                    a.CreatedAt, eventCode,
                    VisitHistoryEventSources.Build(VisitHistoryEventSources.Audit, a.AuditLogId),
                    a.VisitInstanceId, CampusOf(a.VisitInstanceId), null,
                    null, null, null, null, null, null, null, null, null),
                    a.ActorUserId);
            }
        }

        // ── Actor display names (one query), then attach them ──
        var names = actorIds.Count == 0
            ? new Dictionary<ulong, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);

        var ordered = raw
            .Select(x => x.ActorId is { } id && names.TryGetValue(id, out var name)
                ? x.Entry with { ActorName = name }
                : x.Entry)
            .OrderByDescending(e => e.At)
            .ToList();

        return new VisitRequestHistoryResponse(
            visit.VisitRequestId, visit.RequestCode ?? string.Empty, ordered);
    }

    /// <summary>
    /// What a per-campus revision row MEANS, read from the reason it was written rather than from its
    /// number. Both "sửa nhanh" and "gửi lại đơn" already write these rows — the timeline just collapsed
    /// every one of them into "content created / revised", so a safe edit and a resubmit were
    /// indistinguishable from an ordinary edit. Revision 1 is still a creation for rows written before
    /// <c>source_type</c> was populated.
    /// </summary>
    /// <summary>
    /// Marker written by <c>VisitRevisionBaselineGuard</c> on a recovered baseline row. Mirrors its
    /// <c>BaselineReason</c> constant — Application cannot reference Infrastructure, and the value is
    /// a stored string rather than a shared symbol.
    /// </summary>
    private const string RecoveredBaselineReason = "RECOVERED_BASELINE";

    private static string InstanceRevisionCode(string? sourceType, uint formRevision) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.InstanceContentCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.InstanceSafeEditApplied,
        FormRevisionSourceTypes.PendingEdit => VisitHistoryEventCodes.InstancePendingEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.InstanceContentResubmitted,
        FormRevisionSourceTypes.AmendmentApplied => VisitHistoryEventCodes.InstanceAmendmentApplied,
        _ => formRevision <= 1
            ? VisitHistoryEventCodes.InstanceContentCreated
            : VisitHistoryEventCodes.InstanceContentRevised,
    };

    /// <summary>Same reading for the request-level (registrant + contact block) revisions.</summary>
    private static string RequestRevisionCode(string? sourceType) => sourceType switch
    {
        FormRevisionSourceTypes.Create => VisitHistoryEventCodes.RequestCreated,
        FormRevisionSourceTypes.SafeEdit => VisitHistoryEventCodes.RequestSafeEditApplied,
        FormRevisionSourceTypes.PendingEdit => VisitHistoryEventCodes.RequestPendingEditApplied,
        FormRevisionSourceTypes.Resubmit => VisitHistoryEventCodes.RequestResubmitted,
        _ => VisitHistoryEventCodes.RequestRevision,
    };
}
