using PEMS.Shared;
using Xunit;

namespace PEMS.UnitTests.Common;

/// <summary>
/// The backend nationality rule (Patch 4). Nationality has never had a format concept at all — every
/// write path only checked NotEmpty + MaximumLength(100) — so the audited data shows the same real
/// country stored under multiple spellings (e.g. "UAE" vs "Các Tiểu vương quốc Ả Rập Thống Nhất").
/// This resolver is the single backend authority for turning free text into the canonical Vietnamese
/// short name, or rejecting it.
/// </summary>
public class CountryNameTests
{
    [Theory]
    [InlineData("Hàn Quốc")]        // canonical Vietnamese short name (the PERSISTED form)
    [InlineData("South Korea")]     // English name
    [InlineData("Korea, Republic of")] // English official name
    [InlineData("KR")]              // ISO alpha-2, upper case
    [InlineData("kr")]              // ISO alpha-2, lower case
    [InlineData(" Hàn Quốc ")]      // surrounding whitespace is trimmed, not rejected
    [InlineData("HÀN QUỐC")]        // case-insensitive
    public void Resolves_every_real_spelling_of_the_same_country(string input)
    {
        Assert.True(CountryName.TryResolve(input, out var canonical));
        Assert.Equal("Hàn Quốc", canonical);
    }

    [Fact]
    public void The_documented_decision_example_resolves_exactly_as_specified()
    {
        // "Hàn Quốc" / "South Korea" / "KR" → resolve KR → persist "Hàn Quốc" (Patch 4 decision).
        Assert.True(CountryName.TryResolve("Hàn Quốc", out var a));
        Assert.True(CountryName.TryResolve("South Korea", out var b));
        Assert.True(CountryName.TryResolve("KR", out var c));
        Assert.Equal("Hàn Quốc", a);
        Assert.Equal("Hàn Quốc", b);
        Assert.Equal("Hàn Quốc", c);
    }

    [Theory]
    [InlineData("UAE")]
    [InlineData("Các Tiểu vương quốc Ả Rập Thống nhất")] // live pems_db casing (lowercase v/n)
    [InlineData("United Arab Emirates")]
    [InlineData("AE")]
    public void The_audited_UAE_alias_duplicate_resolves_to_one_country(string input)
    {
        Assert.True(CountryName.TryResolve(input, out var canonical));
        Assert.Equal("Các Tiểu Vương Quốc Ả Rập Thống Nhất", canonical);
    }

    // Every distinct raw value the P4.0 audit found in the real pems_db across visit_requests,
    // visit_guest_members, users and partners must resolve — Patch 4 must not make an existing,
    // legitimate value newly unwritable.
    [Theory]
    [InlineData("Hàn Quốc")]
    [InlineData("Việt Nam")]
    [InlineData("Singapore")]
    [InlineData("Nhật Bản")]
    [InlineData("Tây Ban Nha")]
    [InlineData("Bồ Đào Nha")]
    [InlineData("Morocco")]
    [InlineData("Afghanistan")]
    [InlineData("Ý")]
    [InlineData("Úc")]
    [InlineData("Pháp")]
    [InlineData("Egypt")]
    [InlineData("Đài Loan")]
    [InlineData("Hoa Kỳ")]
    [InlineData("Phần Lan")]
    [InlineData("Malaysia")]
    [InlineData("Đức")]
    [InlineData("Ba Lan")]
    [InlineData("Ireland")]
    [InlineData("Chile")]
    [InlineData("Ấn Độ")]
    [InlineData("Vương quốc Anh")]
    [InlineData("Indonesia")]
    [InlineData("Philippines")]
    [InlineData("Canada")]
    [InlineData("Nigeria")]
    [InlineData("Thái Lan")]
    public void Resolves_every_value_currently_stored_in_pems_db(string input)
        => Assert.True(CountryName.IsValid(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FPTU123")]
    [InlineData("abcxyzcountry")]
    [InlineData("Not A Country")]
    public void Rejects_arbitrary_text(string? input) => Assert.False(CountryName.IsValid(input));

    [Fact]
    public void An_unresolvable_value_yields_no_canonical_form()
    {
        Assert.False(CountryName.TryResolve("abcxyzcountry", out var canonical));
        Assert.Null(canonical);
    }
}
