using System;
using System.Collections.Generic;
using PEMS.Domain.Constants;

namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Shared audit vocabulary for the two per-campus decision commands (approve+assign-host and reject).
/// Both write their audit row under the deciding instance's own campus/request/instance context so the
/// campus-scoped audit surfaces can find them; this constant keeps the two in step.
///
/// <para>
/// Also the single whitelist the business-history reader uses to recognize an immutable decision event
/// (VISIT_HISTORY_INTEGRITY_MASTER_IMPLEMENTATION_PLAN, Fix Group B): writer and reader share these
/// exact action strings so the two cannot drift apart.
/// </para>
/// </summary>
public static class CampusDecisionAudit
{
    /// <summary>audit_logs.source_type for a Staff Leader decision on one campus instance.</summary>
    public const string SourceType = "CAMPUS_DECISION";

    /// <summary>An approve+assign-host decision with no calendar conflict on the assigned host.</summary>
    public const string Approved = "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST";

    /// <summary>
    /// Same decision, flagged because the assigned host has a calendar overlap elsewhere. Same
    /// business meaning as <see cref="Approved"/> — the schedule warning is metadata about the
    /// assignment, not a different outcome, so the history reader must treat both as ONE event
    /// (INSTANCE_APPROVED) and never invent a second semantics for it.
    /// </summary>
    public const string ApprovedWithScheduleWarning =
        "APPROVE_CAMPUS_INSTANCE_AND_ASSIGN_HOST_WITH_SCHEDULE_WARNING";

    /// <summary>
    /// A rejection. Same value as <c>CampusRejectionEmail.RejectionAuditAction</c> (kept there as the
    /// canonical constant the reject handler and the email-recovery sweep already depend on) — named
    /// here too so the history reader's decision whitelist reads as decision vocabulary rather than
    /// reaching into the notification layer for what is, from here, just an action string.
    /// </summary>
    public const string Rejected = "REJECT_CAMPUS_INSTANCE";

    /// <summary>
    /// A preauthorized host proposal (Leader-selected or self-nominated) auto-applying the instant the
    /// request's contact-confirmation gate opens — ProposedHostActivationService.ActivateAsync. It
    /// carries the SAME decision fields an approval does (decided_by/decided_at/decision_actor_role
    /// all set, campus lands on ASSIGNED with a host — see DecisionSources.PreauthorizedHostActivation
    /// on the campus row itself), which is what makes it decision history at all rather than a
    /// technical gate. It is deliberately its OWN action, never folded into <see cref="Approved"/>:
    /// the actual reviewing act happened earlier, when whoever had the authority authorized the
    /// proposal — this is only where that authorization was filed once it could take effect. Filing it
    /// as INSTANCE_APPROVED would misstate WHO reviewed the campus and WHEN.
    /// </summary>
    public const string HostProposalActivated = "PROPOSED_HOST_ACTIVATED";

    /// <summary>Both approval actions — the reader must never distinguish them by semantics.</summary>
    public static readonly IReadOnlyCollection<string> ApprovalActions =
        new[] { Approved, ApprovedWithScheduleWarning };

    /// <summary>
    /// Every action the history reader recognizes as an immutable campus decision. Includes
    /// <see cref="HostProposalActivated"/> — a genuine decision, just not an approval — but NOT
    /// PROPOSED_HOST_NEEDS_RESELECTION, which sets no decided_by/decided_at on the campus row at all
    /// (ProposedHostActivationService.MarkNeedsReselection) and so records no decision to surface.
    /// </summary>
    public static readonly IReadOnlyCollection<string> DecisionActions =
        new[] { Approved, ApprovedWithScheduleWarning, Rejected, HostProposalActivated };

    public static bool IsApproval(string? action) => action == Approved || action == ApprovedWithScheduleWarning;

    /// <summary>
    /// True when the CURRENT campus row is still, verifiably, describing the EXACT SAME decision this
    /// audit recorded — never merely "a decision of the same kind happened at some point". This exists
    /// for exactly one purpose: letting a legacy audit (written before this capture existed, so its own
    /// <c>AuditLogChange</c> rows carry no <c>decision_note</c>) borrow that one field from the current
    /// row instead of reporting it as unknown forever.
    ///
    /// <para>
    /// <b>Why exact equality on <paramref name="currentDecidedAt"/> is safe, not fragile.</b> Both
    /// <see cref="CampusApprovalExecutor"/> and <c>RejectCampusInstanceCommandHandler</c> assign the
    /// SAME in-memory <c>now</c>/<c>actorId</c> to both the campus row's <c>decided_at</c>/
    /// <c>decided_by</c> AND this audit's <c>created_at</c>/<c>actor_user_id</c>, inside one method
    /// call, before one <c>SaveChangesAsync</c>. Both columns are plain MySQL <c>DATETIME</c> in the
    /// canonical schema (whole-second precision, no fractional part — see
    /// <c>docs/database/scripts/phase_1_candidate/00_fresh_target.sql</c>: <c>decided_at DATETIME
    /// NULL</c>, <c>created_at DATETIME NOT NULL</c>), so round-tripping the identical in-memory value
    /// through either column truncates it the same way. A GENUINE match therefore compares equal
    /// exactly, every time — and a fuzzy tolerance window is not a safety margin here, it is exactly
    /// how a DIFFERENT decision on the same instance, made moments apart (reject #1 vs reject #2 after
    /// a resubmit — see plan Case C2-L3), would get the wrong reason attached to the wrong audit row.
    /// No tolerance is used on purpose.
    /// </para>
    /// <para>
    /// Matching on timestamp + actor alone is not enough either: two decisions of the SAME kind by the
    /// SAME actor on the SAME instance (two rejections, both by the campus's one Staff Leader) would
    /// both pass that pair of checks against whichever is CURRENT. The status-class check below is
    /// what keeps a rejection audit from ever being enriched by a current row that has moved on to
    /// ASSIGNED, and vice versa — combined with the exact timestamp match, only the one audit whose
    /// timestamp the current row's <c>decided_at</c> actually equals can win.
    /// </para>
    /// </summary>
    public static bool CurrentRowMatchesAuditedDecision(
        string action, DateTime auditCreatedAt, ulong? auditActorUserId,
        string? currentStatus, DateTime? currentDecidedAt, ulong? currentDecidedBy)
    {
        if (currentDecidedAt != auditCreatedAt || currentDecidedBy != auditActorUserId)
            return false;

        return IsApproval(action)
            // Approval survives lifecycle progression (ASSIGNED → BEFORE/DURING/AFTER_VISIT → CLOSED)
            // without decided_at/decided_by ever being cleared — same status set the legacy standalone
            // fallback already treats as "this campus was approved" (plan Case B5).
            ? currentStatus is VisitInstanceStatuses.Assigned or VisitInstanceStatuses.BeforeVisit
                or VisitInstanceStatuses.DuringVisit or VisitInstanceStatuses.AfterVisit
                or VisitInstanceStatuses.Closed
            : currentStatus == VisitInstanceStatuses.Rejected;
    }

    /// <summary>
    /// The three facts <see cref="CurrentRowMatchesAuditedDecision"/> compares a candidate audit
    /// against. Exists so a reader can hold "every decision audit on this instance" as a small,
    /// comparable set without carrying the audit row's id, reason text, or change list along —
    /// uniqueness is decided purely on Action/CreatedAt/ActorUserId, nothing else.
    /// </summary>
    public readonly record struct DecisionAuditFacts(string Action, DateTime CreatedAt, ulong? ActorUserId);

    /// <summary>
    /// <see cref="CurrentRowMatchesAuditedDecision"/> answers "could this audit be the one the current
    /// row still reflects?" — but MySQL <c>DATETIME</c> is whole-second precision (see the type's own
    /// remarks), so two DIFFERENT decisions on the same instance, by the same actor, landing in the
    /// same second (reject → resubmit → reject again, all within one second) can BOTH answer that
    /// question "yes". Borrowing the current row's note for either one, in that case, is a coin flip
    /// dressed up as recovery — exactly the "wrong history" this exists to refuse in favor of "unknown".
    ///
    /// <para>
    /// Enrichment is therefore only safe when <paramref name="candidate"/> is the UNIQUE audit, among
    /// every decision audit this instance has ever recorded (<paramref name="instanceDecisionAudits"/>
    /// — canonical ones included, since a canonical audit that also matches the current row's tuple is
    /// itself proof the current row's identity is ambiguous, even though that canonical audit needs no
    /// borrowing itself), that the current row's tuple resolves to. No timestamp tolerance, no
    /// "newest wins", no "highest id wins" — a tie is reported as a tie.
    /// </para>
    /// </summary>
    public static bool CanEnrichFromCurrentRow(
        DecisionAuditFacts candidate,
        IReadOnlyCollection<DecisionAuditFacts> instanceDecisionAudits,
        string? currentStatus, DateTime? currentDecidedAt, ulong? currentDecidedBy)
    {
        var matchCount = 0;
        var candidateIsAMatch = false;
        foreach (var audit in instanceDecisionAudits)
        {
            if (!CurrentRowMatchesAuditedDecision(
                    audit.Action, audit.CreatedAt, audit.ActorUserId,
                    currentStatus, currentDecidedAt, currentDecidedBy))
                continue;
            matchCount++;
            if (audit == candidate) candidateIsAMatch = true;
        }
        return candidateIsAMatch && matchCount == 1;
    }
}
