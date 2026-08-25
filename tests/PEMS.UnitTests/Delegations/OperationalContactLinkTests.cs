using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Delegations.Common;
using PEMS.Domain.Constants;
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
/// The first fix answered it with the member's ARRAY POSITION, which is not an identity: inserting a
/// row above the chosen one, deleting one, or sorting the list silently re-aimed the contact at a
/// different person. These pin the replacement — a per-row key minted by the form — and, just as
/// importantly, the cases where the answer is a refusal rather than a guess.
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

    /// <summary>Members with the keys the form knew them by — what a real payload resolves against.</summary>
    private static IReadOnlyList<KeyedMember> Keyed(params (VisitGuestMember Member, string Key)[] rows)
        => rows.Select(r => new KeyedMember(r.Member, r.Key)).ToList();

    private static IReadOnlyList<KeyedMember> Unkeyed(params VisitGuestMember[] rows)
        => rows.Select(m => new KeyedMember(m, null)).ToList();

    // ── The pick is a key, and a key survives what a position does not ───────────

    [Fact]
    public void The_member_the_user_picked_wins()
    {
        var members = Keyed(
            (Member(1, "Nguyễn Văn A", "Thành viên", "ABC University", order: 0), "k-a"),
            (Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University", order: 1), "k-b"));

        var detail = Detail();
        OperationalContactLink.Resolve(detail, members, "k-b");

        Assert.Equal(2UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Inserting_a_row_ABOVE_the_pick_does_not_re_aim_it()
    {
        // The whole reason positions were abandoned. With an index of 1 the contact silently became
        // the newcomer; with a key it stays the person the user chose.
        var afterInsert = Keyed(
            (Member(9, "Người mới thêm", "Thành viên", "ABC University", order: 0), "k-new"),
            (Member(1, "Nguyễn Văn A", "Thành viên", "ABC University", order: 1), "k-a"),
            (Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University", order: 2), "k-b"));

        var detail = Detail();
        OperationalContactLink.Resolve(detail, afterInsert, "k-b");

        Assert.Equal(2UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Reordering_the_list_does_not_re_aim_the_pick()
    {
        var reordered = Keyed(
            (Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University", order: 0), "k-b"),
            (Member(1, "Nguyễn Văn A", "Thành viên", "ABC University", order: 1), "k-a"));

        var detail = Detail();
        OperationalContactLink.Resolve(detail, reordered, "k-a");

        Assert.Equal(1UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void The_pick_survives_the_member_being_edited_afterwards()
    {
        // Renaming somebody, correcting their job title or picking a different partner behind their
        // organisation are all edits to ONE person. None of them makes them a different human, and
        // none may move the link — a fingerprint match would lose it on any of the three.
        var edited = Keyed((Member(7, "Trần Thị B (đã sửa)", "Phó trưởng đoàn", "ABC University"), "k-b"));

        var detail = Detail();
        OperationalContactLink.Resolve(detail, edited, "k-b");

        Assert.Equal(7UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Support_staff_travelling_with_the_delegation_may_hold_the_role()
    {
        // An interpreter or a travelling coordinator is frequently the person a campus actually rings.
        // Only GUEST rows used to be offered, so the obvious answer could not be given at all.
        var members = Keyed(
            (Member(1, "Nguyễn Văn A", "Trưởng đoàn", "ABC University"), "k-a"),
            (Member(5, "Lê Thị C", "Phiên dịch", "ABC University", memberType: "EXTERNAL_SUPPORT"), "k-c"));

        var detail = Detail();
        OperationalContactLink.Resolve(detail, members, "k-c");

        Assert.Equal(5UL, detail.OperationalContactGuestMemberId);
    }

    // ── The refusals: a key that cannot name one eligible person ─────────────────

    [Fact]
    public void Removing_the_member_who_is_the_contact_is_refused_rather_than_absorbed()
    {
        // The submitted key names nobody, which in practice means the delegation row holding the role
        // was deleted in the same edit. Dropping the link quietly would put the contact back to being
        // a name to be string-matched; keeping it would point at a row that no longer exists.
        var afterDelete = Keyed((Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"), "k-a"));

        var ex = Assert.Throws<BusinessRuleException>(() =>
            OperationalContactLink.Resolve(Detail(), afterDelete, "k-b"));

        Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
        Assert.Contains("đầu mối", ex.Message);
    }

    [Fact]
    public void Two_rows_under_one_key_are_refused_instead_of_resolving_to_whichever_comes_first()
    {
        var duplicated = Keyed(
            (Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"), "k-dup"),
            (Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"), "k-dup"));

        var ex = Assert.Throws<BusinessRuleException>(() =>
            OperationalContactLink.Resolve(Detail(), duplicated, "k-dup"));

        Assert.Equal(OperationalContactErrorCodes.MemberAmbiguous, ex.ErrorCode);
    }

    [Fact]
    public void A_member_type_outside_the_guest_side_cannot_hold_the_role()
    {
        // Defence in depth. Nothing writes such a row today — member_type is GUEST or EXTERNAL_SUPPORT
        // and FPTU's own people live in visit_participants — so this pins the rule rather than a
        // reachable path, and would catch a third member_type being added without a decision.
        var members = Keyed((Member(3, "Người nội bộ", "Cán bộ", "FPTU", memberType: "INTERNAL"), "k-i"));

        var ex = Assert.Throws<BusinessRuleException>(() =>
            OperationalContactLink.Resolve(Detail(), members, "k-i"));

        Assert.Equal(OperationalContactErrorCodes.MemberNotEligible, ex.ErrorCode);
    }

    [Fact]
    public void A_key_from_a_DIFFERENT_campus_names_nobody_here()
    {
        // Each campus keeps its own independent member rows, and only this campus's rows are ever
        // offered to the resolver — so a key belonging to a sibling campus (or another request
        // entirely) cannot resolve, whatever it is.
        var thisCampus = Keyed((Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"), "k-here"));

        var ex = Assert.Throws<BusinessRuleException>(() =>
            OperationalContactLink.Resolve(Detail(), thisCampus, "k-somewhere-else"));

        Assert.Equal(OperationalContactErrorCodes.MemberNotFound, ex.ErrorCode);
    }

    // ── The snapshot follows the member, never the other way round ───────────────

    [Fact]
    public void The_snapshot_is_rewritten_from_the_member_so_it_cannot_describe_somebody_else()
    {
        // A payload carrying Daniel's key beside another person's name stores ONE person, not two
        // halves of a contradiction.
        var detail = Detail(fullName: "Tên khác", jobTitle: "Chức vụ khác", org: "Đơn vị khác");
        var member = Member(2, "Daniel Kim", "Program Manager", "ABC University");

        OperationalContactLink.ApplySnapshotFromMember(detail, member);

        Assert.Equal("Daniel Kim", detail.OperationalContactFullName);
        Assert.Equal("Program Manager", detail.OperationalContactJobTitle);
        Assert.Equal("ABC University", detail.OperationalContactOrganization);
    }

    [Fact]
    public void Rewriting_the_snapshot_leaves_phone_and_email_alone()
    {
        // A delegation row has neither, so copying "nothing" over them would delete the only way the
        // campus has to reach this person.
        var detail = Detail();
        detail.OperationalContactPhone = "+84912345678";

        OperationalContactLink.ApplySnapshotFromMember(
            detail, Member(2, "Daniel Kim", "Program Manager", "ABC University"));

        Assert.Equal("+84912345678", detail.OperationalContactPhone);
        Assert.Equal("b@abc.edu", detail.OperationalContactEmail);
    }

    [Fact]
    public void A_member_with_no_organisation_blanks_the_column_rather_than_storing_an_empty_string()
    {
        // The DB CHECK rejects '' — the nullable column's own way of saying "not stated".
        var detail = Detail();

        OperationalContactLink.ApplySnapshotFromMember(detail, Member(2, "Daniel Kim", "PM", "   "));

        Assert.Null(detail.OperationalContactOrganization);
    }

    // ── No pick, from a client that mints keys: "nobody" is the answer ───────────

    [Fact]
    public void A_client_that_mints_keys_and_picks_nobody_links_to_nobody()
    {
        // "Đây là hai người khác nhau." The form asks precisely that before it submits, whenever an
        // unlinked contact's fingerprint fits exactly one member — and the answer used to travel no
        // further than the browser. The payload arrived with no pick, the fingerprint fallback
        // matched anyway, and the two buttons in the dialog produced identical data: the biên bản
        // showed ONE row wearing both "Khách" and "Đầu mối" for two humans.
        var detail = Detail();                                       // Trần Thị B / Trưởng đoàn / ABC
        var members = Keyed(
            (Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"), "k-a"),
            (Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"), "k-b")); // same fingerprint

        OperationalContactLink.Resolve(detail, members, null);

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void One_member_carrying_a_key_is_enough_to_take_the_client_at_its_word()
    {
        // A payload that names keys at all is one that asked the question. It does not have to key
        // every row for its silence about the contact to mean something.
        var detail = Detail();
        var members = new List<KeyedMember>
        {
            new(Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"), "k-a"),
            new(Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"), null),
        };

        OperationalContactLink.Resolve(detail, members, null);

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    // ── No keys at all: the fallback is a guess, and behaves like one ────────────

    [Fact]
    public void Without_a_key_the_snapshot_is_matched_on_name_role_and_organisation()
    {
        // The path an amendment takes: member lists stored days ago, no key from that form left.
        var detail = Detail();
        OperationalContactLink.Resolve(detail, new[]
        {
            Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"),
            Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"),
        });

        Assert.Equal(2UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void Matching_ignores_letter_case_and_stray_whitespace()
    {
        var detail = Detail();
        OperationalContactLink.Resolve(
            detail, Unkeyed(Member(4, "  trần   thị b ", "TRƯỞNG ĐOÀN", " ABC University ")), null);

        Assert.Equal(4UL, detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_name_alone_is_never_enough()
    {
        // Two people named Trần Thị B, in different roles at different places, are two people. The
        // contact is neither of them, and guessing would attach the visit's coordinator to a stranger.
        var detail = Detail(jobTitle: "Trưởng đoàn", org: "ABC University");
        OperationalContactLink.Resolve(detail, new[]
        {
            Member(1, "Trần Thị B", "Thành viên", "XYZ University"),
            Member(2, "Trần Thị B", "Sinh viên", "ABC University"),
        });

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_contact_who_is_not_in_the_delegation_links_to_nobody()
    {
        // Not an error: coordinating a visit you are not attending is ordinary. The biên bản adds
        // them from the snapshot instead.
        var detail = Detail();
        OperationalContactLink.Resolve(
            detail, new[] { Member(1, "Nguyễn Văn A", "Thành viên", "ABC University") });

        Assert.Null(detail.OperationalContactGuestMemberId);
    }

    [Fact]
    public void A_delegation_guest_is_preferred_over_an_identical_support_member()
    {
        // Both are eligible, so a fingerprint that fits two rows has to break the tie somehow. The
        // guest-side row wins: the delegation proper is the more likely reading of an ambiguous match.
        var detail = Detail();
        OperationalContactLink.Resolve(detail, new[]
        {
            Member(9, "Trần Thị B", "Trưởng đoàn", "ABC University", memberType: "EXTERNAL_SUPPORT", order: 0),
            Member(3, "Trần Thị B", "Trưởng đoàn", "ABC University", memberType: "GUEST", order: 1),
        });

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

        OperationalContactLink.Resolve(
            detail, Keyed((Member(42, "Trần Thị B", "Trưởng đoàn", "ABC University"), "k-b")), "k-b");

        Assert.Equal(42UL, detail.OperationalContactGuestMemberId);
    }

    // ── Pairing rows with the keys they arrived under ────────────────────────────

    [Fact]
    public void Pair_aligns_rows_with_keys_and_tolerates_a_payload_that_sent_none()
    {
        var rows = new[]
        {
            Member(1, "Nguyễn Văn A", "Thành viên", "ABC University"),
            Member(2, "Trần Thị B", "Trưởng đoàn", "ABC University"),
        };

        var paired = OperationalContactLink.Pair(rows, new List<string?> { "k-a" });

        Assert.Equal("k-a", paired[0].ClientMemberKey);
        // A short (or absent) key list is not a fault — Pair simply leaves the tail unkeyed. What the
        // resolver then makes of it is decided in Match, not here.
        Assert.Null(paired[1].ClientMemberKey);
        Assert.Equal(2, OperationalContactLink.Pair(rows, null).Count);
    }

    // ── CheckPreservesExistingMemberRelation: the continuity classifier (operational-contact
    // consistency fix, CONT-01..10). Pure — no DB, no entities beyond the (GuestMemberId, ClientMemberKey)
    // tuples the real call sites project their Visitors/ExternalSupportMembers rows into. Each case pins
    // one row of the authoritative classification table from the fix plan; the order matters (see the
    // method's own doc comment) and these are what prove that order is actually implemented, not just
    // documented. ──

    private static (ulong? GuestMemberId, string? ClientMemberKey) Row(ulong? id, string? key) => (id, key);

    [Fact]
    public void CONT01_the_same_persisted_member_under_the_same_key_is_preserved()
    {
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-kim") },
            proposedClientMemberKey: "k-kim");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.Preserved, result);
    }

    [Fact]
    public void CONT02_kim_present_but_the_key_points_at_a_different_real_member_is_a_repoint()
    {
        // The disguised Kim(100)→Moon(200) case: both are genuinely present, but Kim's own id being
        // provable at all means any OTHER proposed key is a repoint attempt, never a guess.
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-kim"), Row(200, "k-moon") },
            proposedClientMemberKey: "k-moon");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere, result);
    }

    [Fact]
    public void CONT03_the_linked_member_is_genuinely_gone_with_no_stale_signature()
    {
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(300, "k-someone-else") },
            proposedClientMemberKey: "k-someone-else");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.CurrentMemberMissing, result);
    }

    [Fact]
    public void CONT04_two_incoming_rows_carrying_the_current_id_is_never_accept_the_first()
    {
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-a"), Row(100, "k-b") },
            proposedClientMemberKey: "k-a");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.DuplicatePersistentId, result);
    }

    [Fact]
    public void CONT05_zero_current_matches_and_the_key_uniquely_names_a_null_id_row_is_the_stale_client_shape()
    {
        // The structural signature of a pre-upgrade client: the row the OLD key-only logic would have
        // picked exists, it simply predates the GuestMemberId field.
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(null, "k-kim") },
            proposedClientMemberKey: "k-kim");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.MissingIdentityEvidence, result);
    }

    [Fact]
    public void CONT06_kim_present_but_no_key_at_all_is_a_repoint_not_missing_evidence()
    {
        // Evidence for the CURRENT member already exists (id 100 is present), so this state can never
        // also mean "no evidence" — a null/empty proposed key here is a repoint-to-nothing, not a stale
        // client and not a default Preserved.
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-kim") },
            proposedClientMemberKey: null);

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere, result);
    }

    [Fact]
    public void CONT07_kim_present_and_the_key_names_a_brand_new_null_id_row_is_still_a_repoint()
    {
        // Round 9's correction: a payload proving Kim(100) is present and then pointing the relation at
        // a freshly-added, not-yet-persisted "Moon" row is a repoint attempt — NOT MissingIdentityEvidence,
        // because that result requires zero evidence the current member is present, which is false here.
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-kim"), Row(null, "k-moon") },
            proposedClientMemberKey: "k-moon");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere, result);
    }

    [Fact]
    public void CONT08_kim_present_and_the_key_names_nobody_at_all_is_a_repoint()
    {
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: 100,
            incomingRows: new[] { Row(100, "k-kim") },
            proposedClientMemberKey: "does-not-exist");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.RelationKeyPointsElsewhere, result);
    }

    [Fact]
    public void CONT09_currently_unlinked_and_a_key_is_proposed_anyway_introduces_a_relation()
    {
        // Round 10's correction: relation EXISTENCE must be preserved too, not just identity — turning
        // "not in the delegation" into "linked" is exclusively Safe Edit's (link) job.
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: null,
            incomingRows: new[] { Row(100, "k-kim") },
            proposedClientMemberKey: "k-kim");

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.RelationIntroduced, result);
    }

    [Fact]
    public void CONT10_currently_unlinked_and_no_key_is_proposed_stays_preserved()
    {
        var result = OperationalContactLink.CheckPreservesExistingMemberRelation(
            currentGuestMemberId: null,
            incomingRows: new[] { Row(100, "k-kim") },
            proposedClientMemberKey: null);

        Assert.Equal(OperationalContactLink.ContactMemberContinuityResult.Preserved, result);
    }
}
