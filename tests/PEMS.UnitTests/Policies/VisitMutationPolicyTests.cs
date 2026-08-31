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
    /// Deciding a proposal belongs to the campus's CURRENT Host — the person running the visit, who
    /// holds the room and the schedule and is already talking to the guest. All three directions are
    /// asserted because the interesting exclusion is now the campus STAFF LEADER: they approved the
    /// campus and named its Host, and from that point the day-to-day content is the Host's to decide.
    /// </summary>
    [Fact]
    public void ApproveAmendment_RequiresTheCurrentHost()
    {
        var asHost = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.Host);
        Assert.True(asHost.Allowed);

        var asRequester = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.Requester);
        Assert.False(asRequester.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, asRequester.ErrorCode);

        var asLeader = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddDays(-2), relation: VisitViewerRelations.CampusLeader);
        Assert.False(asLeader.Allowed);
        Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, asLeader.ErrorCode);
    }

    [Fact]
    public void ApproveAmendment_IsAlsoSubjectToTheDeadline()
    {
        // A proposal still pending hours before the visit must not be written in behind everyone's
        // back — by then the campus has printed its list and briefed its team.
        var decision = Evaluate(
            VisitMutationAction.ApproveAmendment, Start.AddHours(-2),
            relation: VisitViewerRelations.Host);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.CutoffReached, decision.ErrorCode);
    }

    // ── Per-campus pending edit (§17): the door a MIXED request needs ──

    /// <summary>
    /// The case the whole action exists for. With HN already ASSIGNED the request aggregate reads
    /// PARTIALLY_APPROVED, and the whole-request edit is refused on HN — but HCM has not been answered
    /// by anyone, and it must stay correctable. The policy therefore asks about the CAMPUS, never about
    /// the aggregate.
    /// </summary>
    [Theory]
    [InlineData(VisitRequestStatuses.PendingApproval)]
    [InlineData(VisitRequestStatuses.PartiallyApproved)]
    public void EditPendingCampus_IsOpenOnAWaitingCampus_WhateverTheRequestAggregateSays(string requestStatus)
    {
        var decision = Evaluate(
            VisitMutationAction.EditPendingCampus, Start.AddDays(-2),
            instanceStatus: VisitInstanceStatuses.WaitingRequestApproval,
            requestStatus: requestStatus);
        Assert.True(decision.Allowed);
    }

    [Theory]
    [InlineData(VisitInstanceStatuses.Assigned)]
    [InlineData(VisitInstanceStatuses.BeforeVisit)]
    [InlineData(VisitInstanceStatuses.Rejected)]
    [InlineData(VisitInstanceStatuses.DuringVisit)]
    [InlineData(VisitInstanceStatuses.Closed)]
    public void EditPendingCampus_IsRefusedOnceTheCampusHasMovedOn(string instanceStatus)
    {
        // Each of those states has its own workflow — safe edit / amendment, resubmit, or nothing at
        // all. Accepting them here would be a second, quieter way to rewrite a decided campus.
        var decision = Evaluate(
            VisitMutationAction.EditPendingCampus, Start.AddDays(-2),
            instanceStatus: instanceStatus, requestStatus: VisitRequestStatuses.PartiallyApproved);
        Assert.False(decision.Allowed);
        Assert.Equal(VisitMutationErrorCodes.LifecycleNotAllowed, decision.ErrorCode);
    }

    /// <summary>
    /// Both sides of the campus reach it: the requester side because it is their request, and the
    /// campus's Staff Leader because fixing a schedule before approving is how a request gets accepted
    /// rather than refused. Nobody else — a HO reader, a sibling campus's leader — gets in.
    /// </summary>
    [Theory]
    [InlineData(VisitViewerRelations.Requester, true)]
    [InlineData(VisitViewerRelations.CampusLeader, true)]
    [InlineData(VisitViewerRelations.Host, false)]
    [InlineData(VisitViewerRelations.Other, false)]
    public void EditPendingCampus_AdmitsTheRequesterSideAndTheCampusLeader(string relation, bool allowed)
    {
        var decision = Evaluate(
            VisitMutationAction.EditPendingCampus, Start.AddDays(-2),
            instanceStatus: VisitInstanceStatuses.WaitingRequestApproval,
            requestStatus: VisitRequestStatuses.PendingApproval,
            relation: relation);
        Assert.Equal(allowed, decision.Allowed);
        if (!allowed)
            Assert.Equal(VisitMutationErrorCodes.RelationNotAllowed, decision.ErrorCode);
    }

    // ── The 72-hour registration floor, and who may pass it (§29–§31) ──

    [Fact]
    public void AScheduleAtOrBeyondSeventyTwoHours_IsAllowedForEveryone()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0);
        var exactly = VisitMutationPolicy.EvaluateScheduleLeadTime(
            now.AddHours(72), now, actorMayOverride: false, overrideConfirmed: false);
        Assert.True(exactly.Allowed);
        Assert.False(exactly.ConfirmationRequired);
    }

    [Fact]
    public void AScheduleInsideTheFloor_IsRefusedForTheRequesterSide()
    {
        // No confirmation is offered, because there is nothing they could confirm: the answer is no,
        // and a "continue anyway" would promise something the backend will not honour.
        var now = new DateTime(2026, 8, 1, 9, 0, 0);
        var decision = VisitMutationPolicy.EvaluateScheduleLeadTime(
            now.AddHours(71), now, actorMayOverride: false, overrideConfirmed: false);
        Assert.False(decision.Allowed);
        Assert.False(decision.ConfirmationRequired);
        Assert.Equal(now.AddHours(72), decision.EarliestAllowedStart);
    }

    [Fact]
    public void AScheduleInsideTheFloor_AsksTheCampusLeaderToConfirm_ThenAllowsIt()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0);

        var unconfirmed = VisitMutationPolicy.EvaluateScheduleLeadTime(
            now.AddHours(71), now, actorMayOverride: true, overrideConfirmed: false);
        Assert.False(unconfirmed.Allowed);
        Assert.True(unconfirmed.ConfirmationRequired);

        var confirmed = VisitMutationPolicy.EvaluateScheduleLeadTime(
            now.AddHours(71), now, actorMayOverride: true, overrideConfirmed: true);
        Assert.True(confirmed.Allowed);
        Assert.False(confirmed.ConfirmationRequired);
    }

    /// <summary>
    /// The flag alone grants nothing. Whether the actor MAY override is decided by the handler from
    /// their relation to the campus; a caller who simply sets the flag is refused exactly as if they
    /// had not, and is not even offered the confirmation.
    /// </summary>
    [Fact]
    public void TheOverrideFlagAloneDoesNothingForSomebodyWhoMayNotOverride()
    {
        var now = new DateTime(2026, 8, 1, 9, 0, 0);
        var decision = VisitMutationPolicy.EvaluateScheduleLeadTime(
            now.AddHours(1), now, actorMayOverride: false, overrideConfirmed: true);
        Assert.False(decision.Allowed);
        Assert.False(decision.ConfirmationRequired);
    }

    // ── IsShortNoticeEligible (PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS) — the capability behind
    //    allowShortNotice on every create/edit/resubmit path: internal actor AND registrant of THIS
    //    request, both required, neither sufficient alone. ──

    [Theory]
    [InlineData(true, true, true)]    // internal Staff/Staff Leader, registrant of this request → eligible
    [InlineData(true, false, false)]  // internal, but NOT the registrant (e.g. only the operational contact) → not eligible
    [InlineData(false, true, false)]  // registrant, but not internal (Visitor/Guest) → not eligible
    [InlineData(false, false, false)] // neither → not eligible
    public void IsShortNoticeEligible_RequiresBothInternalAndRegistrant(
        bool isInternalActor, bool isRegistrant, bool expected)
    {
        Assert.Equal(expected, VisitMutationPolicy.IsShortNoticeEligible(isInternalActor, isRegistrant));
    }

    /// <summary>
    /// The two numbers answer different questions and must not be collapsed into one: 6 hours is how
    /// late an action may be taken on an existing schedule, 72 is how soon a schedule may be SET.
    /// </summary>
    [Fact]
    public void TheCutoffAndTheRegistrationFloor_AreDifferentNumbers()
    {
        Assert.Equal(6, VisitMutationPolicy.RequiredLeadHours);
        Assert.Equal(VisitMutationPolicy.RequiredLeadHours, VisitMutationPolicy.MutationCutoffHours);
        Assert.Equal(72, VisitMutationPolicy.MinScheduleLeadHours);
        Assert.NotEqual(VisitMutationPolicy.MutationCutoffHours, VisitMutationPolicy.MinScheduleLeadHours);
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
            var relation = action switch
            {
                VisitMutationAction.ApproveAmendment => VisitViewerRelations.Host,
                VisitMutationAction.TransferHost => VisitViewerRelations.CampusLeader,
                _ => VisitViewerRelations.Requester,
            };
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
    [InlineData(VisitInstanceStatuses.BeforeVisit, false)]
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
