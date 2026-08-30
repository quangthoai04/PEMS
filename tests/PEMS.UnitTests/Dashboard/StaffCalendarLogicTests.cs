using System;
using PEMS.Application.Dashboard.Common;
using PEMS.Application.Dashboard.Queries.GetStaffCalendar;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Dashboard;

/// <summary>
/// StaffCalendarLogic is the single classification engine shared by GetStaffCalendarQueryHandler
/// (list) and GetStaffCalendarDetailQueryHandler (detail) — both construct an InstanceSnapshot from
/// their own query and call these two methods directly with no logic of their own layered on top, so
/// asserting the pure function here covers both call sites at once.
/// </summary>
public class StaffCalendarLogicTests
{
    private static readonly DateTime Now = new(2026, 8, 30, 9, 0, 0);
    private static readonly DateTime FutureStart = Now.AddDays(1);
    private static readonly DateTime FutureEnd = Now.AddDays(1).AddHours(2);

    private static readonly StaffCalendarLogic.ViewerContext StaffLeader =
        new(UserId: 100, IsStaffLeader: true, PrimaryCampusId: 1);

    private static readonly StaffCalendarLogic.ViewerContext PlainStaff =
        new(UserId: 200, IsStaffLeader: false, PrimaryCampusId: 1);

    private static StaffCalendarLogic.InstanceSnapshot Snapshot(
        string requestStatus, string campusStatus, ulong? currentHostUserId = null)
        => new(
            requestStatus, campusStatus, VisitScopes.SingleCampus, CampusId: 1,
            currentHostUserId, FutureStart, FutureEnd);

    // ── F1 — the global contact-confirmation gate must be mirrored in BuildAllowedActions ──────

    [Fact]
    public void Case1_WaitingRequestApproval_with_the_gate_open_grants_the_campus_Staff_Leader_all_three_decisions()
    {
        var snapshot = Snapshot(VisitRequestStatuses.PendingApproval, VisitInstanceStatus.WaitingRequestApproval);

        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        Assert.True(actions.CanApprove);
        Assert.True(actions.CanReject);
        Assert.True(actions.CanAssignHost);
    }

    [Fact]
    public void Case2_WaitingRequestApproval_behind_the_gate_grants_no_decision_to_the_campus_Staff_Leader()
    {
        // A sibling campus in the same multi-campus request has not confirmed its operational contact
        // yet, so VisitRequestAggregateStatusService keeps the aggregate at PENDING_CONTACT_CONFIRMATION
        // even though THIS campus already reached WAITING_REQUEST_APPROVAL. Before the fix, this
        // combination let the calendar show enabled Approve/Reject that CampusApprovalExecutor would
        // then refuse with a 409 (see ContactGateVisibilityTests.Approve_and_reject_are_both_refused_
        // behind_the_gate_when_called_directly for the command-layer side of the same contract).
        var snapshot = Snapshot(VisitRequestStatuses.PendingContactConfirmation, VisitInstanceStatus.WaitingRequestApproval);

        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        Assert.False(actions.CanApprove);
        Assert.False(actions.CanReject);
        Assert.False(actions.CanAssignHost);
    }

    [Fact]
    public void Case3_A_campus_not_at_WaitingRequestApproval_has_no_decision_actions_regardless_of_the_gate()
    {
        var snapshot = Snapshot(VisitRequestStatuses.PendingContactConfirmation, VisitInstanceStatus.WaitingContactConfirmation);

        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        Assert.False(actions.CanApprove);
        Assert.False(actions.CanReject);
        Assert.False(actions.CanAssignHost);
    }

    [Fact]
    public void Plain_Staff_never_gets_decision_actions_even_when_the_gate_is_open()
    {
        // Regression guard: the gate check is additive to the existing IsStaffLeader/sameCampus check,
        // never a replacement for it.
        var snapshot = Snapshot(VisitRequestStatuses.PendingApproval, VisitInstanceStatus.WaitingRequestApproval);

        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, PlainStaff, Now);

        Assert.False(actions.CanApprove);
        Assert.False(actions.CanReject);
        Assert.False(actions.CanAssignHost);
    }

    // ── F2 — WAITING_CONTACT_CONFIRMATION must read as "waiting on the guest", not "needs you" ──

    [Fact]
    public void WaitingContactConfirmation_gets_its_own_label_instead_of_the_generic_fallback()
    {
        var snapshot = Snapshot(VisitRequestStatuses.PendingContactConfirmation, VisitInstanceStatus.WaitingContactConfirmation);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        var (label, _, _, _, _) = StaffCalendarLogic.ResolveStatus(snapshot, StaffLeader, actions, Now);

        Assert.NotEqual("Chờ xử lý", label);
        // Same wording VisitRowLabels.Resolve already uses for this exact status, so the two surfaces
        // that both describe "campus instance" state stay consistent with each other.
        Assert.Equal("Chờ xác nhận", label);
    }

    [Fact]
    public void WaitingContactConfirmation_never_paints_NEEDS_ACTION_and_grants_no_decision_actions()
    {
        var snapshot = Snapshot(VisitRequestStatuses.PendingContactConfirmation, VisitInstanceStatus.WaitingContactConfirmation);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        var (_, color, _, _, _) = StaffCalendarLogic.ResolveStatus(snapshot, StaffLeader, actions, Now);

        Assert.NotEqual(StaffCalendarColorTypes.NeedsAction, color);
        Assert.False(actions.CanApprove);
        Assert.False(actions.CanReject);
        Assert.False(actions.CanAssignHost);
    }

    [Fact]
    public void WaitingRequestApproval_with_the_gate_open_still_paints_NEEDS_ACTION_for_the_Staff_Leader()
    {
        // Regression guard: only WAITING_CONTACT_CONFIRMATION loses NEEDS_ACTION — a genuinely
        // actionable WAITING_REQUEST_APPROVAL item must keep it.
        var snapshot = Snapshot(VisitRequestStatuses.PendingApproval, VisitInstanceStatus.WaitingRequestApproval);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        var (_, color, _, _, _) = StaffCalendarLogic.ResolveStatus(snapshot, StaffLeader, actions, Now);

        Assert.Equal(StaffCalendarColorTypes.NeedsAction, color);
    }

    [Theory]
    [InlineData(VisitInstanceStatus.Assigned, "Đã gán host")]
    [InlineData(VisitInstanceStatus.BeforeVisit, "Chuẩn bị đón tiếp")]
    [InlineData(VisitInstanceStatus.DuringVisit, "Đang tiếp khách")]
    [InlineData(VisitInstanceStatus.AfterVisit, "Sau tiếp khách")]
    [InlineData(VisitInstanceStatus.Closed, "Đã hoàn tất")]
    public void Existing_campus_statuses_keep_their_original_label_after_the_fix(string campusStatus, string expectedLabel)
    {
        var snapshot = Snapshot(VisitRequestStatuses.Approved, campusStatus, currentHostUserId: 999);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        var (label, _, _, _, _) = StaffCalendarLogic.ResolveStatus(snapshot, StaffLeader, actions, Now);

        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [InlineData(VisitInstanceStatus.Assigned)]
    [InlineData(VisitInstanceStatus.BeforeVisit)]
    [InlineData(VisitInstanceStatus.DuringVisit)]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Closed)]
    public void Existing_campus_statuses_keep_their_original_color_after_the_fix(string campusStatus)
    {
        // Every one of these has a host assigned (999, not the viewer), so pre-fix they already
        // resolved to PROCESSED via the isDecided/!hasNoHost branch — the WAITING_CONTACT_CONFIRMATION
        // exclusion added to that branch's condition must not disturb them.
        var snapshot = Snapshot(VisitRequestStatuses.Approved, campusStatus, currentHostUserId: 999);
        var actions = StaffCalendarLogic.BuildAllowedActions(snapshot, StaffLeader, Now);

        var (_, color, _, _, _) = StaffCalendarLogic.ResolveStatus(snapshot, StaffLeader, actions, Now);

        Assert.Equal(StaffCalendarColorTypes.Processed, color);
    }
}
