using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.ApproveCampusInstance;
using PEMS.Application.Delegations.Commands.RejectCampusInstance;
using PEMS.Application.Delegations.Services;
using PEMS.Domain.Constants;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// A campus decision must say which revision it was taken on, and there must be no way to avoid
/// saying it.
///
/// <para>
/// The stale-review protection was already built: the review screen renders the campus's
/// <c>rowVersion</c>, the client echoes it back, and the handler locks the row and refuses a mismatch
/// with <c>VISIT_INSTANCE_VERSION_CONFLICT</c>. What was missing is that the field was OPTIONAL —
/// <c>int? ExpectedInstanceRowVersion = null</c>, with null read as "no expectation" and allowed
/// through. So the entire protection could be switched off by leaving one JSON field out: an older
/// client, a script, a caller that simply forgot, and the approval lands on whatever the row has since
/// become. The guest edits the delegation, the date or the purpose while the review screen is open,
/// and the Staff Leader's click approves content nobody read — with an audit trail showing a perfectly
/// ordinary approval, because from the row's point of view that is all that happened.
/// </para>
/// <para>
/// The fix is that "unstated" is no longer expressible. In-process callers cannot compile without a
/// value, and the one boundary where an omission still exists — a JSON body — converts it to a 400
/// instead of a default.
/// </para>
/// </summary>
public class CampusDecisionRowVersionContractTests
{
    public static IEnumerable<object[]> DecisionCommands() => new[]
    {
        new object[] { typeof(ApproveCampusInstanceCommand) },
        new object[] { typeof(RejectCampusInstanceCommand) },
    };

    // ── The bypass cannot be expressed ───────────────────────────────────────

    /// <summary>
    /// Approve and Reject alike: the parameter is a plain <c>int</c> with no default. Nullable would
    /// bring back the "unstated" case; a default would let a caller omit it and still compile.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecisionCommands))]
    public void The_expected_row_version_is_required_and_not_nullable(Type commandType)
    {
        var parameter = commandType
            .GetConstructors().Single()
            .GetParameters()
            .SingleOrDefault(p => p.Name == "ExpectedInstanceRowVersion");

        Assert.NotNull(parameter);
        Assert.Equal(typeof(int), parameter!.ParameterType);
        Assert.False(parameter.IsOptional,
            $"{commandType.Name}.ExpectedInstanceRowVersion is optional again — a decision that states " +
            "no revision would compile, and the stale-review guard would be one omitted argument away " +
            "from being off.");
    }

    /// <summary>
    /// Reject is held to the SAME contract as Approve, deliberately. Refusing a visit whose schedule or
    /// purpose has since been corrected is as wrong as approving one, and a bypass closed on only one
    /// of the two decisions just moves to the other.
    /// </summary>
    [Fact]
    public void Approve_and_reject_declare_the_same_version_contract()
    {
        static Type Kind(Type t) => t.GetConstructors().Single().GetParameters()
            .Single(p => p.Name == "ExpectedInstanceRowVersion").ParameterType;

        Assert.Equal(Kind(typeof(ApproveCampusInstanceCommand)), Kind(typeof(RejectCampusInstanceCommand)));
    }

    // ── The transport boundary fails closed ──────────────────────────────────

    /// <summary>
    /// An omitted field is refused rather than defaulted. 400 with a stable code, so the client can
    /// tell "you forgot to send the version" apart from "the version you sent is stale" (409) — the
    /// two need different reactions: fix the caller versus reload and re-read.
    /// </summary>
    [Fact]
    public void A_decision_that_states_no_version_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(
            () => VisitInstanceConcurrencyGuard.RequireExpectedRowVersion(null));

        Assert.Equal(VisitRequestErrorCodes.InstanceVersionRequired, ex.ErrorCode);
        Assert.NotEqual(VisitRequestErrorCodes.InstanceVersionConflict, ex.ErrorCode);
    }

    /// <summary>
    /// And a stated version passes through untouched — including ZERO, which is why the boundary is
    /// nullable in the first place. A campus starts at row version 0, so reading a missing field as 0
    /// would have let the most common "forgot to send it" case decide against a brand-new campus and
    /// look entirely valid doing it.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4321)]
    public void A_stated_version_is_passed_through_including_zero(int stated)
        => Assert.Equal(stated, VisitInstanceConcurrencyGuard.RequireExpectedRowVersion(stated));

    /// <summary>
    /// The guard that compares versions no longer has an "unstated" branch either. While it accepted
    /// null it documented itself as buying "no protection" for such callers, which is the same hole one
    /// layer down.
    /// </summary>
    [Fact]
    public void The_concurrency_guard_takes_a_non_nullable_expectation()
    {
        var parameter = typeof(VisitInstanceConcurrencyGuard)
            .GetMethod(nameof(VisitInstanceConcurrencyGuard.EnsureUnchangedAsync))!
            .GetParameters()
            .Single(p => p.Name == "expectedRowVersion");

        Assert.Equal(typeof(int), parameter.ParameterType);
    }
}
