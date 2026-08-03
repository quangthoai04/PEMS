using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using Xunit;

// Deliberately NOT PEMS.UnitTests.Domain: that name shadows PEMS.Domain for every sibling test that
// writes `Domain.Entities.…` unqualified, and adding it broke files this change never touched.
namespace PEMS.UnitTests.Policies;

/// <summary>
/// The six-hour rule, exercised at its edges.
///
/// This policy is the single answer both the read model and every command handler rely on, so an
/// off-by-one here shows up twice: as a button that appears when it should not, and as a request the
/// handler accepts when it should not. The boundary cases below are the whole point of the file.
/// </summary>
public class VisitMutationPolicyTests
{
    private static readonly DateTime Start = new(2026, 8, 21, 14, 0, 0);

    private static VisitMutationDecision Evaluate(
        VisitMutationAction action,
        DateTime now,
        string instanceStatus = VisitInstanceStatuses.Assigned,
        string requestStatus = VisitRequestStatuses.Approved,
        string relation = VisitViewerRelations.Requester)
        => VisitMutationPolicy.Evaluate(new VisitMutationContext(
            action, requestStatus, instanceStatus, Start, now, relation));

    // ── Boundary (§10). "At least six hours before" INCLUDES the six-hour mark. ──

    [Fact]
    public void SixHoursAndOneSecondBeforeStart_IsAllowed()
    {
        var decision = Evaluate(VisitMutationAction.SubmitSafeEdit, Start.AddHours(-6).AddSeconds(-1));
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void ExactlySixHoursBeforeStart_IsAllowed()
    {
        // The rule is stated as a minimum lead time, so landing exactly on it satisfies it. Refusing
        // here would make the documented boundary unreachable in practice.
        var decision = Evaluate(VisitMutationAction.SubmitSafeEdit, Start.AddHours(-6));
        Assert.True(decision.Allowed);
        Assert.Equal(Start.AddHours(-6), decision.CutoffAt);
    }

    [Fact]
    public void FiveHoursFiftyNineFiftyNineBeforeStart_IsRefused()
    {
        var decision = Evaluate(VisitMutationAction.SubmitSafeEdit, Start.AddHours(-6).AddSeconds(1));
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.CutoffReached, decision.ErrorCode);
        Assert.Equal(Start.AddHours(-6), decision.CutoffAt);
        Assert.Equal(6, decision.RequiredLeadHours);
    }

    [Fact]
    public void AfterTheVisitHasStarted_IsRefused()
    {
        var decision = Evaluate(VisitMutationAction.SubmitSafeEdit, Start.AddMinutes(1));
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.CutoffReached, decision.ErrorCode);
    }

    [Fact]
    public void ARefusedDecisionStillCarriesItsDeadline()
    {
        // The UI has to show "hạn cuối 08:00 · bắt đầu 14:00" precisely when the answer is no.
        var decision = Evaluate(VisitMutationAction.SubmitAmendment, Start.AddHours(-1));
        Assert.False(decision.Allowed);
        Assert.Equal(Start.AddHours(-6), decision.CutoffAt);
    }

    // ── Lifecycle per action (§13 matrix) ──

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, true)]
    [InlineData(VisitInstanceStatuses.Assigned, false)]
    [InlineData(VisitInstanceStatuses.BeforeVisit, false)]
    [InlineData(VisitInstanceStatuses.Rejected, false)]
    [InlineData(VisitInstanceStatuses.DuringVisit, false)]
    public void EditPendingRequest_OnlyWhileWaiting(string instanceStatus, bool expected)
    {
        var requestStatus = instanceStatus == VisitInstanceStatuses.WaitingRequestApproval
            ? VisitRequestStatuses.PendingApproval
            : VisitRequestStatuses.Approved;
        var decision = Evaluate(
            VisitMutationAction.EditPendingRequest, Start.AddDays(-2), instanceStatus, requestStatus);
        Assert.Equal(expected, decision.Allowed);
    }

    [Fact]
    public void EditPendingRequest_RefusedWhenRequestIsNotPending_EvenIfCampusIsWaiting()
    {
        // A campus can still read WAITING while the request as a whole has moved on; the action needs
        // both to agree, or a partially-decided request would offer a full edit of everything.
        var decision = Evaluate(
            VisitMutationAction.EditPendingRequest, Start.AddDays(-2),
            VisitInstanceStatuses.WaitingRequestApproval, VisitRequestStatuses.PartiallyApproved);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.LifecycleNotAllowed, decision.ErrorCode);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Rejected, true)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, false)]
    [InlineData(VisitInstanceStatuses.Assigned, false)]
    public void ResubmitRejectedRequest_OnlyWhenFullyRejected(string instanceStatus, bool expected)
    {
        var requestStatus = instanceStatus == VisitInstanceStatuses.Rejected
            ? VisitRequestStatuses.Rejected
            : VisitRequestStatuses.PendingApproval;
        var decision = Evaluate(
            VisitMutationAction.ResubmitRejectedRequest, Start.AddDays(-2), instanceStatus, requestStatus);
        Assert.Equal(expected, decision.Allowed);
    }

    [Fact]
    public void Resubmit_IsNotMeasuredAgainstTheOldSchedule()
    {
        // A rejected request usually sits until well after its original date. Testing that date would
        // refuse every resubmit that matters — the point of resubmitting is to propose a NEW one, and
        // the lead time is enforced against that new date in the edit service.
        var longAfterTheOriginalDate = Start.AddDays(30);
        var decision = Evaluate(
            VisitMutationAction.ResubmitRejectedRequest, longAfterTheOriginalDate,
            VisitInstanceStatuses.Rejected, VisitRequestStatuses.Rejected);
        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Assigned, true)]
    [InlineData(VisitInstanceStatuses.BeforeVisit, true)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, false)]
    [InlineData(VisitInstanceStatuses.Rejected, false)]
    [InlineData(VisitInstanceStatuses.DuringVisit, false)]
    [InlineData(VisitInstanceStatuses.AfterVisit, false)]
    [InlineData(VisitInstanceStatuses.Closed, false)]
    [InlineData(VisitInstanceStatuses.Cancelled, false)]
    public void SafeEdit_OnlyOnADecidedNotYetStartedCampus(string instanceStatus, bool expected)
    {
        var decision = Evaluate(VisitMutationAction.SubmitSafeEdit, Start.AddDays(-2), instanceStatus);
        Assert.Equal(expected, decision.Allowed);
    }

    [Fact]
    public void SafeEdit_IsRefusedWhileTheCampusIsUnderWay_EvenWeeksAhead()
    {
        // The headline defect: "Sửa nhanh" appeared on a campus whose delegation was already on site.
        var decision = Evaluate(
            VisitMutationAction.SubmitSafeEdit, Start.AddDays(-30), VisitInstanceStatuses.DuringVisit);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.LifecycleNotAllowed, decision.ErrorCode);
        Assert.Contains("đang diễn ra", decision.DisabledReason);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Assigned, true)]
    [InlineData(VisitInstanceStatuses.BeforeVisit, true)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, false)]
    [InlineData(VisitInstanceStatuses.DuringVisit, false)]
    [InlineData(VisitInstanceStatuses.Closed, false)]
    public void Amendment_OnlyOnADecidedNotYetStartedCampus(string instanceStatus, bool expected)
    {
        var decision = Evaluate(VisitMutationAction.SubmitAmendment, Start.AddDays(-2), instanceStatus);
        Assert.Equal(expected, decision.Allowed);
    }

    // ── Relation (§12: a capability never replaces authorization) ──

    [Fact]
    public void TransferHost_RequiresTheCampusLeader()
    {
        var asRequester = Evaluate(
            VisitMutationAction.TransferHost, Start.AddDays(-2), relation: VisitViewerRelations.Requester);
        Assert.False(asRequester.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, asRequester.ErrorCode);

        var asLeader = Evaluate(
            VisitMutationAction.TransferHost, Start.AddDays(-2), relation: VisitViewerRelations.CampusLeader);
        Assert.True(asLeader.Allowed);
    }

    /// <summary>
    /// Deciding a proposal is campus governance: the Staff Leader who owns the campus approves or
    /// rejects it. Both directions are asserted because the interesting exclusion is the HOST — the
    /// proposals that matter most are the ones that alter the Host's own visit, so a Host who could
    /// approve them would be reviewing themself. The requester is excluded for the obvious reason.
    /// </summary>
    [Fact]
    public void ApproveAmendment_RequiresTheCampusLeader()
    {
        var asLeader = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.CampusLeader);
        Assert.True(asLeader.Allowed);

        var asRequester = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.Requester);
        Assert.False(asRequester.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, asRequester.ErrorCode);

        var asHost = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.Host);
        Assert.False(asHost.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, asHost.ErrorCode);
    }

    [Fact]
    public void ApproveAmendment_IsAlsoSubjectToTheDeadline()
    {
        // A proposal still pending hours before the visit must not be written in behind everyone's
        // back — by then the campus has printed its list and briefed its Host.
        var decision = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddHours(-2),
            relation: VisitViewerRelations.CampusLeader);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.CutoffReached, decision.ErrorCode);
    }

    [Theory]
    [InlineData(VisitMutationAction.SubmitSafeEdit)]
    [InlineData(VisitMutationAction.SubmitAmendment)]
    [InlineData(VisitMutationAction.EditPendingRequest)]
    public void RequesterOnlyActions_AreRefusedForACampusLeader(VisitMutationAction action)
    {
        var decision = Evaluate(action, Start.AddDays(-2), relation: VisitViewerRelations.CampusLeader);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, decision.ErrorCode);
    }

    [Fact]
    public void ACancelledRequest_HasNoActionsAtAll()
    {
        foreach (var action in Enum.GetValues<VisitMutationAction>())
        {
            var relation = action is VisitMutationAction.ApproveAmendment or VisitMutationAction.TransferHost
                ? VisitViewerRelations.CampusLeader
                : VisitViewerRelations.Requester;
            var decision = Evaluate(
                action, Start.AddDays(-2), VisitInstanceStatuses.Assigned,
                VisitRequestStatuses.Cancelled, relation);
            Assert.False(decision.Allowed);
        }
    }

    // ── Multi-campus helpers used by the request-level scope rule (§10) ──

    [Theory]
    [InlineData(VisitInstanceStatuses.DuringVisit, true)]
    [InlineData(VisitInstanceStatuses.AfterVisit, true)]
    [InlineData(VisitInstanceStatuses.Closed, true)]
    [InlineData(VisitInstanceStatuses.Assigned, false)]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, false)]
    [InlineData(VisitInstanceStatuses.Cancelled, false)]
    public void BlocksRequestLevel_IdentifiesThePointOfNoReturn(string status, bool expected)
        => Assert.Equal(expected, VisitMutationPolicy.BlocksRequestLevel(status));

    [Theory]
    [InlineData(VisitInstanceStatuses.WaitingRequestApproval, true)]
    [InlineData(VisitInstanceStatuses.Assigned, true)]
    [InlineData(VisitInstanceStatuses.BeforeVisit, true)]
    [InlineData(VisitInstanceStatuses.Cancelled, false)]
    [InlineData(VisitInstanceStatuses.Rejected, false)]
    public void RequestLevelScope_CoversTheStillLiveCampuses(string status, bool expected)
        => Assert.Equal(expected, VisitMutationPolicy.RequestLevelScope(status));
}
