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
/// The registrant-identity boundary for the authenticated v2 create (plan §5.2–§5.4).
///
/// Every negative case here is a security boundary, not a formatting nicety: passing a registrant email
/// the caller does not own is exactly the "create a request in somebody else's name, unverified" hole
/// these rules exist to close. The comparison is deliberately trim + lower-case ONLY — the tests pin that
/// Gmail dot/alias folding is NOT applied, because folding would let one mailbox claim another's identity.
/// </summary>
public class RegistrantIdentityRulesTests
{
    // ── Normalisation ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  Staff@FPT.EDU.VN  ", "staff@fpt.edu.vn")]
    [InlineData("staff@fpt.edu.vn", "staff@fpt.edu.vn")]
    [InlineData("\tSTAFF@FPT.EDU.VN\n", "staff@fpt.edu.vn")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_trims_and_lowercases_only(string? input, string expected)
        => Assert.Equal(expected, RegistrantIdentityRules.Normalize(input));

    [Theory]
    [InlineData("staff@fpt.edu.vn", "STAFF@FPT.EDU.VN")]      // case-insensitive
    [InlineData("staff@fpt.edu.vn", "  staff@fpt.edu.vn  ")]  // whitespace-insensitive
    [InlineData(" Staff@Fpt.Edu.Vn ", "sTAFF@fPT.eDU.vN")]    // both at once
    public void IsSameIdentity_matches_after_trim_and_lowercase(string actor, string form)
        => Assert.True(RegistrantIdentityRules.IsSameIdentity(actor, form));

    [Theory]
    [InlineData("first.last@gmail.com", "firstlast@gmail.com")]     // Gmail dots are NOT folded
    [InlineData("user@gmail.com", "user+visit@gmail.com")]          // +alias is NOT stripped
    [InlineData("user@gmail.com", "user@googlemail.com")]           // domain is NOT rewritten
    [InlineData("staff@fpt.edu.vn", "other@fpt.edu.vn")]
    public void IsSameIdentity_does_not_fold_addresses_that_are_different_mailboxes(string actor, string form)
        => Assert.False(RegistrantIdentityRules.IsSameIdentity(actor, form));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "someone@example.com")]
    public void IsSameIdentity_never_matches_when_either_side_is_blank(string? actor, string? form)
        => Assert.False(RegistrantIdentityRules.IsSameIdentity(actor, form));

    // ── Direct create must be self-registration ──────────────────────────────────

    [Theory]
    [InlineData("staff@fpt.edu.vn", "staff@fpt.edu.vn")]
    [InlineData("staff@fpt.edu.vn", "  STAFF@fpt.edu.vn ")]
    public void EnsureDirectCreateIsSelfRegistration_allows_the_callers_own_mailbox(string actor, string form)
    {
        var ex = Record.Exception(
            () => RegistrantIdentityRules.EnsureDirectCreateIsSelfRegistration(actor, form));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureDirectCreateIsSelfRegistration_rejects_another_persons_email_with_a_stable_code()
    {
        var ex = Assert.Throws<ConflictException>(
            () => RegistrantIdentityRules.EnsureDirectCreateIsSelfRegistration(
                "staff@fpt.edu.vn", "guest@partner.com"));

        Assert.Equal(VisitRequestErrorCodes.RegistrantEmailVerificationRequired, ex.ErrorCode);
    }

    [Fact]
    public void EnsureDirectCreateIsSelfRegistration_rejects_when_the_actor_has_no_email_on_record()
    {
        // A blank actor email must never be treated as "matches the blank form field".
        Assert.Throws<ConflictException>(
            () => RegistrantIdentityRules.EnsureDirectCreateIsSelfRegistration(null, ""));
    }

    // ── OTP-gated submissions carry no processing intent ─────────────────────────

    [Fact]
    public void EnsureNoDirectProcessingIntent_allows_a_form_with_no_intent_at_all()
    {
        var ex = Record.Exception(
            () => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(Form(Campus("HN", null))));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureNoDirectProcessingIntent_allows_explicit_send_for_review()
    {
        // SEND_FOR_REVIEW with no host IS the default routing — it asserts nothing the OTP flow disallows.
        var form = Form(Campus("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SendForReview, null)));

        Assert.Null(Record.Exception(() => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(form)));
    }

    [Fact]
    public void EnsureNoDirectProcessingIntent_rejects_self_host()
    {
        var form = Form(Campus("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null)));

        var ex = Assert.Throws<BusinessRuleException>(
            () => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(form));
        Assert.Equal(VisitRequestErrorCodes.InvalidCampusSubmissionMode, ex.ErrorCode);
    }

    [Fact]
    public void EnsureNoDirectProcessingIntent_rejects_assign_host()
    {
        var form = Form(Campus("HN", new CampusProcessingV2Dto(CampusSubmissionModes.AssignHost, 42)));

        Assert.Throws<BusinessRuleException>(
            () => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(form));
    }

    [Fact]
    public void EnsureNoDirectProcessingIntent_rejects_a_host_smuggled_under_send_for_review()
    {
        // The mode reads as the harmless default but a host id is attached — reject on the host alone.
        var form = Form(Campus("HN", new CampusProcessingV2Dto(CampusSubmissionModes.SendForReview, 42)));

        Assert.Throws<BusinessRuleException>(
            () => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(form));
    }

    [Fact]
    public void EnsureNoDirectProcessingIntent_rejects_when_only_a_LATER_campus_carries_the_intent()
    {
        var form = Form(
            Campus("HN", null),
            Campus("HCM", new CampusProcessingV2Dto(CampusSubmissionModes.SelfHost, null)));

        Assert.Throws<BusinessRuleException>(
            () => RegistrantIdentityRules.EnsureNoDirectProcessingIntent(form));
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string campusCode, CampusProcessingV2Dto? processing)
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
}
