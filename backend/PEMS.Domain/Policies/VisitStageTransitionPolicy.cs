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
/// </summary>
public static class VisitStageTransitionPolicy
{
    /// <summary>
    /// How long before the planned start a campus may enter DURING_VISIT.
    ///
    /// <para>
    /// The transition used to have no time rule at all: a Host who finished the agenda, the
    /// invitations and the handover paperwork a week early could put the campus into "đang tiếp
    /// khách" a week early with it. The status is what every other screen reads to decide whether the
    /// visit is happening, so an early flip makes the whole system describe a visit that is not
    /// taking place.
    /// </para>
    /// <para>
    /// Six hours is a working morning's notice: enough to cover an early arrival, a same-day setup
    /// and a delegation that turns up ahead of schedule, without letting the status run days ahead of
    /// reality.
    /// </para>
    /// </summary>
    public const int StartVisitEarlyWindowHours = 6;

    /// <summary>
    /// The earliest moment the Host may start the visit — DERIVED from the campus's current planned
    /// start, never stored.
    ///
    /// <para>
    /// Deriving it is the point. A schedule that moves (an approved amendment, a leader's correction)
    /// moves this with it; a persisted cutoff would keep answering for a start time the visit no
    /// longer has.
    /// </para>
    /// </summary>
    public static DateTime StartVisitAvailableAt(DateTime plannedStartAt)
        => plannedStartAt.AddHours(-StartVisitEarlyWindowHours);

    /// <summary>
    /// True from T-6h onwards, INCLUDING the boundary itself and everything after it.
    ///
    /// <para>
    /// There is deliberately no upper bound. A visit that has already started — or finished — while
    /// the campus still reads BEFORE_VISIT is a workflow that got stuck, and refusing the transition
    /// then would strand it there with no way forward. The rule exists to stop the status running
    /// AHEAD of reality, not to stop it catching up.
    /// </para>
    /// <para>
    /// <paramref name="vietnamNow"/> must be Vietnam wall-clock: <c>planned_start_at</c> is stored as
    /// wall clock throughout, so comparing it against UTC would shift the window by seven hours and
    /// open it at T+1h.
    /// </para>
    /// </summary>
    public static bool CanAdvanceBeforeToDuring(DateTime vietnamNow, DateTime plannedStartAt)
        => vietnamNow >= StartVisitAvailableAt(plannedStartAt);
}
