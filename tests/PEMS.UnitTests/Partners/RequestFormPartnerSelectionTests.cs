using System.Linq;
using PEMS.Application.Partners.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Partners;
using Xunit;

namespace PEMS.UnitTests.Partners;

/// <summary>
/// PART-09 — what a REGISTRATION FORM may offer as a delegation member's employer, and what it may
/// accept when the payload arrives.
///
/// <para>
/// This used to be decided by AUDIENCE: an authenticated Staff Leader got the internal option set,
/// which includes their own campus's profiles still awaiting a decision. So "Hồ sơ chờ duyệt · FPT
/// University Hà Nội" appeared in "Đơn vị công tác" on a registration form, was attached to a guest,
/// and travelled through approval on a request. Being signed in is not a reason to be offered an
/// unapproved partner: the form asks "which published organisation does this guest work for", and
/// that question has the same answer for everybody.
/// </para>
///
/// <para>
/// The partner MODULE is untouched — seeing pending and internal profiles is its job — and so is the
/// duplicate matcher, which must keep surfacing them so a second profile is not created for an
/// organisation that already has one. Those live behind their own queries; only the form's rule
/// changed. <see cref="PartnerLinkPolicyTests"/> pins the module side.
/// </para>
/// </summary>
public sealed class RequestFormPartnerSelectionTests
{
    private const ulong OwnCampus = 1;

    private static Partner Profile(
        ulong partnerId,
        string profileStatus = PartnerProfileStatuses.Approved,
        string visibility = PartnerVisibilities.Public,
        string cooperationStatus = "ACTIVE",
        ulong ownerCampusId = OwnCampus) =>
        new()
        {
            PartnerId = partnerId,
            Name = $"Partner {partnerId}",
            OwnerCampusId = ownerCampusId,
            ProfileStatus = profileStatus,
            Visibility = visibility,
            CooperationStatus = cooperationStatus,
        };

    private static ulong[] Selectable(params Partner[] partners) =>
        GuestOrganizationPartnerPolicy.RequestFormSelectable(partners.AsQueryable())
            .Select(p => p.PartnerId).ToArray();

    [Fact]
    public void Only_active_approved_public_profiles_are_selectable()
    {
        Assert.Equal(new ulong[] { 1 }, Selectable(Profile(1)));
    }

    [Fact]
    public void A_profile_awaiting_approval_is_not_selectable_even_for_its_own_campus()
    {
        // The exact case that produced the bug: a Staff Leader's own campus, pending, offered anyway.
        Assert.Empty(Selectable(Profile(1, profileStatus: PartnerProfileStatuses.PendingApproval)));
    }

    [Fact]
    public void Draft_and_rejected_profiles_are_not_selectable()
    {
        Assert.Empty(Selectable(
            Profile(1, profileStatus: PartnerProfileStatuses.Draft),
            Profile(2, profileStatus: PartnerProfileStatuses.Rejected)));
    }

    [Fact]
    public void Internal_and_private_visibility_are_not_selectable()
    {
        Assert.Empty(Selectable(
            Profile(1, visibility: PartnerVisibilities.Internal),
            Profile(2, visibility: PartnerVisibilities.Private)));
    }

    [Fact]
    public void An_inactive_cooperation_is_not_selectable()
    {
        Assert.Empty(Selectable(Profile(1, cooperationStatus: "INACTIVE")));
    }

    [Fact]
    public void The_form_rule_is_narrower_than_the_module_rule_it_replaced()
    {
        // Pinned as a RELATIONSHIP, not two separate lists: the point of the change is that the
        // module keeps its wider view while the form no longer borrows it.
        var pending = Profile(1, profileStatus: PartnerProfileStatuses.PendingApproval);
        var internalOnly = Profile(2, visibility: PartnerVisibilities.Internal);
        var all = new[] { pending, internalOnly, Profile(3) };

        var moduleSees = GuestOrganizationPartnerPolicy
            .InternalSelectable(all.AsQueryable(), OwnCampus)
            .Select(p => p.PartnerId).ToArray();
        var formSees = Selectable(all);

        Assert.Contains(1ul, moduleSees);
        Assert.Contains(2ul, moduleSees);
        Assert.Equal(new ulong[] { 3 }, formSees);
    }

    [Fact]
    public void The_refusal_says_what_is_wrong_without_saying_which_id()
    {
        // Telling "does not exist" apart from "not approved" would turn the form into a probe for
        // which partner ids exist and what state they are in.
        Assert.Equal("PARTNER_NOT_SELECTABLE", GuestOrganizationPartnerPolicy.NotSelectableCode);
        Assert.Contains("chưa được duyệt", GuestOrganizationPartnerPolicy.NotSelectableMessage);
        Assert.DoesNotContain("id", GuestOrganizationPartnerPolicy.NotSelectableMessage);
    }
}
