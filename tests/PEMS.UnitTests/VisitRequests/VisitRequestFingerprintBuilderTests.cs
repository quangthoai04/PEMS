using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands;
using PEMS.Application.Delegations.Services;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Unit tests for <see cref="VisitRequestFingerprintBuilder"/> (business_fingerprint v1).
/// The fingerprint is a pure function of the CORE visit identity: normalization must make
/// cosmetically-different inputs equal, soft content must not affect it, and every core
/// field change must produce a different hash.
/// </summary>
public class VisitRequestFingerprintBuilderTests
{
    private static readonly DateTime Start = new(2026, 8, 20, 9, 0, 0);
    private static readonly DateTime End = new(2026, 8, 20, 15, 0, 0);

    private static string Build(
        string registrantEmail = "Anna@Example.com",
        string contactEmail = "anna@example.com",
        string delegation = "Đoàn Đại học ABC",
        string scope = "SINGLE_CAMPUS",
        string visitType = "CAMPUS_TOUR",
        string? visitTypeOther = null,
        IEnumerable<(string, DateTime, DateTime)>? slots = null)
        => VisitRequestFingerprintBuilder.Build(
            registrantEmail, contactEmail, delegation, scope, visitType, visitTypeOther,
            slots ?? new[] { ("HN", Start, End) });

    [Fact]
    public void Fingerprint_Is64HexChars()
    {
        var fp = Build();
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), fp);
    }

    // ── §30.1: case/whitespace normalization → same fingerprint ───────────────────

    [Fact]
    public void EmailCase_And_Whitespace_DoNotChangeFingerprint()
    {
        var baseline = Build();
        var noisy = Build(
            registrantEmail: "  ANNA@example.COM ",
            contactEmail: " Anna@Example.com  ",
            delegation: "  đoàn   đại học   abc ");
        Assert.Equal(baseline, noisy);
    }

    [Fact]
    public void UnicodeNormalizationForm_DoesNotChangeFingerprint()
    {
        // "Đoàn" with the à precomposed (NFC) vs decomposed (NFD) must hash identically.
        var nfc = "Đoàn Đại học ABC".Normalize(System.Text.NormalizationForm.FormC);
        var nfd = "Đoàn Đại học ABC".Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal(Build(delegation: nfc), Build(delegation: nfd));
    }

    [Fact]
    public void VietnameseDiacritics_AreSignificant()
    {
        // Diacritics must NOT be stripped — different names are different delegations.
        Assert.NotEqual(Build(delegation: "Đoàn ABC"), Build(delegation: "Doan ABC"));
    }

    // ── §30.2: campus slot order does not matter ───────────────────────────────────

    [Fact]
    public void CampusSlotOrder_DoesNotChangeFingerprint()
    {
        var slotsA = new[] { ("HN", Start, End), ("HCM", Start.AddDays(1), End.AddDays(1)) };
        var slotsB = new[] { ("HCM", Start.AddDays(1), End.AddDays(1)), ("HN", Start, End) };
        Assert.Equal(
            Build(scope: "MULTI_CAMPUS", slots: slotsA),
            Build(scope: "MULTI_CAMPUS", slots: slotsB));
    }

    // ── §30.3: soft content (purpose/notes/guests/support/…) is excluded ───────────

    [Fact]
    public void SoftContent_DoesNotAffectFingerprint_ViaFormCommand()
    {
        var a = VisitRequestFingerprintBuilder.BuildFromForm(FakeForm(purpose: "Tham quan", notes: null));
        var b = VisitRequestFingerprintBuilder.BuildFromForm(FakeForm(
            purpose: "Một mục đích hoàn toàn khác",
            notes: "ghi chú thêm",
            workingContent: "nội dung làm việc khác",
            transportationNote: "xe 45 chỗ",
            mediaConsent: "AGREED",
            visitors: new List<VisitorDto> { new("Khách A", "Việt Nam", "GĐ", "Cty A") }));
        Assert.Equal(a, b);
    }

    // ── §30.4: every core field change produces a different fingerprint ────────────

    [Fact]
    public void RegistrantEmail_Changes_Fingerprint()
        => Assert.NotEqual(Build(), Build(registrantEmail: "other@example.com"));

    [Fact]
    public void ContactEmail_Changes_Fingerprint()
        => Assert.NotEqual(Build(), Build(contactEmail: "contact@example.com"));

    [Fact]
    public void DelegationName_Changes_Fingerprint()
        => Assert.NotEqual(Build(), Build(delegation: "Đoàn khác"));

    [Fact]
    public void VisitScope_Changes_Fingerprint()
        => Assert.NotEqual(Build(), Build(scope: "MULTI_CAMPUS"));

    [Fact]
    public void VisitType_Changes_Fingerprint()
        => Assert.NotEqual(Build(), Build(visitType: "MEETING"));

    [Fact]
    public void VisitTypeOther_OnlyCountsWhenOther()
    {
        // Non-OTHER type: visitTypeOther is ignored entirely.
        Assert.Equal(
            Build(visitType: "MEETING", visitTypeOther: null),
            Build(visitType: "MEETING", visitTypeOther: "bị bỏ qua"));

        // OTHER type: the normalized other-text is part of the identity.
        Assert.NotEqual(
            Build(visitType: "OTHER", visitTypeOther: "Hội thảo AI"),
            Build(visitType: "OTHER", visitTypeOther: "Hội thảo Blockchain"));
    }

    [Fact]
    public void CampusCode_Date_Time_Change_Fingerprint()
    {
        Assert.NotEqual(Build(), Build(slots: new[] { ("HCM", Start, End) }));
        Assert.NotEqual(Build(), Build(slots: new[] { ("HN", Start.AddDays(1), End.AddDays(1)) }));
        Assert.NotEqual(Build(), Build(slots: new[] { ("HN", Start, End.AddMinutes(30)) }));
    }

    // ── §30.5: wall-clock UTC+7 — no timezone conversion may shift the entered time ─

    [Fact]
    public void WallClockFormatting_KeepsEnteredDateAndTime()
    {
        Assert.Equal("2026-08-20T09:00", VisitRequestFingerprintBuilder.FormatWallClock(Start));
        // Seconds are truncated (fingerprint is per-minute).
        Assert.Equal("2026-08-20T09:00",
            VisitRequestFingerprintBuilder.FormatWallClock(new DateTime(2026, 8, 20, 9, 0, 45)));
    }

    [Fact]
    public void DateTimeKind_DoesNotChangeFingerprint()
    {
        // Same wall-clock instant tagged Unspecified vs Local vs Utc must hash identically —
        // the builder must never convert timezones.
        var unspecified = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified);
        var local = DateTime.SpecifyKind(Start, DateTimeKind.Local);
        var utc = DateTime.SpecifyKind(Start, DateTimeKind.Utc);
        Assert.Equal(
            Build(slots: new[] { ("HN", unspecified, End) }),
            Build(slots: new[] { ("HN", local, End) }));
        Assert.Equal(
            Build(slots: new[] { ("HN", unspecified, End) }),
            Build(slots: new[] { ("HN", utc, End) }));
    }

    // ── IsContactSelf uses the registrant email as effective contact ───────────────

    [Fact]
    public void IsContactSelf_UsesRegistrantEmail_AsEffectiveContact()
    {
        var self = VisitRequestFingerprintBuilder.BuildFromForm(
            FakeForm(isContactSelf: true, contactEmail: "ignored@example.com"));
        var explicitSame = VisitRequestFingerprintBuilder.BuildFromForm(
            FakeForm(isContactSelf: false, contactEmail: "anna@example.com"));
        Assert.Equal(self, explicitSame);
    }

    // ── Test double for the shared UC-17 form shape ────────────────────────────────

    private static IVisitRequestFormCommand FakeForm(
        string purpose = "Tham quan",
        string? notes = null,
        string? workingContent = null,
        string? transportationNote = null,
        string mediaConsent = "DECLINED",
        bool isContactSelf = false,
        string contactEmail = "anna@example.com",
        List<VisitorDto>? visitors = null)
        => new FakeVisitRequestForm(
            RegistrantFullName: "Anna Nguyễn",
            RegistrantNationality: "Việt Nam",
            RegistrantOrganization: "Cty ABC",
            RegistrantPosition: "Giám đốc",
            RegistrantPhone: "0912345678",
            RegistrantEmail: "anna@example.com",
            DelegationName: "Đoàn Đại học ABC",
            VisitScope: "SINGLE_CAMPUS",
            VisitType: "CAMPUS_TOUR",
            VisitTypeOther: null,
            CampusVisits: new List<VisitSlotDto> { new("HN", Start, End) },
            Purpose: purpose,
            WorkingContent: workingContent,
            Visitors: visitors ?? new List<VisitorDto>(),
            SupportMembers: new List<SupportTeamMemberDto>(),
            ContactPerson: new ContactPointDto("Anna Nguyễn", "Cty ABC", "0912345678", contactEmail),
            IsContactSelf: isContactSelf,
            WorkingLanguage: "VI",
            TransportationNote: transportationNote,
            MediaConsentStatus: mediaConsent,
            MediaConsentNote: null,
            PartnerId: null,
            Notes: notes);

    private sealed record FakeVisitRequestForm(
        string RegistrantFullName,
        string RegistrantNationality,
        string RegistrantOrganization,
        string RegistrantPosition,
        string RegistrantPhone,
        string RegistrantEmail,
        string DelegationName,
        string VisitScope,
        string VisitType,
        string? VisitTypeOther,
        IList<VisitSlotDto> CampusVisits,
        string Purpose,
        string? WorkingContent,
        IList<VisitorDto> Visitors,
        IList<SupportTeamMemberDto> SupportMembers,
        ContactPointDto ContactPerson,
        bool IsContactSelf,
        string WorkingLanguage,
        string? TransportationNote,
        string MediaConsentStatus,
        string? MediaConsentNote,
        ulong? PartnerId,
        string? Notes) : IVisitRequestFormCommand;
}
