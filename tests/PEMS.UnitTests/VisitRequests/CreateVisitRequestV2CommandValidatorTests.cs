using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// The backend half of the v2 required-field contract. These matter more than the frontend ones:
/// the form could always be bypassed by calling the API directly, and several fields the UI
/// required were only length-checked here — so a hand-written request could store a visit with no
/// working content, an operational contact with no phone or email, or a support row that violates
/// a NOT NULL column. Every case below is one of those holes.
/// </summary>
public class CreateVisitRequestV2CommandValidatorTests
{
    private static readonly DateTime Start = DateTime.Now.AddDays(20);
    private readonly CreateVisitRequestV2CommandValidator _validator = new();

    private static VisitorDto Guest() => new("Khách 1", "VN", "GV", "ĐH X");

    /// <summary>
    /// A roster of DISTINCT people, for the tests that are about list SIZE.
    ///
    /// <para>They used to build N copies of <see cref="Guest"/>, which stopped being a delegation of
    /// N once the campus was checked for one person appearing twice (ID-02): 200 identical rows is
    /// one person entered 200 times, and that is refused on purpose. Numbering the names keeps the
    /// ceiling tests about the ceiling.</para>
    /// </summary>
    private static IList<VisitorDto> Roster(int count) =>
        Enumerable.Range(1, count).Select(i => new VisitorDto($"Khách {i}", "VN", "GV", "ĐH X")).ToList();

    private static CampusVisitFormDto Campus(
        string? workingContent = "Nội dung làm việc",
        ContactPointDto? opContact = null,
        IList<SupportTeamMemberDto>? support = null,
        IList<VisitorDto>? visitors = null,
        string mediaConsentStatus = "DECLINED",
        string? notes = null)
        => new(
            "HN", Start, Start.AddHours(2),
            "Đoàn A", "MEETING", null, "Trao đổi", workingContent,
            visitors ?? new List<VisitorDto> { Guest() },
            support ?? new List<SupportTeamMemberDto>(),
            opContact ?? new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", "+84911111111", "op@example.com"),
            "EN", null, mediaConsentStatus, notes, null);

    private static CreateVisitRequestV2Command Command(CampusVisitFormDto? campus = null,
        RegistrantInputV2? registrant = null)
        => new(new VisitRequestFormDataV2(
            "SUB-1",
            registrant ?? new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com"),
            null,
            new List<CampusVisitFormDto> { campus ?? Campus() }));

    private string[] ErrorsFor(CreateVisitRequestV2Command command)
        => _validator.Validate(command).Errors.Select(e => e.PropertyName).ToArray();

    [Fact]
    public void The_fully_populated_baseline_is_valid()
        => Assert.True(_validator.Validate(Command()).IsValid);

    // ── Fields the API used to accept blank ──────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Working_content_is_required(string? value)
        => Assert.Contains(ErrorsFor(Command(Campus(workingContent: value))),
            p => p.Contains("WorkingContent"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrant_job_title_is_required(string value)
        => Assert.Contains(
            ErrorsFor(Command(registrant: new RegistrantInputV2("Người ĐK", "VN", "ĐH X", value, "+84912345678", "reg@example.com"))),
            p => p.Contains("JobTitle"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrant_nationality_is_required(string value)
        => Assert.Contains(
            ErrorsFor(Command(registrant: new RegistrantInputV2("Người ĐK", value, "ĐH X", "TP", "+84912345678", "reg@example.com"))),
            p => p.Contains("Nationality"));

    [Theory]
    [InlineData("", "op@example.com")]
    [InlineData("   ", "op@example.com")]
    [InlineData("ĐH X", "")]
    [InlineData("ĐH X", "   ")]
    public void All_four_operational_contact_fields_are_required(string org, string email)
        => Assert.NotEmpty(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", org, "Trưởng phòng Hợp tác", "+84911111111", email)))));

    // ── Phone: the biggest bypass — the API only checked length ──────────────

    [Theory]
    [InlineData("0912345678")]
    [InlineData("+84912345678")]
    public void A_real_phone_number_is_accepted(string phone)
        => Assert.True(_validator.Validate(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", phone, "op@example.com")))).IsValid);

    [Theory]
    [InlineData("123")]
    [InlineData("090abc123")]
    public void A_value_that_is_not_a_phone_number_is_rejected(string phone)
        => Assert.Contains(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", phone, "op@example.com")))),
            p => p.Contains("Phone"));

    /// <summary>
    /// Blank is NOT a malformed number — the phone is optional on the operational contact, so leaving
    /// it out has to submit. <c>MustBeAPhoneNumber</c> passes blank on purpose and nothing chains
    /// <c>.NotEmpty()</c> before it; the column is nullable and its CHECK accepts NULL, so a blank
    /// normalizes away rather than becoming a constraint violation.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_phone_is_accepted_because_the_field_is_optional(string phone)
        => Assert.DoesNotContain(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", phone, "op@example.com")))),
            p => p.Contains("Phone"));

    /// <summary>
    /// The job title, unlike the phone, IS required — it is what tells a campus whether the person
    /// answering can decide anything. It was optional and the create form never asked, so the column
    /// was NULL on every request and every detail screen rendered a labelled blank.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_operational_contact_job_title_is_refused(string jobTitle)
        => Assert.Contains(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", jobTitle, "+84911111111", "op@example.com")))),
            p => p.Contains("JobTitle"));

    [Fact]
    public void A_fifty_character_string_is_no_longer_a_valid_phone_number()
    {
        // The old rule was MaximumLength(50), so this passed.
        var junk = new string('9', 50);
        Assert.Contains(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", junk, "op@example.com")))),
            p => p.Contains("Phone"));
    }

    // ── Guests: at least one per campus ──────────────────────────────────────
    // Unlike the support team, the delegation is the reason the campus is receiving anybody. The
    // form has always required one; the server only null-checked the list, so a direct call could
    // store a campus whose guest list was empty and the detail screen showed a delegation of nobody.
    // These run against the SHARED CampusVisitFormDtoValidator, which pending-edit and resubmit
    // reuse through ToFormDto — so one rule covers every write path.

    // `with` rather than the helper: the helper coalesces null to the default roster, which is the
    // whole point of a default — so a null has to be written onto the DTO itself.
    [Fact]
    public void A_null_guest_list_is_rejected()
        => Assert.Contains(
            ErrorsFor(Command(Campus() with { Visitors = null! })),
            p => p.Contains("Visitors"));

    [Fact]
    public void An_empty_guest_list_is_rejected()
        => Assert.Contains(
            ErrorsFor(Command(Campus(visitors: new List<VisitorDto>()))),
            p => p.Contains("Visitors"));

    [Fact]
    public void One_guest_is_enough()
        => Assert.True(_validator.Validate(Command(Campus(
            visitors: new List<VisitorDto> { Guest() }))).IsValid);

    [Fact]
    public void Exactly_the_two_hundred_guest_ceiling_is_accepted()
        => Assert.True(_validator.Validate(Command(Campus(visitors: Roster(200)))).IsValid);

    [Fact]
    public void More_than_two_hundred_guests_is_rejected()
        => Assert.Contains(
            ErrorsFor(Command(Campus(visitors: Roster(201)))),
            p => p.Contains("Visitors"));

    // ── Support members: optional list, mandatory columns ────────────────────

    [Fact]
    public void An_empty_support_list_is_fine()
        => Assert.True(_validator.Validate(Command(Campus(
            support: new List<SupportTeamMemberDto>()))).IsValid);

    [Fact]
    public void A_support_row_must_be_complete_because_the_columns_are_not_null()
    {
        var errors = ErrorsFor(Command(Campus(
            support: new List<SupportTeamMemberDto> { new("Hỗ trợ 1", "", "", "") })));
        Assert.Contains(errors, p => p.Contains("JobTitle"));
        Assert.Contains(errors, p => p.Contains("Organization"));
        Assert.Contains(errors, p => p.Contains("Nationality"));
    }

    [Fact]
    public void A_complete_support_row_is_accepted()
        => Assert.True(_validator.Validate(Command(Campus(
            support: new List<SupportTeamMemberDto> { new("Hỗ trợ 1", "VN", "CV", "ĐH X") }))).IsValid);

    // ── Length boundaries ────────────────────────────────────────────────────

    [Fact]
    public void Exactly_the_limit_is_accepted()
        => Assert.True(_validator.Validate(Command(Campus(
            workingContent: new string('x', 4000)))).IsValid);

    [Fact]
    public void One_character_over_the_limit_is_rejected()
        => Assert.Contains(ErrorsFor(Command(Campus(workingContent: new string('x', 4001)))),
            p => p.Contains("WorkingContent"));

    // ── "Ghi chú gửi FPTU" is independent of media consent ───────────────────
    // The note the media-consent field used to carry was conditional on AGREED: the form only
    // showed it then, and it read as a justification for the consent answer. `notes` replaces it
    // with the guest's own general remark about the campus, which has nothing to do with consent —
    // so all four combinations below are legitimate, and none of them may be refused.

    [Theory]
    [InlineData("AGREED", null)]
    [InlineData("DECLINED", null)]
    [InlineData("AGREED", "Đoàn có hai khách lớn tuổi, xin hỗ trợ xe điện.")]
    [InlineData("DECLINED", "Đoàn có hai khách lớn tuổi, xin hỗ trợ xe điện.")]
    public void Notes_and_media_consent_do_not_gate_each_other(string status, string? notes)
        => Assert.True(_validator.Validate(
            Command(Campus(mediaConsentStatus: status, notes: notes))).IsValid);

    [Fact]
    public void Notes_at_exactly_the_limit_is_accepted()
        => Assert.True(_validator.Validate(Command(Campus(notes: new string('n', 2000)))).IsValid);

    [Fact]
    public void Notes_one_character_over_the_limit_is_rejected()
        => Assert.Contains(ErrorsFor(Command(Campus(notes: new string('n', 2001)))),
            p => p.Contains("Notes"));

    [Fact]
    public void An_over_long_note_is_refused_in_Vietnamese_naming_the_field()
    {
        var messages = _validator.Validate(Command(Campus(notes: new string('n', 2001))))
            .Errors.Select(e => e.ErrorMessage).ToArray();
        Assert.Contains(messages, m => m.Contains("Ghi chú gửi FPTU", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Contains("The length of", StringComparison.OrdinalIgnoreCase));
    }
}
