using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Common;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// ID-02 — one person, one member row, across BOTH of a campus's lists.
///
/// <para>
/// The delegation list and the support list are two doors into <c>visit_guest_members</c>, and
/// nothing compared them: the Excel importer de-duplicates inside the list it is importing, and the
/// validator checked each array on its own. So the same human written into both was stored as two
/// rows with two different <c>guest_member_id</c>s — and from that moment they really WERE two
/// people as far as everything downstream was concerned. The biên bản listed them twice, the
/// headcount was wrong, and no de-duplication in the biên bản could have fixed it.
/// </para>
///
/// <para>
/// The rule is deliberately strict about what counts as the same person and deliberately final about
/// what happens when it does. Two rows matching on every field the form collects have nothing left
/// that could distinguish them, and the user has already been asked (the form runs this same rule
/// before submitting). Two rows differing anywhere are two people and pass — so "add the
/// distinguishing detail" is always an available answer.
/// </para>
/// </summary>
public class MemberDuplicatePolicyTests
{
    private static MemberIdentityInput Row(
        string kind = MemberDuplicatePolicy.GuestKind,
        int index = 0,
        string name = "Nguyễn Văn An",
        string jobTitle = "Giám đốc",
        string organization = "ABC University",
        ulong? partnerId = null,
        string nationality = "Việt Nam")
        => new(kind, index, name, jobTitle, organization, partnerId, nationality);

    [Fact]
    public void The_same_person_in_both_lists_is_one_person()
    {
        var duplicates = MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(MemberDuplicatePolicy.GuestKind, 0),
            Row(MemberDuplicatePolicy.SupportKind, 0),
        });

        var pair = Assert.Single(duplicates);
        Assert.Equal(MemberDuplicatePolicy.GuestKind, pair.First.Kind);
        Assert.Equal(MemberDuplicatePolicy.SupportKind, pair.Second.Kind);
    }

    [Fact]
    public void A_shared_name_alone_is_never_enough()
    {
        // Two members of one delegation sharing a name is ordinary — merging them would attach the
        // visit's coordinator to a stranger.
        Assert.Empty(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0),
            Row(index: 1, jobTitle: "Trợ lý"),
        }));
    }

    [Fact]
    public void A_different_nationality_makes_two_people()
    {
        Assert.Empty(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0),
            Row(index: 1, nationality: "Hàn Quốc"),
        }));
    }

    [Fact]
    public void Case_and_whitespace_do_not_make_two_people()
    {
        Assert.Single(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0, name: "Nguyễn Văn An"),
            Row(index: 1, name: "  nguyễn   VĂN an "),
        }));
    }

    [Fact]
    public void Accents_are_left_alone_so_two_real_names_stay_apart()
    {
        // Folding them would make "Nguyễn Văn An" and "Nguyen Van An" one person, which in a system
        // full of Vietnamese names is a merge waiting to happen.
        Assert.Empty(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0, name: "Nguyễn Văn An"),
            Row(index: 1, name: "Nguyen Van An"),
        }));
    }

    [Fact]
    public void A_shared_partner_id_settles_two_spellings_of_one_employer()
    {
        Assert.Single(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0, organization: "ABC University (ABC)", partnerId: 7),
            Row(index: 1, organization: "ABC University", partnerId: 7),
        }));
    }

    [Fact]
    public void Two_different_partner_profiles_stay_apart_under_one_name()
    {
        Assert.Empty(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0, partnerId: 7),
            Row(index: 1, partnerId: 8),
        }));
    }

    [Fact]
    public void An_unnamed_row_matches_nothing()
    {
        // Half-typed rows are ordinary while a form is open; refusing a submit because two of them
        // were equally blank would be nonsense.
        Assert.Empty(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0, name: "  "),
            Row(index: 1, name: ""),
        }));
    }

    [Fact]
    public void A_person_entered_three_times_is_one_conflict_to_resolve()
    {
        Assert.Single(MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(index: 0), Row(index: 1), Row(index: 2),
        }));
    }

    [Fact]
    public void The_message_names_the_person_and_says_what_to_do()
    {
        var duplicates = MemberDuplicatePolicy.FindDuplicates(new[]
        {
            Row(MemberDuplicatePolicy.GuestKind, 0, name: "Liam O'Connor"),
            Row(MemberDuplicatePolicy.SupportKind, 1, name: "Liam O'Connor"),
        });

        var message = MemberDuplicatePolicy.DescribeConflicts(duplicates);
        Assert.Contains("Liam O'Connor", message);
        Assert.Contains("khách", message);
        Assert.Contains("nhân sự hỗ trợ", message);
        // Both answers are legitimate, so the message offers both rather than only "remove one".
        Assert.Contains("phân biệt", message);
    }
}

/// <summary>
/// The same rule at the boundary the four write paths share (create, pending-edit request,
/// pending-edit one instance, resubmit) — none of them can accept a campus holding one person twice.
/// </summary>
public class CampusMemberDuplicateValidationTests
{
    private static readonly CampusVisitFormDtoValidator Validator = new();

    private static VisitorDto Guest(string name, string key, string jobTitle = "Thành viên") =>
        new(name, "Việt Nam", jobTitle, "ABC University", null, key);

    private static SupportTeamMemberDto Support(string name, string key, string jobTitle = "Thành viên") =>
        new(name, jobTitle, "ABC University", "Việt Nam", null, key);

    private static CampusVisitFormDto Campus(
        IList<VisitorDto> visitors, IList<SupportTeamMemberDto>? support = null)
        => new(
            CampusId: "HN",
            PlannedStartAt: new DateTime(2026, 9, 1, 9, 0, 0),
            PlannedEndAt: new DateTime(2026, 9, 1, 11, 0, 0),
            DelegationName: "Đoàn ABC",
            VisitType: "MEETING",
            VisitTypeOther: null,
            Purpose: "Tham quan",
            WorkingContent: "Trao đổi hợp tác",
            Visitors: visitors,
            ExternalSupportMembers: support ?? new List<SupportTeamMemberDto>(),
            OperationalContact: new ContactPointDto(
                "Trần Thị B", "ABC University", "Trưởng đoàn", "+84912345678", "b@abc.edu"),
            WorkingLanguage: "VI",
            TransportationNote: null,
            MediaConsentStatus: "AGREED",
            Notes: null,
            HostSelection: null,
            OperationalContactClientMemberKey: null);

    private static IEnumerable<string> ErrorsFor(CampusVisitFormDto campus) =>
        Validator.Validate(campus).Errors.Select(e => e.ErrorMessage);

    [Fact]
    public void A_guest_repeated_as_a_support_member_is_refused()
    {
        var errors = ErrorsFor(Campus(
            new[] { Guest("Liam O'Connor", "k-a") },
            new[] { Support("Liam O'Connor", "k-b") }));

        Assert.Contains(errors, m => m.Contains("Liam O'Connor", StringComparison.Ordinal));
    }

    [Fact]
    public void The_refusal_carries_a_code_a_client_can_branch_on()
    {
        var failures = Validator.Validate(Campus(
            new[] { Guest("Liam O'Connor", "k-a") },
            new[] { Support("Liam O'Connor", "k-b") })).Errors;

        Assert.Contains(failures, f => f.ErrorCode == MemberDuplicatePolicy.DuplicateCode);
    }

    [Fact]
    public void A_delegation_of_distinct_people_passes()
    {
        var errors = ErrorsFor(Campus(
            new[] { Guest("Liam O'Connor", "k-a"), Guest("Ana Costa", "k-b") },
            new[] { Support("Kim Min Jae", "k-c") }));

        Assert.DoesNotContain(errors, m => m.Contains("nhiều lần", StringComparison.Ordinal));
    }

    [Fact]
    public void Distinguishing_them_is_a_way_through()
    {
        // The answer "they really are two different people" has to remain available, or the refusal
        // would be a dead end for a delegation that genuinely contains two people of one name.
        var errors = ErrorsFor(Campus(
            new[] { Guest("Liam O'Connor", "k-a", jobTitle: "Trưởng đoàn") },
            new[] { Support("Liam O'Connor", "k-b", jobTitle: "Phiên dịch") }));

        Assert.DoesNotContain(errors, m => m.Contains("nhiều lần", StringComparison.Ordinal));
    }
}
