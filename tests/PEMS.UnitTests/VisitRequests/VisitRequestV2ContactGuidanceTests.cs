using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Validation;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VerifyAndCreateVisitRequestV2;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Plan §23 items 2–5 — what the API says about a phone number, how long an organization may be,
/// and what the public create response is allowed to contain.
///
/// The phone rule itself was already right on both sides; the MESSAGE was not. "Số điện thoại không
/// hợp lệ" says nothing about which of the three phones on a visit request is wrong, nor what a
/// valid one looks like, and a caller with no UI in front of them has nothing else to go on.
/// </summary>
public class VisitRequestV2ContactGuidanceTests
{
    private static readonly DateTime Start = DateTime.Now.AddDays(20).Date.AddHours(9);
    private readonly CreateVisitRequestV2CommandValidator _validator = new();

    private static CampusVisitFormDto Campus(ContactPointDto? opContact = null, string? organization = null)
        => new(
            "HN", Start, Start.AddHours(2), "Đoàn A", "MEETING", null, "Trao đổi", "Nội dung làm việc",
            new List<VisitorDto> { new("Khách 1", "VN", "GV", organization ?? "ĐH X") },
            new List<SupportTeamMemberDto>(),
            opContact ?? new ContactPointDto("ĐM CS", organization ?? "ĐH X", "Trưởng phòng Hợp tác", "+84911111111", "op@example.com"),
            "EN", null, "DECLINED", null, null);

    private static CreateVisitRequestV2Command Command(
        CampusVisitFormDto? campus = null,
        string registrantPhone = "+84912345678",
        string primaryContactPhone = "+84987654321")
        => new(new VisitRequestFormDataV2(
            "SUB-1",
            new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", registrantPhone, "reg@example.com"),
            null,
            new List<CampusVisitFormDto> { campus ?? Campus() }));

    private string[] MessagesFor(CreateVisitRequestV2Command command)
        => _validator.Validate(command).Errors.Select(e => e.ErrorMessage).ToArray();

    private bool IsValid(CreateVisitRequestV2Command command) => _validator.Validate(command).IsValid;

    // ── The accepted shapes, and the message that states them ────────────────

    [Theory]
    [InlineData("0912345678")]        // national Vietnamese
    [InlineData("+84912345678")]      // full international
    [InlineData("+821012340001")]     // another country, same rule
    public void Accepted_phone_shapes_match_the_frontend_rule(string phone)
        => Assert.True(IsValid(Command(registrantPhone: phone)));

    [Theory]
    [InlineData("090abc123")]             // letters
    [InlineData("123")]                   // too short to exist
    [InlineData("+84912345678 ext 12")]   // an extension has no place in a stored contact number
    [InlineData("+9999999999999")]        // a country code the metadata does not know
    public void Rejected_phone_shapes_match_the_frontend_rule(string phone)
        => Assert.False(IsValid(Command(registrantPhone: phone)));

    [Fact]
    public void Every_phone_rejection_names_its_own_field()
    {
        // Two phones on one form — the registrant’s and the campus’s operational contact. A shared
        // "invalid phone number" would leave the caller guessing which of them the server means.
        var messages = MessagesFor(new CreateVisitRequestV2Command(new VisitRequestFormDataV2(
            "SUB-1",
            new RegistrantInputV2("Người ĐK", "VN", "ĐH X", "TP", "abc", "reg@example.com"),
            null,
            new List<CampusVisitFormDto>
            {
                Campus(new ContactPointDto("ĐM CS", "ĐH X", "Trưởng phòng Hợp tác", "abc", "op@example.com")),
            })));

        Assert.Contains(messages, m => m.Contains("Số điện thoại người đăng ký"));
        Assert.Contains(messages, m => m.Contains("Số điện thoại đầu mối phối hợp"));
        // And nothing still speaks of a request-level contact: that field is gone, so a message
        // naming it would be describing an input the caller can no longer send.
        Assert.DoesNotContain(messages, m => m.Contains("Số điện thoại đầu mối liên hệ"));
    }

    [Fact]
    public void Every_phone_rejection_states_the_accepted_formats()
    {
        var messages = MessagesFor(Command(registrantPhone: "abc"))
            .Where(m => m.Contains("Số điện thoại người đăng ký"))
            .ToArray();

        var message = Assert.Single(messages);
        // Verbatim the same guidance the form shows, so the two sides cannot describe one rule
        // in two different ways.
        Assert.Contains(PhoneNumberRules.FormatHint, message);
        Assert.Contains("0912345678", message);
        Assert.Contains("+84912345678", message);
        Assert.Contains("máy lẻ", message);
    }

    // ── Operational contact organization ─────────────────────────────────────

    [Fact]
    public void Operational_organization_is_bounded_at_the_same_200_as_the_form()
    {
        var at = Command(Campus(new ContactPointDto("ĐM CS", new string('x', 200), "Trưởng phòng Hợp tác", "+84911111111", "op@example.com")));
        var over = Command(Campus(new ContactPointDto("ĐM CS", new string('x', 201), "Trưởng phòng Hợp tác", "+84911111111", "op@example.com")));

        Assert.True(IsValid(at));
        Assert.False(IsValid(over));
        Assert.Contains(MessagesFor(over), m => m.Contains("Đơn vị công tác đầu mối phối hợp"));
    }

    [Fact]
    public void Operational_organization_accepts_free_text_as_well_as_a_known_organization()
    {
        // The combobox may offer organizations already on file, but this column is a SNAPSHOT with
        // no relation to a partner record — a name nobody has typed before is perfectly valid.
        Assert.True(IsValid(Command(Campus(
            new ContactPointDto("ĐM CS", "Một Tổ Chức Chưa Từng Có", "Trưởng phòng Hợp tác", "+84911111111", "op@example.com")))));
    }

    [Fact]
    public void Picking_an_operational_organization_cannot_carry_a_partner_selection()
    {
        // The per-campus contact has nowhere to put a partnerId: the DTO simply has no such field.
        // This is the structural reason the combobox cannot disturb the request's own partner link.
        var properties = typeof(ContactPointDto).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("PartnerId", properties);
        Assert.Equal(
            new[] { "FullName", "Organization", "Phone", "Email", "JobTitle" }.OrderBy(x => x),
            properties.OrderBy(x => x));
    }

    // ── What the public create response may contain (plan §23.2) ─────────────

    [Fact]
    public void Public_create_response_carries_the_receipt_and_no_personal_data()
    {
        var response = new VerifyAndCreateVisitRequestV2Response(
            VisitRequestId: 42,
            RequestCode: "VR2026072629B9DFF",
            VisitScope: "SINGLE_CAMPUS",
            HasMixedCampusDetails: false,
            PendingContactConfirmations: 0,
            Instances: new List<CreateVisitRequestV2CampusRef>
            {
                new(11, 1, "WAITING_REQUEST_APPROVAL"),
            },
            Idempotent: false,
            Message: "created",
            Status: "PENDING_APPROVAL",
            SubmittedAt: "2026-07-26T14:30:00",
            CampusCount: 1);

        var json = JsonSerializer.Serialize(response);

        // The receipt the screen is built from.
        Assert.Contains("\"RequestCode\":\"VR2026072629B9DFF\"", json);
        Assert.Contains("\"Status\":\"PENDING_APPROVAL\"", json);
        Assert.Contains("\"SubmittedAt\":\"2026-07-26T14:30:00\"", json);
        Assert.Contains("\"CampusCount\":1", json);

        // …and nothing beyond it. This endpoint is reachable without a session; anything personal in
        // the body would be readable by whoever holds the OTP, not only by the person it describes.
        foreach (var field in new[] { "Email", "Phone", "FullName", "Registrant", "Contact\"", "Organization" })
        {
            Assert.DoesNotContain(field, json, StringComparison.Ordinal);
        }
    }
}
