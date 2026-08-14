using PEMS.Application.Common.Interfaces;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Partners;
using Xunit;

namespace PEMS.UnitTests.Partners;

/// <summary>
/// PART-04 — "may I read this partner" and "may I say this person belongs to it" are different
/// questions, and the second one is what a link asserts.
///
/// <para>They used to share an answer: the matcher set every candidate's <c>CanLink</c> from
/// <see cref="PartnerAccess.CanViewPartner"/>, and the link command checked the same thing. Campus
/// staff can see every profile their campus owns — including the ones their Staff Leader has just
/// REJECTED — so the modal offered "Liên kết" on a dead profile and the command accepted it,
/// producing a confirmed business relationship against an organization the university had refused.
/// The fix is a policy of its own; these tests pin its matrix so the two can never be re-merged by
/// accident.</para>
/// </summary>
public sealed class PartnerLinkPolicyTests
{
    private const ulong OwnCampus = 1;
    private const ulong OtherCampus = 2;

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAuthenticated => UserId is not null;
        public ulong? UserId { get; init; } = 10;
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; init; }
        public string? SubRole { get; init; }
        public ulong? PrimaryCampusId { get; init; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Staff(ulong campusId = OwnCampus) =>
        new() { RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Staff, PrimaryCampusId = campusId };

    private static FakeUser StaffLeader(ulong campusId = OwnCampus) =>
        new() { RoleCode = RoleCodes.Staff, SubRole = UserSubRoles.Leader, PrimaryCampusId = campusId };

    private static FakeUser Ho() => new() { RoleCode = RoleCodes.Ho };

    private static Partner Profile(
        string profileStatus,
        ulong ownerCampusId = OwnCampus,
        string visibility = PartnerVisibilities.Internal) =>
        new() { OwnerCampusId = ownerCampusId, ProfileStatus = profileStatus, Visibility = visibility };

    // ── The four profile statuses, from the campus that owns them ────────────────

    [Fact]
    public void Approved_profile_of_own_campus_is_linkable()
    {
        Assert.Null(PartnerAccess.LinkBlockReason(Staff(), Profile(PartnerProfileStatuses.Approved)));
        Assert.True(PartnerAccess.CanLinkPartner(Staff(), Profile(PartnerProfileStatuses.Approved)));
    }

    [Fact]
    public void Rejected_profile_is_never_linkable_even_by_the_campus_that_owns_it()
    {
        var rejected = Profile(PartnerProfileStatuses.Rejected);

        // Visible — the staff must still be able to open it and read WHY it was refused …
        Assert.True(PartnerAccess.CanViewPartner(Staff(), rejected));
        // … but a relationship against it is exactly what must not be creatable.
        Assert.Equal(PartnerLinkBlockReasons.Rejected, PartnerAccess.LinkBlockReason(Staff(), rejected));
        Assert.False(PartnerAccess.CanLinkPartner(Staff(), rejected));
        Assert.False(PartnerAccess.CanLinkPartner(StaffLeader(), rejected));
    }

    [Fact]
    public void A_rejected_candidate_is_pointed_at_resubmit_rather_than_left_with_nothing_to_do()
    {
        // The route out of a rejection is edit + resubmit — never a second profile under the same
        // name, which the duplicate-name guard blocks anyway.
        Assert.Equal(
            PartnerLinkRecommendedActions.Resubmit,
            PartnerAccess.RecommendedActionFor(PartnerLinkBlockReasons.Rejected));
    }

    [Fact]
    public void Draft_profile_is_not_linkable()
    {
        var draft = Profile(PartnerProfileStatuses.Draft);
        Assert.Equal(PartnerLinkBlockReasons.Draft, PartnerAccess.LinkBlockReason(Staff(), draft));
        Assert.Equal(
            PartnerLinkRecommendedActions.None,
            PartnerAccess.RecommendedActionFor(PartnerLinkBlockReasons.Draft));
    }

    [Fact]
    public void Pending_profile_is_linkable_by_its_own_campus_so_work_can_continue_while_it_waits()
    {
        var pending = Profile(PartnerProfileStatuses.PendingApproval);
        Assert.Null(PartnerAccess.LinkBlockReason(Staff(), pending));
        Assert.Null(PartnerAccess.LinkBlockReason(StaffLeader(), pending));
    }

    [Fact]
    public void Pending_profile_of_ANOTHER_campus_is_not_linkable()
    {
        // Nobody outside the owning campus should build history against a profile that campus has
        // not decided yet.
        var pending = Profile(PartnerProfileStatuses.PendingApproval, ownerCampusId: OtherCampus);

        // For campus staff the answer arrives one step earlier and says LESS: they cannot see a
        // foreign campus's undecided profile at all, so the reason is scope. That ordering is
        // deliberate — "pending elsewhere" would confirm the record exists.
        Assert.Equal(
            PartnerLinkBlockReasons.OutOfScope,
            PartnerAccess.LinkBlockReason(Staff(OwnCampus), pending));

        // HO reads every campus, so for them the profile IS visible and the block is the real
        // reason: it is still awaiting its owner campus's decision.
        Assert.Equal(
            PartnerLinkBlockReasons.PendingOtherCampus,
            PartnerAccess.LinkBlockReason(Ho(), pending));
    }

    // ── Scope is answered before status, so nothing leaks ────────────────────────

    [Fact]
    public void An_out_of_scope_private_profile_reports_scope_not_its_status()
    {
        // A caller who cannot see the profile must not learn from the block reason that it exists and
        // was rejected — that would turn the matcher into an oracle for other campuses' records.
        var hidden = Profile(
            PartnerProfileStatuses.Rejected,
            ownerCampusId: OtherCampus,
            visibility: PartnerVisibilities.Private);

        Assert.Equal(PartnerLinkBlockReasons.OutOfScope, PartnerAccess.LinkBlockReason(Staff(OwnCampus), hidden));
    }

    [Fact]
    public void Ho_may_link_an_approved_profile_of_any_campus_but_not_a_rejected_one()
    {
        Assert.True(PartnerAccess.CanLinkPartner(
            Ho(), Profile(PartnerProfileStatuses.Approved, ownerCampusId: OtherCampus)));
        Assert.False(PartnerAccess.CanLinkPartner(
            Ho(), Profile(PartnerProfileStatuses.Rejected, ownerCampusId: OtherCampus)));
    }

    // ── The audience split behind the option endpoints (PART-03) ─────────────────

    [Fact]
    public void A_visitor_account_is_on_the_public_side_of_the_option_split()
    {
        // Signed in, but the internal partner records are not theirs to browse.
        var visitor = new FakeUser { RoleCode = RoleCodes.Visitor };
        Assert.False(GuestOrganizationPartnerPolicy.IsInternalAudience(visitor));
        Assert.True(GuestOrganizationPartnerPolicy.IsInternalAudience(Staff()));
    }

    [Fact]
    public void No_session_at_all_resolves_to_the_public_option_set()
    {
        // The unknown case must land on the NARROWER set: a missing dependency has to tighten the
        // check, never open it.
        Assert.False(GuestOrganizationPartnerPolicy.IsInternalAudience(null));
    }
}
