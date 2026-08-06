using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Plan §25 — the boundary of every bounded field, and the schedule rules, on the SERVER side.
///
/// Two of these were genuinely unguarded before: <c>MediaConsentNote</c> had no length rule at all
/// (the form capped it at 2000, the API took anything, and the edit screen would then refuse to
/// save the value back), and no phone field had one, though every phone column is VARCHAR(50) —
/// so an over-long number failed as a MySQL truncation instead of as a message naming the field.
///
/// Each limit is checked from BOTH sides: the largest accepted value and the smallest rejected
/// one. A one-sided test would still pass if someone quietly widened the rule.
/// </summary>
public class VisitRequestV2LengthAndScheduleTests
{
    private static readonly DateTime Start = DateTime.Now.AddDays(20).Date.AddHours(9);
    private readonly CreateVisitRequestV2CommandValidator _validator = new();

    private static string Of(int length) => new('x', length);

    private static VisitorDto Guest(
        string? fullName = null, string? nationality = null, string? jobTitle = null, string? organization = null)
        => new(fullName ?? "Khách 1", nationality ?? "VN", jobTitle ?? "GV", organization ?? "ĐH X");

    private static ContactPointDto OpContact(
        string? fullName = null, string? organization = null, string? phone = null, string? email = null,
        string? jobTitle = null)
        => new(fullName ?? "ĐM CS", organization ?? "ĐH X", jobTitle ?? "Trưởng phòng Hợp tác",
            phone ?? "+84911111111", email ?? "op@example.com");

    private static CampusVisitFormDto Campus(
        DateTime? start = null,
        DateTime? end = null,
        string? delegationName = null,
        string? visitType = null,
        string? visitTypeOther = null,
        string? purpose = null,
        string? workingContent = null,
        string? transportationNote = null,
        string? mediaConsentNote = null,
        IList<VisitorDto>? visitors = null,
        IList<SupportTeamMemberDto>? support = null,
        ContactPointDto? opContact = null)
        => new(
            "HN", start ?? Start, end ?? (start ?? Start).AddHours(2),
            delegationName ?? "Đoàn A", visitType ?? "MEETING", visitTypeOther,
            purpose ?? "Trao đổi", workingContent ?? "Nội dung làm việc",
            visitors ?? new List<VisitorDto> { Guest() },
            support ?? new List<SupportTeamMemberDto>(),
            opContact ?? OpContact(),
            "EN", transportationNote, "DECLINED", mediaConsentNote, null);

    private static CreateVisitRequestV2Command Command(
        CampusVisitFormDto? campus = null,
        RegistrantInputV2? registrant = null)
        => new(new VisitRequestFormDataV2(
            "SUB-1",
            registrant ?? new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "+84912345678", "reg@example.com"),
            null,
            new List<CampusVisitFormDto> { campus ?? Campus() }));

    private string[] ErrorsFor(CreateVisitRequestV2Command command)
        => _validator.Validate(command).Errors.Select(e => e.PropertyName).ToArray();

    private string[] MessagesFor(CreateVisitRequestV2Command command)
        => _validator.Validate(command).Errors.Select(e => e.ErrorMessage).ToArray();

    private void AssertBoundary(int max, Func<string, CreateVisitRequestV2Command> build, string property)
    {
        Assert.DoesNotContain(ErrorsFor(build(Of(max))), p => p.Contains(property));

        var overErrors = ErrorsFor(build(Of(max + 1)));
        Assert.Contains(overErrors, p => p.Contains(property));
    }

    // ── Per-campus content ───────────────────────────────────────────────────

    [Fact]
    public void Delegation_name_is_bounded_at_200()
        => AssertBoundary(200, v => Command(Campus(delegationName: v)), "DelegationName");

    [Fact]
    public void Visit_type_other_is_bounded_at_200()
        => AssertBoundary(200, v => Command(Campus(visitType: "OTHER", visitTypeOther: v)), "VisitTypeOther");

    [Fact]
    public void Purpose_is_bounded_at_2000()
        => AssertBoundary(2000, v => Command(Campus(purpose: v)), "Purpose");

    [Fact]
    public void Working_content_is_bounded_at_4000()
        => AssertBoundary(4000, v => Command(Campus(workingContent: v)), "WorkingContent");

    [Fact]
    public void Transportation_note_is_bounded_at_2000()
        => AssertBoundary(2000, v => Command(Campus(transportationNote: v)), "TransportationNote");

    [Fact]
    public void Media_consent_note_is_bounded_at_2000()
        => AssertBoundary(2000, v => Command(Campus(mediaConsentNote: v)), "MediaConsentNote");

    // ── People ───────────────────────────────────────────────────────────────

    [Fact]
    public void Guest_full_name_is_bounded_at_150()
        => AssertBoundary(150, v => Command(Campus(visitors: new List<VisitorDto> { Guest(fullName: v) })), "FullName");

    [Fact]
    public void Guest_job_title_is_bounded_at_150()
        => AssertBoundary(150, v => Command(Campus(visitors: new List<VisitorDto> { Guest(jobTitle: v) })), "JobTitle");

    [Fact]
    public void Guest_organization_is_bounded_at_200()
        => AssertBoundary(200, v => Command(Campus(visitors: new List<VisitorDto> { Guest(organization: v) })), "Organization");

    [Fact]
    public void Guest_nationality_is_bounded_at_100()
        => AssertBoundary(100, v => Command(Campus(visitors: new List<VisitorDto> { Guest(nationality: v) })), "Nationality");

    [Fact]
    public void Support_member_fields_are_bounded_the_same_way_as_guests()
    {
        SupportTeamMemberDto Member(string? name = null, string? job = null, string? org = null, string? nat = null)
            => new(name ?? "Hỗ trợ", job ?? "CV", org ?? "ĐH X", nat ?? "VN");

        AssertBoundary(150, v => Command(Campus(support: new List<SupportTeamMemberDto> { Member(name: v) })), "FullName");
        AssertBoundary(150, v => Command(Campus(support: new List<SupportTeamMemberDto> { Member(job: v) })), "JobTitle");
        AssertBoundary(200, v => Command(Campus(support: new List<SupportTeamMemberDto> { Member(org: v) })), "Organization");
        AssertBoundary(100, v => Command(Campus(support: new List<SupportTeamMemberDto> { Member(nat: v) })), "Nationality");
    }

    // ── Registrant / contacts ────────────────────────────────────────────────

    [Fact]
    public void Registrant_fields_are_bounded_by_their_columns()
    {
        RegistrantInputV2 R(string? name = null, string? nat = null, string? org = null, string? job = null, string? email = null)
            => new(name ?? "Người ĐK", nat ?? "VN", org ?? "ĐH X", job ?? "TP", "+84912345678", email ?? "reg@example.com");

        AssertBoundary(150, v => Command(registrant: R(name: v)), "FullName");
        AssertBoundary(200, v => Command(registrant: R(org: v)), "Organization");
        AssertBoundary(150, v => Command(registrant: R(job: v)), "JobTitle");
        AssertBoundary(100, v => Command(registrant: R(nat: v)), "Nationality");
    }

    [Fact]
    public void An_over_long_phone_is_rejected_by_name_rather_than_by_the_column()
    {
        // registrant_phone and operational_contact_phone are both VARCHAR(50).
        var digits = "+84" + new string('9', 60);

        Assert.Contains(ErrorsFor(Command(
                registrant: new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", digits, "reg@example.com"))),
            p => p.Contains("Phone"));

        Assert.Contains(ErrorsFor(Command(Campus(opContact: OpContact(phone: digits)))),
            p => p.Contains("Phone"));
    }

    [Fact]
    public void An_over_long_email_is_rejected_at_150()
        => AssertBoundary(150,
            v => Command(Campus(opContact: OpContact(email: $"{new string('a', Math.Max(1, v.Length - 12))}@example.com"))),
            "Email");

    [Fact]
    public void Every_length_failure_says_so_in_Vietnamese()
    {
        // FluentValidation's untranslated default ("The length of ... must be ...") sitting next to
        // Vietnamese required-messages on the same field is what this rules out.
        var messages = MessagesFor(Command(Campus(
            delegationName: Of(201), purpose: Of(2001), mediaConsentNote: Of(2001))));

        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.DoesNotContain("The length of", m, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(messages, m => m.Contains("tối đa", StringComparison.Ordinal));
    }

    // ── Schedule (plan §25 items 6–10) ───────────────────────────────────────

    [Fact]
    public void An_end_at_or_before_the_start_is_refused()
    {
        Assert.False(_validator.Validate(Command(Campus(start: Start, end: Start))).IsValid);
        Assert.False(_validator.Validate(Command(Campus(start: Start, end: Start.AddMinutes(-1)))).IsValid);
    }

    [Fact]
    public void The_minimum_duration_is_thirty_minutes_to_the_minute()
    {
        Assert.False(_validator.Validate(Command(Campus(start: Start, end: Start.AddMinutes(29)))).IsValid);
        Assert.True(_validator.Validate(Command(Campus(start: Start, end: Start.AddMinutes(30)))).IsValid);
    }

    [Fact]
    public void A_visit_may_end_on_a_later_day()
    {
        // 22:00 → 01:00 the next day is a normal three-hour evening visit, not an error.
        var evening = Start.Date.AddHours(22);
        var result = _validator.Validate(Command(Campus(start: evening, end: evening.AddHours(3))));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void The_schedule_is_read_as_a_wall_clock_with_no_offset_applied()
    {
        // DateTime.Kind must not change the verdict: these values are Vietnam wall-clock, and the
        // validator does no conversion. If anything ever normalised them through UTC, an
        // Unspecified and a Local instant with the same components would disagree here.
        var unspecified = new DateTime(Start.Year, Start.Month, Start.Day, 9, 0, 0, DateTimeKind.Unspecified);
        var asLocal = DateTime.SpecifyKind(unspecified, DateTimeKind.Local);

        var a = _validator.Validate(Command(Campus(start: unspecified, end: unspecified.AddHours(2))));
        var b = _validator.Validate(Command(Campus(start: asLocal, end: asLocal.AddHours(2))));

        Assert.Equal(a.IsValid, b.IsValid);
        Assert.True(a.IsValid);
    }
}
