using PEMS.Application.Campuses.Common;

namespace PEMS.UnitTests.Campuses;

/// <summary>
/// Unit tests for <see cref="CampusMasterRules"/> / <see cref="CampusNormalization"/> — the shared
/// master-data rules used by both UC-81 create and UC-85 update. Covers the normalization contract
/// (§3), campus code (§4), name (§5), city (§6), address (§7), phone (§8) and email (§9) from
/// PEMS_HO_CAMPUS_MASTER_DATA_VALIDATION_IMPLEMENTATION_SPEC §17.
/// Mirrors frontend <c>features/campus-management/__tests__/campusMasterValidation.test.ts</c> —
/// both sides must accept and reject exactly the same values, with the same messages.
/// </summary>
public class CampusMasterRulesTests
{
    // ── §3 Normalization ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("  hp  ", "HP")]
    [InlineData("fpt-hn", "FPT-HN")]
    [InlineData("HN", "HN")]
    [InlineData(null, "")]
    public void Code_TrimsAndUppercases_WithoutRewritingSeparators(string? input, string expected)
        => Assert.Equal(expected, CampusNormalization.Code(input));

    [Theory]
    [InlineData("  FPT   University   Hải Phòng ", "FPT University Hải Phòng")]
    [InlineData("FPT\tUniversity\nHà Nội", "FPT University Hà Nội")]
    [InlineData("fpt university", "fpt university")] // casing never rewritten
    [InlineData(null, "")]
    public void Text_CollapsesWhitespace_AndPreservesCasingAndDiacritics(string? input, string expected)
        => Assert.Equal(expected, CampusNormalization.Text(input));

    [Theory]
    [InlineData("  hà nội ", "Hà Nội")]   // mapped onto the canonical spelling
    [InlineData("Đà Nẵng", "Đà Nẵng")]
    [InlineData("  Vùng đất lạ  ", "Vùng đất lạ")] // outside the whitelist: trimmed, not rewritten
    public void City_MapsOntoCanonicalSpelling_AndLeavesUnknownValuesIntact(string input, string expected)
        => Assert.Equal(expected, CampusNormalization.City(input));

    [Theory]
    [InlineData("  HP@FPT.EDU.VN ", "hp@fpt.edu.vn")]
    [InlineData(null, "")]
    public void Email_TrimsAndLowercases(string? input, string expected)
        => Assert.Equal(expected, CampusNormalization.Email(input));

    [Fact]
    public void PhoneDisplay_CollapsesSpaces_ButKeepsTheUsersSeparators()
        => Assert.Equal("(024) 7300 5588", CampusNormalization.PhoneDisplay("(024)   7300  5588"));

    /// <summary>§3.6 / §8.5 — every spelling of the same number collapses to one canonical key.</summary>
    [Theory]
    [InlineData("024 7300 5588")]
    [InlineData("024-7300-5588")]
    [InlineData("(024) 7300.5588")]
    [InlineData("+84 24 7300 5588")]
    public void PhoneKey_TreatsEveryFormattingOfTheSameNumberAsEqual(string input)
        => Assert.Equal("02473005588", CampusNormalization.PhoneKey(input));

    // ── §4 Campus code ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HN")]
    [InlineData("HCM")]
    [InlineData("HP")]
    [InlineData("DN-2")]
    [InlineData("FPT_HN")]
    [InlineData("CAMPUS01")]
    [InlineData(" hp ")] // normalized to "HP" before validation
    public void ValidateCampusCode_AcceptsWellFormedCodes(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusCode(input));

    [Theory]
    [InlineData("", CampusMasterRules.CodeRequiredMessage)]
    [InlineData("   ", CampusMasterRules.CodeRequiredMessage)]
    [InlineData("H", CampusMasterRules.CodeTooShortMessage)]
    [InlineData("Hà Nội", CampusMasterRules.CodeInvalidCharsMessage)]
    [InlineData("H N", CampusMasterRules.CodeInvalidCharsMessage)]
    [InlineData("HN@01", CampusMasterRules.CodeInvalidCharsMessage)]
    [InlineData("-HN", CampusMasterRules.CodeSeparatorEdgeMessage)]
    [InlineData("HN-", CampusMasterRules.CodeSeparatorEdgeMessage)]
    [InlineData("_HN", CampusMasterRules.CodeSeparatorEdgeMessage)]
    [InlineData("HN_", CampusMasterRules.CodeSeparatorEdgeMessage)]
    [InlineData("HN__2", CampusMasterRules.CodeConsecutiveSeparatorMessage)]
    [InlineData("HN--2", CampusMasterRules.CodeConsecutiveSeparatorMessage)]
    [InlineData("HN-_2", CampusMasterRules.CodeConsecutiveSeparatorMessage)]
    public void ValidateCampusCode_RejectsMalformedCodes_WithTheSpecMessage(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusCode(input));

    [Fact]
    public void ValidateCampusCode_RejectsCodesOverTwentyCharacters()
        => Assert.Equal(
            CampusMasterRules.CodeTooLongMessage,
            CampusMasterRules.ValidateCampusCode(new string('A', 21)));

    // ── §5 Campus name ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("FPT University Hà Nội")]
    [InlineData("FPT University Hải Phòng")]
    [InlineData("FPT Campus 2")]
    [InlineData("FPT Education (Hòa Lạc)")]
    [InlineData("FPT Polytechnic - Đà Nẵng")]
    [InlineData("Đại học FPT, cơ sở Hòa Lạc")]
    public void ValidateCampusName_AcceptsRealCampusNames(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusName(input));

    [Theory]
    [InlineData("", CampusMasterRules.NameRequiredMessage)]
    [InlineData("A", CampusMasterRules.NameTooShortMessage)]
    [InlineData("12", CampusMasterRules.NameTooShortMessage)]
    [InlineData("123", CampusMasterRules.NameNotMeaningfulMessage)]
    [InlineData("...", CampusMasterRules.NameNotMeaningfulMessage)]
    [InlineData("<script>alert(1)</script>", CampusMasterRules.NameInvalidCharsMessage)]
    [InlineData("😊😊😊", CampusMasterRules.NameNotMeaningfulMessage)]
    public void ValidateCampusName_RejectsMeaninglessOrUnsafeNames(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusName(input));

    [Fact]
    public void ValidateCampusName_RejectsNamesOverOneHundredFiftyCharacters()
        => Assert.Equal(
            CampusMasterRules.NameTooLongMessage,
            CampusMasterRules.ValidateCampusName(new string('a', 151)));

    [Fact]
    public void ValidateCampusName_CollapsesSpacesBeforeMeasuringLength()
        => Assert.Null(CampusMasterRules.ValidateCampusName("  FPT   University   Hải Phòng "));

    // ── §6 City ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Hà Nội")]
    [InlineData("TP. Hồ Chí Minh")]
    [InlineData("hà nội")] // canonicalized before the whitelist check
    public void ValidateCampusCity_AcceptsWhitelistedProvinces(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusCity(input));

    [Theory]
    [InlineData("", CampusMasterRules.CityRequiredMessage)]
    [InlineData("   ", CampusMasterRules.CityRequiredMessage)]
    [InlineData("Hà Nội City", CampusMasterRules.CityNotAllowedMessage)]
    [InlineData("Bắc Giang", CampusMasterRules.CityNotAllowedMessage)] // merged away in 2025
    [InlineData("<script>", CampusMasterRules.CityNotAllowedMessage)]
    public void ValidateCampusCity_RejectsFreeText(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusCity(input));

    // ── §7 Address ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Khu Giáo dục và Đào tạo, Khu Công nghệ cao Hòa Lạc, Hà Nội")]
    [InlineData("Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Hồ Chí Minh")]
    [InlineData("25 Nguyễn Văn Linh, Hải Châu, Đà Nẵng")]
    [InlineData("Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội")]
    public void ValidateCampusAddress_AcceptsRealAddresses(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusAddress(input));

    [Theory]
    [InlineData("", CampusMasterRules.AddressRequiredMessage)]
    [InlineData("25sđ", CampusMasterRules.AddressTooShortMessage)]
    [InlineData("12345", CampusMasterRules.AddressNotMeaningfulMessage)]
    [InlineData(".....", CampusMasterRules.AddressNotMeaningfulMessage)]
    [InlineData("<script>", CampusMasterRules.AddressInvalidCharsMessage)]
    public void ValidateCampusAddress_RejectsMeaninglessOrUnsafeAddresses(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusAddress(input));

    [Fact]
    public void ValidateCampusAddress_RejectsAddressesOverTwoHundredFiftyFiveCharacters()
        => Assert.Equal(
            CampusMasterRules.AddressTooLongMessage,
            CampusMasterRules.ValidateCampusAddress(new string('a', 256)));

    [Fact]
    public void ValidateCampusAddress_DropsNewlinesViaNormalization()
        => Assert.Null(CampusMasterRules.ValidateCampusAddress("25 Nguyễn Văn Linh,\nĐà Nẵng"));

    // ── §8 Phone ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("024 7300 5588")]
    [InlineData("024-7300-5588")]
    [InlineData("(024) 7300 5588")]
    [InlineData("+84 24 7300 5588")]
    [InlineData("0918271611")]
    public void ValidateCampusPhone_AcceptsVietnameseNumbersInAnyCommonFormat(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusPhone(input));

    [Theory]
    [InlineData("", CampusMasterRules.PhoneRequiredMessage)]
    [InlineData("1234567", CampusMasterRules.PhoneDigitCountMessage)]           // 7 digits
    [InlineData("0123456789012345", CampusMasterRules.PhoneDigitCountMessage)]  // 16 digits
    [InlineData("024ABC5588", CampusMasterRules.PhoneFormatMessage)]
    [InlineData("024 7300 5588 ext 123", CampusMasterRules.PhoneFormatMessage)]
    [InlineData("84+2473005588", CampusMasterRules.PhonePlusPlacementMessage)]
    [InlineData("++84 24 7300 5588", CampusMasterRules.PhonePlusPlacementMessage)]
    [InlineData("+1 202 555 0173", CampusMasterRules.PhoneFormatMessage)]       // not a VN number
    [InlineData("1900 1234", CampusMasterRules.PhoneFormatMessage)]             // no leading 0 / +84
    public void ValidateCampusPhone_RejectsMalformedOrNonVietnameseNumbers(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusPhone(input));

    [Fact]
    public void ValidateCampusPhone_RejectsDisplayValuesOverThirtyCharacters()
        => Assert.Equal(
            CampusMasterRules.PhoneTooLongMessage,
            CampusMasterRules.ValidateCampusPhone(new string('0', 31)));

    // ── §9 Email ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("hn@fpt.edu.vn")]
    [InlineData("campus.hp@fpt.edu.vn")]
    [InlineData("contact.qn@fe.edu.vn")]
    [InlineData("  HP@FPT.EDU.VN ")] // normalized before validation
    public void ValidateCampusEmail_AcceptsInstitutionalAddresses(string input)
        => Assert.Null(CampusMasterRules.ValidateCampusEmail(input));

    [Theory]
    [InlineData("", CampusMasterRules.EmailRequiredMessage)]
    [InlineData("abc@gmail.com", CampusMasterRules.EmailDomainNotAllowedMessage)]
    [InlineData("abc@yahoo.com", CampusMasterRules.EmailDomainNotAllowedMessage)]
    [InlineData("abc@student.fpt.edu.vn", CampusMasterRules.EmailDomainNotAllowedMessage)]
    [InlineData("abc@fpt.edu.vn.fake.com", CampusMasterRules.EmailDomainNotAllowedMessage)]
    [InlineData("abc@fakefpt.edu.vn", CampusMasterRules.EmailDomainNotAllowedMessage)]
    [InlineData("abc+test@fpt.edu.vn", CampusMasterRules.EmailPlusNotAllowedMessage)]
    [InlineData("abc..def@fpt.edu.vn", CampusMasterRules.EmailFormatMessage)]
    [InlineData(".abc@fpt.edu.vn", CampusMasterRules.EmailFormatMessage)]
    [InlineData("abc.@fpt.edu.vn", CampusMasterRules.EmailFormatMessage)]
    [InlineData("abc@@fpt.edu.vn", CampusMasterRules.EmailFormatMessage)]
    [InlineData("abc fpt@fpt.edu.vn", CampusMasterRules.EmailFormatMessage)]
    public void ValidateCampusEmail_RejectsNonInstitutionalOrMalformedAddresses(string input, string expected)
        => Assert.Equal(expected, CampusMasterRules.ValidateCampusEmail(input));

    [Fact]
    public void ValidateCampusEmail_RejectsLocalPartOverSixtyFourCharacters()
        => Assert.Equal(
            CampusMasterRules.EmailLocalPartTooLongMessage,
            CampusMasterRules.ValidateCampusEmail($"{new string('a', 65)}@fpt.edu.vn"));

    [Fact]
    public void ValidateCampusEmail_RejectsAddressesOverOneHundredFiftyCharacters()
        => Assert.Equal(
            CampusMasterRules.EmailTooLongMessage,
            CampusMasterRules.ValidateCampusEmail($"{new string('a', 140)}@fpt.edu.vn"));

    /// <summary>§9.5 — the domain must be matched exactly, never with Contains/EndsWith.</summary>
    [Fact]
    public void HasAllowedCampusEmailDomain_IsAnExactMatch_NotASuffixCheck()
    {
        Assert.True(CampusMasterRules.HasAllowedCampusEmailDomain("hn@fpt.edu.vn"));
        Assert.False(CampusMasterRules.HasAllowedCampusEmailDomain("hn@sub.fpt.edu.vn"));
        Assert.False(CampusMasterRules.HasAllowedCampusEmailDomain("hn@xfpt.edu.vn"));
        Assert.False(CampusMasterRules.HasAllowedCampusEmailDomain("hn@fpt.edu.vn.evil.com"));
    }
}
