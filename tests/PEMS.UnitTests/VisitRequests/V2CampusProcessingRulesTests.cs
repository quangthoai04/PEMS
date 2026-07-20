using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Domain.Constants;
using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// The role × mode × campus matrix for per-campus processing on the AUTHENTICATED v2 create.
/// These are the rules that decide who may already be the host of a campus at submit time, so every
/// negative case here is an authorization boundary: a Staff self-hosting a campus they do not belong
/// to, a Staff acting as a Leader, a Visitor forging a decision. The DB-dependent half (is this host
/// candidate an ACTIVE same-campus IC Staff) lives in the handler and is covered by IntegrationTests.
/// </summary>
public class V2CampusProcessingRulesTests
{
    private const ulong ActorId = 100;
    private const ulong OtherStaffId = 200;

    private static V2ProcessingActor Visitor() =>
        new(IsVisitor: true, IsRegularStaff: false, IsStaffLeader: false,
            OwnCampusCode: null, OwnDepartmentIsIc: false, ActorUserId: ActorId);

    private static V2ProcessingActor Staff(string campus = "HN", bool ic = true) =>
        new(IsVisitor: false, IsRegularStaff: true, IsStaffLeader: false,
            OwnCampusCode: campus, OwnDepartmentIsIc: ic, ActorUserId: ActorId);

    private static V2ProcessingActor Leader(string campus = "HN") =>
        new(IsVisitor: false, IsRegularStaff: false, IsStaffLeader: true,
            OwnCampusCode: campus, OwnDepartmentIsIc: true, ActorUserId: ActorId);

    private static CampusVisitFormDto CampusVisit(string campusCode, CampusProcessingV2Dto? processing)
    {
        var start = new DateTime(2026, 9, 1, 9, 0, 0);
        return new CampusVisitFormDto(
            campusCode, start, start.AddHours(2),
            "Đoàn ABC", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null,
            processing);
    }

    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses) =>
        new("SUB-1",
            new RegistrantInputV2("Reg", "VN", "Org", "Job", "+8491", "reg@example.com"),
            new ContactPointDto("Contact", "Org", "+8492", "contact@example.com"),
            null,
            campuses.ToList());

    // ── BuildPlans: only DIRECT intents become plans ──────────────────────────────

    [Fact]
    public void BuildPlans_ignores_campuses_with_no_processing_intent()
    {
        var plans = V2CampusProcessingRules.BuildPlans(Form(CampusVisit("HN", null)));
        Assert.Empty(plans);
    }

    [Fact]
    public void BuildPlans_treats_send_for_review_as_default_routing_not_a_plan()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SendForReview, null))));
        Assert.Empty(plans);
    }

    [Fact]
    public void BuildPlans_keeps_each_campus_intent_separate()
    {
        var plans = V2CampusProcessingRules.BuildPlans(Form(
            CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null)),
            CampusVisit("HCM", new CampusProcessingV2Dto(CampusSubmissionModes.SendForReview, null))));

        var plan = Assert.Single(plans);
        Assert.Equal("HN", plan.CampusCode);
        Assert.Equal(CampusSubmissionModes.SelfHost, plan.Mode);
    }

    // ── Visitor: never any internal processing ────────────────────────────────────

    [Fact]
    public void Visitor_cannot_self_host()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2CampusProcessingRules.ValidateShape(Visitor(), plans));
        Assert.Equal(VisitRequestErrorCodes.InvalidCampusSubmissionMode, ex.ErrorCode);
    }

    [Fact]
    public void Visitor_cannot_smuggle_a_host_through_send_for_review()
    {
        // SEND_FOR_REVIEW carrying a hostUserId is NOT default routing — it must reach validation.
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SendForReview, OtherStaffId))));

        Assert.Single(plans);
        Assert.Throws<BusinessRuleException>(() => V2CampusProcessingRules.ValidateShape(Visitor(), plans));
    }

    // ── Regular Staff ─────────────────────────────────────────────────────────────

    [Fact]
    public void Staff_may_self_host_their_own_campus()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        V2CampusProcessingRules.ValidateShape(Staff("HN"), plans); // does not throw

        var decision = V2CampusProcessingRules.Derive(Staff("HN"), plans[0]);
        Assert.Equal(ActorId, decision.HostUserId);
        Assert.Equal(DecisionActorRole.Staff, decision.DecisionActorRole);
        Assert.Equal(DecisionSources.InternalSelfHost, decision.DecisionSource);
        Assert.False(decision.IsLeaderAssignOther);
    }

    [Fact]
    public void Staff_cannot_self_host_a_campus_outside_their_scope()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HCM", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        Assert.Throws<ForbiddenException>(() => V2CampusProcessingRules.ValidateShape(Staff("HN"), plans));
    }

    [Fact]
    public void Staff_outside_the_ic_department_cannot_self_host()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2CampusProcessingRules.ValidateShape(Staff("HN", ic: false), plans));
        Assert.Equal(VisitRequestErrorCodes.SelfHostNotEligible, ex.ErrorCode);
    }

    [Fact]
    public void Staff_cannot_assign_a_host_to_someone_else()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, OtherStaffId))));

        Assert.Throws<ForbiddenException>(() => V2CampusProcessingRules.ValidateShape(Staff("HN"), plans));
    }

    [Fact]
    public void Self_host_naming_another_user_as_host_is_rejected()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, OtherStaffId))));

        Assert.Throws<ForbiddenException>(() => V2CampusProcessingRules.ValidateShape(Staff("HN"), plans));
    }

    // ── Staff Leader ──────────────────────────────────────────────────────────────

    [Fact]
    public void Leader_may_self_host_their_own_campus()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        V2CampusProcessingRules.ValidateShape(Leader("HN"), plans);

        var decision = V2CampusProcessingRules.Derive(Leader("HN"), plans[0]);
        Assert.Equal(ActorId, decision.HostUserId);
        Assert.Equal(DecisionActorRole.StaffLeader, decision.DecisionActorRole);
        Assert.Equal(DecisionSources.InternalSelfHost, decision.DecisionSource);
    }

    [Fact]
    public void Leader_may_assign_another_staff_on_their_own_campus()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, OtherStaffId))));

        V2CampusProcessingRules.ValidateShape(Leader("HN"), plans);

        var decision = V2CampusProcessingRules.Derive(Leader("HN"), plans[0]);
        Assert.Equal(OtherStaffId, decision.HostUserId);
        Assert.Equal(DecisionActorRole.StaffLeader, decision.DecisionActorRole);
        Assert.Equal(DecisionSources.InternalLeaderAssign, decision.DecisionSource);
        Assert.True(decision.IsLeaderAssignOther);
    }

    [Fact]
    public void Leader_assigning_themself_collapses_to_self_host()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, ActorId))));

        V2CampusProcessingRules.ValidateShape(Leader("HN"), plans);

        var decision = V2CampusProcessingRules.Derive(Leader("HN"), plans[0]);
        Assert.Equal(ActorId, decision.HostUserId);
        Assert.Equal(DecisionSources.InternalSelfHost, decision.DecisionSource);
        Assert.False(decision.IsLeaderAssignOther);
    }

    [Fact]
    public void Leader_assign_without_a_host_is_rejected()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, null))));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2CampusProcessingRules.ValidateShape(Leader("HN"), plans));
        Assert.Equal(VisitRequestErrorCodes.InvalidHostCandidate, ex.ErrorCode);
    }

    [Fact]
    public void Leader_cannot_decide_a_sibling_campus()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HCM", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, OtherStaffId))));

        Assert.Throws<ForbiddenException>(() => V2CampusProcessingRules.ValidateShape(Leader("HN"), plans));
    }

    // ── Multi-campus: decisions stay per campus ───────────────────────────────────

    [Fact]
    public void Own_campus_decision_never_extends_to_the_sibling_campus()
    {
        var plans = V2CampusProcessingRules.BuildPlans(Form(
            CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null)),
            CampusVisit("HCM", null)));

        V2CampusProcessingRules.ValidateShape(Staff("HN"), plans);

        // Only HN carries a decision; HCM has no plan at all and stays pending/routed.
        Assert.Equal(new[] { "HN" }, plans.Select(p => p.CampusCode));
    }

    [Fact]
    public void A_single_out_of_scope_campus_rejects_the_whole_submission()
    {
        // Fail-closed: the transaction must not half-apply the in-scope campus.
        var plans = V2CampusProcessingRules.BuildPlans(Form(
            CampusVisit("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null)),
            CampusVisit("HCM", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null))));

        Assert.Throws<ForbiddenException>(() => V2CampusProcessingRules.ValidateShape(Staff("HN"), plans));
    }

    [Fact]
    public void Processing_for_an_unselected_campus_is_rejected()
    {
        // A campus visit whose own processing block names a different campus cannot occur through the
        // UI, but a hand-crafted payload must not be able to reach persistence with it.
        var start = new DateTime(2026, 9, 1, 9, 0, 0);
        var rogue = new CampusVisitFormDto(
            "", start, start.AddHours(2), "Đoàn", "MEETING", null, "Thăm", null,
            new List<VisitorDto> { new("G", "VN", "G", "O") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op", "OpOrg", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null, null,
            new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2CampusProcessingRules.BuildPlans(Form(CampusVisit("HN", null), rogue)));
        Assert.Equal(VisitRequestErrorCodes.DirectModeCampusNotSelected, ex.ErrorCode);
    }

    [Fact]
    public void An_unknown_mode_is_rejected_rather_than_silently_ignored()
    {
        var plans = V2CampusProcessingRules.BuildPlans(
            Form(CampusVisit("HN", new CampusProcessingV2Dto("APPROVE_EVERYTHING", null))));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2CampusProcessingRules.ValidateShape(Leader("HN"), plans));
        Assert.Equal(VisitRequestErrorCodes.InvalidCampusSubmissionMode, ex.ErrorCode);
    }
}
