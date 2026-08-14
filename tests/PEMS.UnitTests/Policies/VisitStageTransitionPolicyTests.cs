using PEMS.Domain.Policies;

namespace PEMS.UnitTests.Policies;

/// <summary>
/// NP-05 — T-6h is a RECOMMENDATION, and these tests exist to keep it one.
///
/// <para>
/// The state this replaces was a three-way disagreement: the command accepted an early confirm, the
/// capability flag refused it, and the screen printed "Có thể chuyển sang Trong tiếp khách từ 03:00"
/// over a button that worked anyway. Nobody had chosen that; it was what was left after the hard gate
/// was removed from the command and not from anywhere else.
/// </para>
/// <para>
/// So the naming carries the decision: <see cref="VisitStageTransitionPolicy.RecommendedStartAt"/>
/// and <see cref="VisitStageTransitionPolicy.IsWithinRecommendedWindow"/> answer "is this the usual
/// moment?" — never "is this allowed?". No caller may gate the transition on them, and there is
/// deliberately no error code left that says a start window is shut.
/// </para>
/// </summary>
public class VisitStageTransitionPolicyTests
{
    private static readonly DateTime PlannedStart = new(2026, 8, 22, 9, 0, 0);

    [Fact]
    public void The_recommended_moment_is_six_hours_before_the_planned_start()
    {
        Assert.Equal(6, VisitStageTransitionPolicy.StartVisitEarlyWindowHours);
        Assert.Equal(new DateTime(2026, 8, 22, 3, 0, 0),
            VisitStageTransitionPolicy.RecommendedStartAt(PlannedStart));
    }

    [Fact]
    public void The_boundary_itself_counts_as_inside_the_window()
    {
        Assert.True(VisitStageTransitionPolicy.IsWithinRecommendedWindow(
            new DateTime(2026, 8, 22, 3, 0, 0), PlannedStart));
    }

    [Fact]
    public void A_minute_before_the_boundary_is_outside_it()
    {
        Assert.False(VisitStageTransitionPolicy.IsWithinRecommendedWindow(
            new DateTime(2026, 8, 22, 2, 59, 0), PlannedStart));
    }

    [Fact]
    public void There_is_no_upper_bound()
    {
        // A visit that has already started — or finished — while the campus still reads BEFORE_VISIT
        // is a workflow that got stuck. Closing the window at the top would strand it there.
        Assert.True(VisitStageTransitionPolicy.IsWithinRecommendedWindow(
            PlannedStart.AddDays(3), PlannedStart));
    }

    [Fact]
    public void The_recommended_moment_moves_with_the_schedule()
    {
        // Derived, never stored: an approved amendment that moves the visit moves this with it. A
        // persisted cutoff would keep answering for a start time the visit no longer has.
        var moved = PlannedStart.AddDays(2);
        Assert.Equal(moved.AddHours(-6), VisitStageTransitionPolicy.RecommendedStartAt(moved));
    }

    [Fact]
    public void Being_outside_the_window_is_not_a_refusal_anywhere_in_the_domain()
    {
        // Guard against the hybrid coming back: if somebody reintroduces a "start window not open"
        // error code, this fails and they have to make the decision deliberately rather than by
        // adding a constant. The only refusals on BEFORE → DURING are readiness ones (agenda,
        // unanswered invitations, unsigned handovers), which live in the command.
        var codes = typeof(PEMS.Domain.Constants.VisitRequestErrorCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.DoesNotContain("VISIT_START_WINDOW_NOT_OPEN", codes);
    }
}
