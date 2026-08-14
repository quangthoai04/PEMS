using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;

namespace PEMS.UnitTests.Delegations;

/// <summary>
/// NP-03, boundary half — a payload must NAME exactly one of its own member rows as the campus's
/// operational contact, and it is told so before a transaction is opened.
///
/// <para>
/// The rule is enforced twice on purpose. Here it is payload SHAPE: cheap, and the answer to a client
/// that sent something impossible is "your payload is wrong", not a rolled-back write. Inside the
/// transaction <c>OperationalContactLink</c> checks the same thing against the rows it is actually
/// inserting, because that is the only place the real member set exists.
/// </para>
/// </summary>
public class OperationalContactMemberKeyValidationTests
{
    private static readonly CampusVisitFormDtoValidator Validator = new();

    private static VisitorDto Guest(string name, string? key) =>
        new(name, "VN", "Thành viên", "ABC University", null, key);

    private static SupportTeamMemberDto Support(string name, string? key) =>
        new(name, "Phiên dịch", "ABC University", "VN", null, key);

    private static CampusVisitFormDto Campus(
        IList<VisitorDto> visitors,
        IList<SupportTeamMemberDto>? support = null,
        string? contactKey = null)
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
            OperationalContactClientMemberKey: contactKey);

    private static IEnumerable<string> ErrorsFor(CampusVisitFormDto campus) =>
        Validator.Validate(campus).Errors.Select(e => e.ErrorMessage);

    [Fact]
    public void A_key_naming_one_guest_is_accepted()
    {
        var errors = ErrorsFor(Campus(new[] { Guest("Nguyễn Văn A", "k-a") }, contactKey: "k-a"));
        Assert.DoesNotContain(errors, m => m.Contains("đầu mối", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_key_naming_one_SUPPORT_member_is_accepted()
    {
        // Support staff travelling with the delegation are eligible; the validator must search both
        // lists or the picker would offer somebody the boundary then rejects.
        var errors = ErrorsFor(Campus(
            new[] { Guest("Nguyễn Văn A", "k-a") },
            new[] { Support("Lê Thị C", "k-c") },
            contactKey: "k-c"));

        Assert.DoesNotContain(errors, m => m.Contains("đầu mối", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_key_naming_nobody_is_refused()
    {
        // What deleting the contact's own row produces. Refused with the sentence that says what to
        // do about it, rather than accepted and silently dropped.
        var errors = ErrorsFor(Campus(new[] { Guest("Nguyễn Văn A", "k-a") }, contactKey: "k-gone"));

        Assert.Contains(errors, m => m.Contains("đầu mối của đoàn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Two_rows_under_one_key_are_refused()
    {
        // A repeated key is not an identity: the contact would resolve to whichever row happened to
        // be enumerated first, which is the silent mis-aiming this whole change removes.
        var errors = ErrorsFor(Campus(
            new[] { Guest("Nguyễn Văn A", "k-dup"), Guest("Trần Thị B", "k-dup") },
            contactKey: "k-dup"));

        Assert.Contains(errors, m => m.Contains("trùng nhau", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_key_repeated_across_the_guest_and_support_lists_is_refused()
    {
        var errors = ErrorsFor(Campus(
            new[] { Guest("Nguyễn Văn A", "k-same") },
            new[] { Support("Lê Thị C", "k-same") }));

        Assert.Contains(errors, m => m.Contains("trùng nhau", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_payload_that_names_no_contact_member_is_fine()
    {
        // Coordinating a visit you are not attending is ordinary, and an older client sends no keys
        // at all. Neither is an error.
        Assert.DoesNotContain(
            ErrorsFor(Campus(new[] { Guest("Nguyễn Văn A", "k-a") })),
            m => m.Contains("đầu mối", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            ErrorsFor(Campus(new[] { Guest("Nguyễn Văn A", null) })),
            m => m.Contains("đầu mối", StringComparison.OrdinalIgnoreCase)
                 || m.Contains("trùng nhau", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rows_that_all_send_NO_key_are_not_read_as_duplicates_of_each_other()
    {
        // "No identity" repeated is not "one identity twice" — an older client would otherwise be
        // refused for sending a perfectly ordinary two-guest delegation.
        var errors = ErrorsFor(Campus(new[] { Guest("A", null), Guest("B", null) }));

        Assert.DoesNotContain(errors, m => m.Contains("trùng nhau", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_edit_payload_carries_the_key_through_its_projection()
    {
        // Every edit path validates through CampusVisitEditV2Dto.ToFormDto(), so a key dropped in
        // that projection would leave pending-edit and resubmit unchecked — and, worse, would lose
        // the link on the copy-on-write rebuild.
        var edit = new CampusVisitEditV2Dto(
            VisitInstanceId: 10, ExpectedRowVersion: 3,
            CampusId: "HN",
            PlannedStartAt: new DateTime(2026, 9, 1, 9, 0, 0),
            PlannedEndAt: new DateTime(2026, 9, 1, 11, 0, 0),
            DelegationName: "Đoàn ABC", VisitType: "MEETING", VisitTypeOther: null,
            Purpose: "Tham quan", WorkingContent: "Trao đổi",
            Visitors: new List<VisitorDto> { Guest("Nguyễn Văn A", "k-a") },
            ExternalSupportMembers: new List<SupportTeamMemberDto>(),
            OperationalContact: new ContactPointDto("A", "ABC", "Trưởng đoàn", null, "a@abc.edu"),
            WorkingLanguage: "VI", TransportationNote: null, MediaConsentStatus: "AGREED", Notes: null,
            OperationalContactClientMemberKey: "k-a");

        Assert.Equal("k-a", edit.ToFormDto().OperationalContactClientMemberKey);
        Assert.Equal("k-a", edit.ToFormDto().Visitors[0].ClientMemberKey);
    }
}
