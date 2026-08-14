using PEMS.Application.Delegations.Common;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// NP-03 — deciding WHICH member of the delegation the campus's operational contact is.
///
/// <para>
/// The contact used to be five free-text columns with no relation to anything, so "is the person
/// coordinating this visit also one of the people attending it?" could only be answered by comparing
/// strings. That produced both failure modes at once: a contact who WAS in the delegation list turned
/// up twice in the biên bản, and a contact who was NOT in it turned up nowhere.
/// </para>
/// <para>
/// These pin the priority — an explicit pick beats a fingerprint, a fingerprint beats nothing, and
/// nothing at all is a legitimate answer rather than a guess.
/// </para>
/// </summary>
public class OperationalContactLinkTests
{
    private static VisitInstanceFormDetail Detail(
        string fullName = "Trần Thị B", string jobTitle = "Trưởng đoàn", string? org = "ABC University")
        => new()
        {
            VisitInstanceId = 10,
            DelegationName = "Đoàn ABC",
            Purpose = "Tham quan",
            OperationalContactFullName = fullName,
            OperationalContactJobTitle = jobTitle,
            OperationalContactOrganization = org,
            OperationalContactEmail = "b@abc.edu",
        };

    private static VisitGuestMember Member(
        ulong id, string fullName, string jobTitle, string org,
        string memberType = "GUEST", uint order = 0)
        => new()
        {
            GuestMemberId = id,
            VisitRequestId = 10,
            MemberType = memberType,
            FullName = fullName,
            JobTitle = jobTitle,
            Organization = org,
            Nationality = "VN",
            DisplayOrder = order,
        };

    [Fact]
    public void The_row_the_user_picked_wins()
    {
        var members = new[]
        {
            Member(1, "Nguyễn Văn A", "Thành viên", "ABC University", order: 0),
            Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University", order: 1),
        };
        var detail = Detail();

        OperationalContactLink.Resolve(detail, members, pickedVisitorIndex: 1);

        Assert.Equal(2UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void The_pick_survives_the_user_editing_the_contacts_job_title_afterwards()
    {
        // This is the whole reason the index exists. Pick row 1, then change the contact's title —
        // a name+role+organisation match would now find nobody and silently drop the link.
        var members = new[] { Member(7, "Trần Thị B", "Trưởng đoàn", "ABC University") };
        var detail = Detail(jobTitle: "Phó trưởng đoàn");

        OperationalContactLink.Resolve(detail, members, pickedVisitorIndex: 0);

        Assert.Equal(7UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_stale_index_falls_back_to_matching_instead_of_failing()
    {
        // The user picked somebody and then deleted a row above them. Refusing the submission over
        // that would throw away a long form; the snapshot still identifies the same person.
        var members = new[] { Member(7, "Trần Thị B", "Trưởng đoàn", "ABC University") };
        var detail = Detail();

        OperationalContactLink.Resolve(detail, members, pickedVisitorIndex: 9);

        Assert.Equal(7UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Without_a_pick_the_snapshot_is_matched_on_name_role_and_organisation()
    {
        var members = new[]
        {
            Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"),
            Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"),
        };
        var detail = Detail();

        OperationalContactLink.Resolve(detail, members);

        Assert.Equal(2UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Matching_ignores_letter_case_and_stray_whitespace()
    {
        var members = new[] { Member(4, "  trần   thị b ", "TRƯỞNG ĐOÀN", " ABC University ") };

        var detail = Detail();
        OperationalContactLink.Resolve(detail, members);

        Assert.Equal(4UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_name_alone_is_never_enough()
    {
        // Two people named Trần Thị B, in different roles at different places, are two people. The
        // contact is neither of them, and guessing would attach the visit's coordinator to a stranger.
        var members = new[]
        {
            Member(1, "Trần Thị B", "Thành viên", "XYZ University"),
            Member(2, "Trần Thị B", "Sinh viên", "ABC University"),
        };
        var detail = Detail(jobTitle: "Trưởng đoàn", org: "ABC University");

        OperationalContactLink.Resolve(detail, members);

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_contact_who_is_not_in_the_delegation_links_to_nobody()
    {
        // Not an error: coordinating a visit you are not attending is ordinary. The biên bản adds
        // them from the snapshot instead.
        var members = new[] { Member(1, "Nguyễn Văn A", "Thành viên", "ABC University") };
        var detail = Detail();

        OperationalContactLink.Resolve(detail, members);

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_delegation_guest_is_preferred_over_an_identical_support_member()
    {
        // The guest-side contact is far more likely to be part of the delegation proper than one of
        // the support staff FPTU's side arranged.
        var members = new[]
        {
            Member(9, "Trần Thị B", "Trưởng đoàn", "ABC University", memberType: "EXTERNAL_SUPPORT", order: 0),
            Member(3, "Trần Thị B", "Trưởng đoàn", "ABC University", memberType: "GUEST", order: 1),
        };
        var detail = Detail();

        OperationalContactLink.Resolve(detail, members);

        Assert.Equal(3UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void An_empty_delegation_leaves_the_link_null()
    {
        var detail = Detail();

        OperationalContactLink.Resolve(detail, Array.Empty<VisitGuestMember>());

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Re_resolving_replaces_a_link_that_points_at_a_deleted_row()
    {
        // What the edit path does: copy-on-write deletes every member row and creates fresh ones, so
        // the id the contact holds names a row that no longer exists. Resolve must overwrite it,
        // never leave the stale value behind.
        var detail = Detail();
        detail.OperationalContactGuestMemberId = 999;   // the deleted row

        OperationalContactLink.Resolve(detail, new[] { Member(42, "Trần Thị B", "Trưởng đoàn", "ABC University") });

        Assert.Equal(42UL, detail.OperationalContactGuestMemberId);
    }
}
