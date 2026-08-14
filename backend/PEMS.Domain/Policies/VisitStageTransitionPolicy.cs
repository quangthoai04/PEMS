using System;

namespace PEMS.Domain.Policies;

/// <summary>
/// When a campus may move from preparation into the visit itself.
///
/// <para>
/// One place, because four things have to agree about it: the command that performs the transition,
/// the permission DTO the process screen renders from, the next-task builder that decides whether the
/// Host is being asked to act, and the frontend button. A rule enforced only in the command produces
/// the worst version of itself — the list tells the Host to confirm, the screen lets them click, and
/// the backend answers 409.
/// </para>
///
/// <para><b>The decided policy (NP-05): T-6h is a RECOMMENDATION, not a gate.</b></para>
/// <list type="bullet">
///   <item>A Host whose preparation is genuinely complete may start the visit whenever they like —
///   deliberately, because reality outruns the schedule: a delegation lands early, a campus sets up
///   the day before, a working session is brought forward.</item>
///   <item>The only hard conditions are the preparation ones the command already checks (agenda,
///   answered invitations, signed handovers). Those are about readiness, and readiness is real.</item>
///   <item>What T-6h buys is a sensible DEFAULT: nothing nudges the Host to flip the status before
///   then, so a campus does not sit in "đang tiếp khách" for a week. <see cref="RecommendedStartAt"/>
///   is offered to the UI as guidance, and <see cref="IsWithinRecommendedWindow"/> answers only
///   "is this the usual time?" — never "may they?".</item>
/// </list>
///
/// <para>
/// The half-and-half version this replaces was the actual defect: the command accepted an early
/// confirm, the capability flag refused it, and the screen printed "Có thể chuyển sang Trong tiếp
/// khách từ …" over a button that worked anyway. Every layer now reads the same rule, and the
/// consequence the Host actually needs to know — the preparation tab becomes read-only — is what the
/// confirmation says.
/// </para>
/// </summary>
public static class VisitStageTransitionPolicy
{
    /// <summary>
    /// How long before the planned start it becomes NORMAL to enter DURING_VISIT.
    ///
    /// <para>
    /// Six hours is a working morning's notice: enough to cover an early arrival, a same-day setup and
    /// a delegation that turns up ahead of schedule. It shapes what the UI suggests and what the task
    /// list nags about; it does not block anybody (see the type doc).
    /// </para>
    /// </summary>
    public const int StartVisitEarlyWindowHours = 6;

    /// <summary>
    /// The moment from which starting the visit is the expected thing to do — DERIVED from the
    /// campus's current planned start, never stored.
    ///
    /// <para>
    /// Deriving it is the point. A schedule that moves (an approved amendment, a leader's correction)
    /// moves this with it; a persisted cutoff would keep answering for a start time the visit no
    /// longer has.
    /// </para>
    /// </summary>
    public static DateTime RecommendedStartAt(DateTime plannedStartAt)
        => plannedStartAt.AddHours(-StartVisitEarlyWindowHours);

    /// <summary>
    /// True from T-6h onwards, INCLUDING the boundary itself and everything after it.
    ///
    /// <para>
    /// ADVISORY ONLY — read it as "is this the usual moment to start?", never as "is this allowed?".
    /// Callers use it to decide whether to nudge (an action-required task, a "sớm hơn dự kiến" note on
    /// the confirmation); no caller may use it to refuse the transition or to disable the button.
    /// </para>
    /// <para>
    /// <paramref name="vietnamNow"/> must be Vietnam wall-clock: <c>planned_start_at</c> is stored as
    /// wall clock throughout, so comparing it against UTC would shift the window by seven hours.
    /// </para>
    /// </summary>
    public static bool IsWithinRecommendedWindow(DateTime vietnamNow, DateTime plannedStartAt)
        => vietnamNow >= RecommendedStartAt(plannedStartAt);
}
