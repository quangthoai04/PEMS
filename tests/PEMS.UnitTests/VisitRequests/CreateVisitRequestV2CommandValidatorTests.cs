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

    private static CampusVisitFormDto Campus(
        string? workingContent = "Nội dung làm việc",
        ContactPointDto? opContact = null,
        IList<SupportTeamMemberDto>? support = null)
        => new(
            "HN", Start, Start.AddHours(2),
            "Đoàn A", "MEETING", null, "Trao đổi", workingContent,
            new List<VisitorDto> { Guest() },
            support ?? new List<SupportTeamMemberDto>(),
            opContact ?? new ContactPointDto("ĐM CS", "ĐH X", "+84911111111", "op@example.com"),
            "EN", null, "DECLINED", null, null, null);

    private static CreateVisitRequestV2Command Command(CampusVisitFormDto? campus = null,
        RegistrantInputV2? registrant = null, ContactPointDto? primaryContact = null)
        => new(new VisitRequestFormDataV2(
            "SUB-1",
            registrant ?? new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com"),
            primaryContact ?? new ContactPointDto("ĐM", "ĐH X", "+84987654321", "contact@example.com"),
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
    [InlineData("")]
    [InlineData("   ")]
    public void Primary_contact_organization_is_required(string value)
        => Assert.Contains(
            ErrorsFor(Command(primaryContact: new ContactPointDto("ĐM", value, "+84987654321", "contact@example.com"))),
            p => p.Contains("Organization"));

    [Theory]
    [InlineData("", "op@example.com")]
    [InlineData("   ", "op@example.com")]
    [InlineData("ĐH X", "")]
    [InlineData("ĐH X", "   ")]
    public void All_four_operational_contact_fields_are_required(string org, string email)
        => Assert.NotEmpty(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", org, "+84911111111", email)))));

    // ── Phone: the biggest bypass — the API only checked length ──────────────

    [Theory]
    [InlineData("0912345678")]
    [InlineData("+84912345678")]
    public void A_real_phone_number_is_accepted(string phone)
        => Assert.True(_validator.Validate(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", phone, "op@example.com")))).IsValid);

    [Theory]
    [InlineData("123")]
    [InlineData("090abc123")]
    public void A_value_that_is_not_a_phone_number_is_rejected(string phone)
        => Assert.Contains(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", phone, "op@example.com")))),
            p => p.Contains("Phone"));

    /// <summary>
    /// Blank is NOT a malformed number — the phone is optional on every contact of a visit request, so
    /// leaving it out has to submit. <c>MustBeAPhoneNumber</c> passes blank on purpose and none of the
    /// three contact validators chains <c>.NotEmpty()</c> before it; a required field would say so
    /// there, in the validator, rather than here. Kept as its own case because the previous version of
    /// this test asserted the opposite and contradicted the shipped rule in both directions.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_phone_is_accepted_because_the_field_is_optional(string phone)
        => Assert.DoesNotContain(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", phone, "op@example.com")))),
            p => p.Contains("Phone"));

    [Fact]
    public void A_fifty_character_string_is_no_longer_a_valid_phone_number()
    {
        // The old rule was MaximumLength(50), so this passed.
        var junk = new string('9', 50);
        Assert.Contains(ErrorsFor(Command(Campus(
            opContact: new ContactPointDto("ĐM CS", "ĐH X", junk, "op@example.com")))),
            p => p.Contains("Phone"));
    }

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
}
