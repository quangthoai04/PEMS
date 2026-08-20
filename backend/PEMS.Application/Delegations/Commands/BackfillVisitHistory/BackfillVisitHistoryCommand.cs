using MediatR;

namespace PEMS.Application.Delegations.Commands.BackfillVisitHistory;

/// <summary>
/// One-off, deliberately-triggered recovery of pre-existing history gaps left by Commits 1–4
/// (VISIT_HISTORY_INTEGRITY final phase, Phase C). Never runs automatically at startup. ADMIN-only —
/// this mutates audit_logs across every visit request, not one campus or one requester's own data, so
/// it belongs with the rest of the System Administration Console's audit-trail actions, not with any
/// per-request/per-campus role.
///
/// <para>
/// Recovers ONLY what committed, immutable evidence already proves happened — never invents an
/// actor, a timestamp, a reason, or a chain of events from a row's CURRENT state. Two kinds of work:
/// <b>enrichment</b> (an existing AuditLog row is missing scope columns or a structured
/// <c>AuditLogChange</c> that a deterministic, already-known fact can fill in) and <b>creation</b> (no
/// AuditLog row exists at all, but another immutable, exclusively-written source — a resubmit
/// snapshot, <c>ClosedAt</c>/<c>ClosedBy</c> — proves the event). A candidate that cannot be resolved
/// to exactly one answer is skipped and counted, never guessed.
/// </para>
/// <para>
/// Idempotent: every candidate query filters to rows that are STILL incomplete, so a second run finds
/// nothing left to do. <see cref="DryRun"/> runs the identical matching logic and reports the identical
/// counts a real run would, without writing anything.
/// </para>
/// </summary>
public sealed record BackfillVisitHistoryCommand(bool DryRun) : IRequest<BackfillVisitHistoryResponse>;

public sealed class BackfillVisitHistoryResponse
{
    public bool DryRun { get; init; }

    // ── Decision (Phase C2) ──
    public int DecisionCandidatesScanned { get; init; }
    public int DecisionAuditsEnriched { get; init; }
    public int DecisionAuditsCreated { get; init; }
    public int DecisionSkippedAmbiguous { get; init; }
    public int DecisionSkippedInsufficientEvidence { get; init; }
    public int DecisionAlreadyComplete { get; init; }

    // ── Lifecycle (Phase C4) ──
    public int LifecycleCandidatesScanned { get; init; }
    public int LifecycleAuditsEnriched { get; init; }
    public int LifecycleCloseEventsCreated { get; init; }
    public int LifecycleSkippedInsufficientEvidence { get; init; }
    public int LifecycleAlreadyComplete { get; init; }

    // ── Contact (Phase C5) ──
    public int ContactCandidatesScanned { get; init; }
    public int ContactAuditsScoped { get; init; }
    public int ContactSkippedInsufficientEvidence { get; init; }
    public int ContactAlreadyComplete { get; init; }

    public int Errors { get; init; }
    public string Message { get; init; } = string.Empty;
}
