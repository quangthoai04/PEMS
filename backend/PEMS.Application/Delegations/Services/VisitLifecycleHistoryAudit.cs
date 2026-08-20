using System.Collections.Generic;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Shared audit vocabulary for the four per-campus lifecycle-stage transitions
/// (ASSIGNED → BEFORE_VISIT → DURING_VISIT → AFTER_VISIT → CLOSED — Commit 4, Fix Group F, closed out
/// by the VISIT_HISTORY_INTEGRITY final phase). Named here, once, so every writer and both history
/// readers cannot drift apart — the same lesson <see cref="CampusDecisionAudit"/> and
/// <see cref="OperationalContactHistoryAudit"/> already exist to enforce for their own action sets.
///
/// <para>
/// Two different handlers write these four actions: <c>StartVisitPreparationCommandHandler</c> writes
/// <see cref="PreparationStarted"/> alone (ASSIGNED → BEFORE_VISIT); everything else is written by
/// <c>CompleteVisitStageCommandHandler</c>. Both already scope the audit row identically
/// (VisitRequestId/VisitInstanceId/CampusId/SourceType="LIFECYCLE"), so one shared whitelist is
/// enough — there is no second lifecycle vocabulary class.
/// </para>
/// </summary>
public static class VisitLifecycleHistoryAudit
{
    /// <summary>audit_logs.source_type for a per-campus lifecycle-stage transition.</summary>
    public const string SourceType = "LIFECYCLE";

    /// <summary>
    /// ASSIGNED → BEFORE_VISIT — the campus's Host opens the preparation stage. Written by
    /// StartVisitPreparationCommandHandler. Kept as its own event
    /// (<see cref="Application.Delegations.Commands.VisitAmendments.VisitHistoryEventCodes.VisitPreparationStarted"/>),
    /// never folded into <see cref="Application.Delegations.Commands.VisitAmendments.VisitHistoryEventCodes.VisitStarted"/>
    /// — that code already means BEFORE_VISIT → DURING_VISIT, a different transition on the same campus.
    /// </summary>
    public const string PreparationStarted = "START_VISIT_PREPARATION";

    /// <summary>BEFORE_VISIT → DURING_VISIT — the Host declares preparation complete and the visit begins.</summary>
    public const string CompleteBeforeVisit = "COMPLETE_BEFORE_VISIT";

    /// <summary>DURING_VISIT → AFTER_VISIT — the Host declares the visit itself complete.</summary>
    public const string CompleteDuringVisit = "COMPLETE_DURING_VISIT";

    /// <summary>AFTER_VISIT → CLOSED — the Host closes the instance's record.</summary>
    public const string CloseVisitInstance = "CLOSE_VISIT_INSTANCE";

    /// <summary>Every action the history reader recognizes as an immutable lifecycle transition.</summary>
    public static readonly IReadOnlyCollection<string> LifecycleActions =
        new[] { PreparationStarted, CompleteBeforeVisit, CompleteDuringVisit, CloseVisitInstance };
}
