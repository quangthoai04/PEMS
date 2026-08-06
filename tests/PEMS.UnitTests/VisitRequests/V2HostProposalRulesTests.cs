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
/// The role × mode × campus matrix for the reception-host arrangement on the AUTHENTICATED v2 create.
///
/// <para>
/// These rules decide who may NAME a host for a campus. They no longer decide who IS one: a proposal
/// only becomes an assignment when the confirmation gate opens and the proposal is revalidated. Every
/// negative case here is still an authorization boundary though — a Staff proposing for a campus they
/// do not belong to, a Staff acting as a Leader, a Visitor forging a proposal — because a proposal
/// that survives to the gate is activated without anybody looking at it again.
/// </para>
///
/// <para>
/// The DB-dependent half (is this person an ACTIVE same-campus IC Staff) lives in the handler and in
/// <c>ProposedHostActivationService</c>, and is covered by IntegrationTests.
/// </para>
/// </summary>
public class V2HostProposalRulesTests
{
    private const ulong ActorId = 100;
    private const ulong OtherStaffId = 200;

    private static V2ProposalActor Visitor() =>
        new(IsVisitor: true, IsRegularStaff: false, IsStaffLeader: false,
            OwnCampusCode: null, OwnDepartmentIsIc: false, ActorUserId: ActorId);

    private static V2ProposalActor Staff(string campus = "HN", bool ic = true) =>
        new(IsVisitor: false, IsRegularStaff: true, IsStaffLeader: false,
            OwnCampusCode: campus, OwnDepartmentIsIc: ic, ActorUserId: ActorId);

    private static V2ProposalActor Leader(string campus = "HN") =>
        new(IsVisitor: false, IsRegularStaff: false, IsStaffLeader: true,
            OwnCampusCode: campus, OwnDepartmentIsIc: true, ActorUserId: ActorId);

    private static CampusVisitFormDto CampusVisit(string campusCode, CampusHostSelectionV2Dto? selection)
    {
        var start = new DateTime(2026, 9, 1, 9, 0, 0);
        return new CampusVisitFormDto(
            campusCode, start, start.AddHours(2),
            "Đoàn ABC", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null,
            selection);
    }

    private static VisitRequestFormDataV2 Form(params CampusVisitFormDto[] campuses) =>
        new("SUB-1",
            new RegistrantInputV2("Reg", "VN", "Org", "Job", "+8491", "reg@example.com"),
            null,
            campuses.ToList());

    private static IReadOnlyList<V2HostProposal> Authorize(
        V2ProposalActor actor, params CampusVisitFormDto[] campuses)
        => V2HostProposalRules.Authorize(actor, V2HostProposalRules.BuildProposals(Form(campuses)));

    // ── BuildProposals: every campus gets an answer, and "nothing" means WAIT_FOR_LATER ──────────

    [Fact]
    public void A_campus_with_no_host_section_waits_for_later()
    {
        var proposals = V2HostProposalRules.BuildProposals(Form(CampusVisit("HN", null)));

        var proposal = Assert.Single(proposals);
        Assert.Equal(HostSelectionModes.WaitForLater, proposal.Mode);
        Assert.Null(proposal.ProposedHostUserId);
    }

    [Fact]
    public void Each_campus_keeps_its_own_arrangement()
    {
        var proposals = V2HostProposalRules.BuildProposals(Form(
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)),
            CampusVisit("HCM", null)));

        Assert.Equal(new[] { "HN", "HCM" }, proposals.Select(p => p.CampusCode));
        Assert.Equal(HostSelectionModes.Self, proposals[0].Mode);
        Assert.Equal(HostSelectionModes.WaitForLater, proposals[1].Mode);
    }

    [Fact]
    public void An_unknown_mode_is_rejected_rather_than_silently_ignored()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            V2HostProposalRules.BuildProposals(
                Form(CampusVisit("HN", new CampusHostSelectionV2Dto("APPROVE_EVERYTHING", null)))));
        Assert.Equal(VisitRequestErrorCodes.InvalidHostSelectionMode, ex.ErrorCode);
    }

    [Fact]
    public void A_host_arrangement_for_an_unselected_campus_is_rejected()
    {
        // A campus visit whose own block names a different campus cannot occur through the UI, but a
        // hand-crafted payload must not reach persistence with it.
        var start = new DateTime(2026, 9, 1, 9, 0, 0);
        var rogue = new CampusVisitFormDto(
            "", start, start.AddHours(2), "Đoàn", "MEETING", null, "Thăm", null,
            new List<VisitorDto> { new("G", "VN", "G", "O") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto("Op", "OpOrg", "Trưởng phòng Hợp tác", "+8410", "op@example.com"),
            "EN", null, "DECLINED", null,
            new CampusHostSelectionV2Dto(HostSelectionModes.Self, null));

        var ex = Assert.Throws<BusinessRuleException>(
            () => V2HostProposalRules.BuildProposals(Form(CampusVisit("HN", null), rogue)));
        Assert.Equal(VisitRequestErrorCodes.HostSelectionCampusNotSelected, ex.ErrorCode);
    }

    [Fact]
    public void Wait_for_later_carrying_a_host_is_a_contradiction_and_is_refused()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Authorize(Leader("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.WaitForLater, OtherStaffId))));
        Assert.Equal(VisitRequestErrorCodes.InvalidHostSelectionMode, ex.ErrorCode);
    }

    // ── Visitor / external: never proposes anybody ───────────────────────────────────────────────

    [Fact]
    public void Visitor_cannot_propose_themself()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Authorize(Visitor(),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null))));
        Assert.Equal(VisitRequestErrorCodes.ProposedHostNotAllowedForRole, ex.ErrorCode);
    }

    [Fact]
    public void Visitor_cannot_smuggle_a_host_through_an_absent_mode()
    {
        // No mode at all defaults to WAIT_FOR_LATER, so the smuggled id has nowhere to hide: it is a
        // contradiction before it is even a permission question.
        var ex = Assert.Throws<BusinessRuleException>(() => Authorize(Visitor(),
            CampusVisit("HN", new CampusHostSelectionV2Dto(null, OtherStaffId))));
        Assert.Equal(VisitRequestErrorCodes.InvalidHostSelectionMode, ex.ErrorCode);
    }

    [Fact]
    public void Visitor_waiting_for_later_is_accepted_and_proposes_nobody()
    {
        var proposals = Authorize(Visitor(), CampusVisit("HN", null));
        Assert.Equal(HostSelectionModes.WaitForLater, Assert.Single(proposals).Mode);
    }

    // ── Regular IC Staff ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Staff_may_propose_themself_on_their_own_campus()
    {
        var proposals = Authorize(Staff("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)));

        var proposal = Assert.Single(proposals);
        Assert.Equal(HostSelectionModes.Self, proposal.Mode);
        // SELF is resolved from the session even when the payload said nothing.
        Assert.Equal(ActorId, proposal.ProposedHostUserId);
        Assert.Equal(DecisionActorRole.Staff, V2HostProposalRules.DecisionActorRoleFor(Staff("HN")));
    }

    [Fact]
    public void Staff_cannot_propose_for_a_campus_outside_their_scope()
    {
        Assert.Throws<ForbiddenException>(() => Authorize(Staff("HN"),
            CampusVisit("HCM", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null))));
    }

    [Fact]
    public void Staff_outside_the_ic_department_cannot_propose_themself()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Authorize(Staff("HN", ic: false),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null))));
        Assert.Equal(VisitRequestErrorCodes.SelfHostNotEligible, ex.ErrorCode);
    }

    [Fact]
    public void Staff_cannot_propose_somebody_else()
    {
        Assert.Throws<ForbiddenException>(() => Authorize(Staff("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Selected, OtherStaffId))));
    }

    [Fact]
    public void Self_naming_another_user_is_rejected_rather_than_quietly_corrected()
    {
        // Silently rewriting this to the caller would hide an attempt to assign under a mode that
        // does not require Leader rights.
        Assert.Throws<ForbiddenException>(() => Authorize(Staff("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, OtherStaffId))));
    }

    // ── Staff Leader ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Leader_may_propose_themself()
    {
        var proposals = Authorize(Leader("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)));

        Assert.Equal(ActorId, Assert.Single(proposals).ProposedHostUserId);
        Assert.Equal(DecisionActorRole.StaffLeader, V2HostProposalRules.DecisionActorRoleFor(Leader("HN")));
    }

    [Fact]
    public void Leader_may_propose_another_staff_on_their_own_campus()
    {
        var proposals = Authorize(Leader("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Selected, OtherStaffId)));

        var proposal = Assert.Single(proposals);
        Assert.Equal(HostSelectionModes.Selected, proposal.Mode);
        Assert.Equal(OtherStaffId, proposal.ProposedHostUserId);
    }

    [Fact]
    public void Leader_selecting_without_naming_anybody_is_rejected()
    {
        var ex = Assert.Throws<BusinessRuleException>(() => Authorize(Leader("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Selected, null))));
        Assert.Equal(VisitRequestErrorCodes.InvalidHostCandidate, ex.ErrorCode);
    }

    [Fact]
    public void Leader_cannot_propose_for_a_sibling_campus()
    {
        Assert.Throws<ForbiddenException>(() => Authorize(Leader("HN"),
            CampusVisit("HCM", new CampusHostSelectionV2Dto(HostSelectionModes.Selected, OtherStaffId))));
    }

    // ── Multi-campus: arrangements stay per campus ───────────────────────────────────────────────

    [Fact]
    public void An_own_campus_proposal_never_extends_to_the_sibling_campus()
    {
        var proposals = Authorize(Staff("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)),
            CampusVisit("HCM", null));

        Assert.Equal(ActorId, proposals[0].ProposedHostUserId);
        Assert.Null(proposals[1].ProposedHostUserId);
        Assert.Equal(HostSelectionModes.WaitForLater, proposals[1].Mode);
    }

    [Fact]
    public void A_single_out_of_scope_campus_rejects_the_whole_submission()
    {
        // Fail-closed: the transaction must not half-apply the in-scope campus.
        Assert.Throws<ForbiddenException>(() => Authorize(Staff("HN"),
            CampusVisit("HN", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null)),
            CampusVisit("HCM", new CampusHostSelectionV2Dto(HostSelectionModes.Self, null))));
    }

    // ── Capabilities the frontend renders from ──────────────────────────────────────────────────

    [Fact]
    public void Leader_gets_all_three_options_on_their_own_campus()
    {
        var caps = V2HostProposalRules.CapabilitiesFor(Leader("HN"), "HN");
        Assert.True(caps.CanProposeSelfAsHost);
        Assert.True(caps.CanProposeOtherHost);
        Assert.True(caps.CanWaitForLaterAssignment);
    }

    [Fact]
    public void Ic_staff_get_self_and_wait_but_never_choose_another()
    {
        var caps = V2HostProposalRules.CapabilitiesFor(Staff("HN"), "HN");
        Assert.True(caps.CanProposeSelfAsHost);
        Assert.False(caps.CanProposeOtherHost);
        Assert.True(caps.CanWaitForLaterAssignment);
    }

    [Fact]
    public void Visitors_get_no_proposal_controls_at_all()
    {
        var caps = V2HostProposalRules.CapabilitiesFor(Visitor(), "HN");
        Assert.False(caps.CanProposeSelfAsHost);
        Assert.False(caps.CanProposeOtherHost);
        Assert.False(caps.CanUpdateProposedHost);
    }

    [Fact]
    public void Internal_staff_get_no_proposal_controls_on_a_campus_that_is_not_theirs()
    {
        var caps = V2HostProposalRules.CapabilitiesFor(Leader("HN"), "HCM");
        Assert.False(caps.CanProposeSelfAsHost);
        Assert.False(caps.CanProposeOtherHost);
    }
}
