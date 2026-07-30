using PEMS.Application.Accounts.Common;

namespace PEMS.UnitTests.Accounts.Common;

/// <summary>
/// Unit tests for <see cref="AccountIdentityRules"/> — the shared full-name / email / reason rules
/// used by CreateAccount, UpdateBasicAccountInfo and ReplaceStaffLeader. Covers the normalization
/// contract (§3), the full-name rules (§4), the email rules (§5) and the replacement reason (§6.3.3)
/// from PEMS_HO_ACCOUNT_IDENTITY_VALIDATION_IMPLEMENTATION_SPEC.
/// </summary>
public class AccountIdentityRulesTests
{
    // ── §3.1 Full-name normalization ──────────────────────────────────────────

    [Theory]
    [InlineData("  Nguyễn   Văn   An  ", "Nguyễn Văn An")]
    [InlineData("Trần\tMinh\nAnh", "Trần Minh Anh")]
    [InlineData("O'Connor", "O'Connor")]
    [InlineData("Jean-Luc Picard", "Jean-Luc Picard")]
    [InlineData("nguyễn văn an", "nguyễn văn an")]   // casing never rewritten
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeFullName_CollapsesWhitespace_AndPreservesTheRest(string? input, string expected)
        => Assert.Equal(expected, AccountIdentityRules.NormalizeFullName(input));

    // ── §3.2 Email normalization ──────────────────────────────────────────────

    [Theory]
    [InlineData("  User.Name@FPT.EDU.VN  ", "user.name@fpt.edu.vn")]
    [InlineData("  USER@FPT.EDU.VN ", "user@fpt.edu.vn")]
    [InlineData(null, "")]
    public void NormalizeEmail_TrimsAndLowercases(string? input, string expected)
        => Assert.Equal(expected, AccountIdentityRules.NormalizeEmail(input));

    // ── §4 Full-name validation ───────────────────────────────────────────────

    [Theory]
    [InlineData("Nguyễn Văn An")]
    [InlineData("An")]
    [InlineData("Li")]
    [InlineData("Jean-Luc Picard")]
    [InlineData("O'Connor")]
    [InlineData("D’Arcy")]
    [InlineData("J. Smith")]
    [InlineData("Đỗ Thị Hồng")]
    [InlineData("  Nguyễn   Văn   An  ")]   // valid once normalized
    public void ValidateFullName_AcceptsRealNames(string value)
        => Assert.Null(AccountIdentityRules.ValidateFullName(value));

    [Theory]
    [InlineData("", AccountIdentityRules.FullNameRequiredMessage)]
    [InlineData("   ", AccountIdentityRules.FullNameRequiredMessage)]
    [InlineData("A", AccountIdentityRules.FullNameTooShortMessage)]
    [InlineData("Nguyễn Văn 123", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("<script>alert(1)</script>", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("😊 Nguyễn Văn An", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("...", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("---", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("@@@", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("abc@fpt.edu.vn", AccountIdentityRules.FullNameInvalidCharsMessage)]
    public void ValidateFullName_RejectsInvalidValues(string value, string expectedMessage)
        => Assert.Equal(expectedMessage, AccountIdentityRules.ValidateFullName(value));

    [Fact]
    public void ValidateFullName_RejectsOver150Characters()
    {
        var tooLong = new string('a', AccountIdentityRules.FullNameMaxLength + 1);
        Assert.Equal(AccountIdentityRules.FullNameTooLongMessage, AccountIdentityRules.ValidateFullName(tooLong));
    }

    [Fact]
    public void ValidateFullName_AcceptsExactly150Characters()
        => Assert.Null(AccountIdentityRules.ValidateFullName(new string('a', AccountIdentityRules.FullNameMaxLength)));

    // ── §5 Email validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("user@gmail.com")]
    [InlineData("user.name@gmail.com")]
    [InlineData("user_name-2@gmail.com")]
    [InlineData("user@fpt.edu.vn")]
    [InlineData("USER@FPT.EDU.VN")]          // normalized before checking
    [InlineData("  User@Gmail.Com  ")]
    public void ValidateEmail_AcceptsAllowedDomains(string value)
        => Assert.Null(AccountIdentityRules.ValidateEmail(value));

    [Fact]
    public void ValidateEmail_AllowsGmail()
        => Assert.Null(AccountIdentityRules.ValidateEmail("an.nguyen@gmail.com"));

    [Fact]
    public void ValidateEmail_AllowsFptEduVn()
        => Assert.Null(AccountIdentityRules.ValidateEmail("an.nguyen@fpt.edu.vn"));

    /// <summary>
    /// fe.edu.vn was an accepted login domain until the HO account rules were narrowed to two
    /// domains. It is now an ordinary rejected domain — no grandfathering anywhere in the stack.
    /// </summary>
    [Fact]
    public void ValidateEmail_RejectsFeEduVn()
        => Assert.Equal(
            AccountIdentityRules.EmailDomainNotAllowedMessage,
            AccountIdentityRules.ValidateEmail("an.nguyen@fe.edu.vn"));

    [Theory]
    [InlineData("a@sub.gmail.com")]
    [InlineData("a@student.fpt.edu.vn")]
    [InlineData("a@sub.fpt.edu.vn")]
    public void ValidateEmail_RejectsSubdomain(string value)
        => Assert.Equal(
            AccountIdentityRules.EmailDomainNotAllowedMessage, AccountIdentityRules.ValidateEmail(value));

    /// <summary>Domains that would slip past a naive EndsWith / StartsWith check.</summary>
    [Theory]
    [InlineData("a@gmail.com.vn")]
    [InlineData("a@fpt.edu.vn.evil.com")]
    [InlineData("a@fakefpt.edu.vn")]
    [InlineData("a@fakegmail.com")]
    [InlineData("a@edu.vn")]
    public void ValidateEmail_RejectsLookalikeDomain(string value)
        => Assert.Equal(
            AccountIdentityRules.EmailDomainNotAllowedMessage, AccountIdentityRules.ValidateEmail(value));

    [Fact]
    public void EmailDomainNotAllowedMessage_NamesBothAllowedDomains_AndNothingElse()
    {
        Assert.Equal("Chỉ chấp nhận @gmail.com và @fpt.edu.vn.", AccountIdentityRules.EmailDomainNotAllowedMessage);
        Assert.DoesNotContain("fe.edu.vn", AccountIdentityRules.EmailDomainNotAllowedMessage);
        Assert.Equal(new[] { "fpt.edu.vn", "gmail.com" }, AccountIdentityRules.AllowedEmailDomains.OrderBy(d => d, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("", AccountIdentityRules.EmailRequiredMessage)]
    [InlineData("   ", AccountIdentityRules.EmailRequiredMessage)]
    [InlineData("abc", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("abc@", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("abc..def@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData(".abc@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("abc.@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("abc @gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("a@b@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("Nguyen Van A <abc@gmail.com>", AccountIdentityRules.EmailFormatMessage)]
    public void ValidateEmail_RejectsMalformedAddresses(string value, string expectedMessage)
        => Assert.Equal(expectedMessage, AccountIdentityRules.ValidateEmail(value));

    [Theory]
    [InlineData("abc+test@gmail.com")]
    [InlineData("abc+test@fpt.edu.vn")]
    public void ValidateEmail_RejectsPlusAddressing(string value)
        => Assert.Equal(AccountIdentityRules.EmailPlusNotAllowedMessage, AccountIdentityRules.ValidateEmail(value));

    [Theory]
    [InlineData("abc@yahoo.com")]
    [InlineData("abc@outlook.com")]
    [InlineData("abc@fpt.com.vn")]
    [InlineData("abc@student.fpt.edu.vn")]   // subdomain — must NOT pass an endsWith-style check
    [InlineData("abc@gmail.com.example.com")]
    [InlineData("abc@fakefpt.edu.vn")]
    public void ValidateEmail_RejectsDomainsOutsideTheWhitelist(string value)
        => Assert.Equal(AccountIdentityRules.EmailDomainNotAllowedMessage, AccountIdentityRules.ValidateEmail(value));

    [Fact]
    public void ValidateEmail_RejectsLocalPartOver64Characters()
    {
        var email = new string('a', AccountIdentityRules.EmailLocalPartMaxLength + 1) + "@gmail.com";
        Assert.Equal(AccountIdentityRules.EmailLocalPartTooLongMessage, AccountIdentityRules.ValidateEmail(email));
    }

    [Fact]
    public void ValidateEmail_AcceptsLocalPartOfExactly64Characters()
    {
        var email = new string('a', AccountIdentityRules.EmailLocalPartMaxLength) + "@gmail.com";
        Assert.Null(AccountIdentityRules.ValidateEmail(email));
    }

    [Fact]
    public void ValidateEmail_RejectsTotalLengthOver150()
    {
        // 150 chars total is the cap; the local-part limit must not trigger first.
        var email = new string('a', 64) + "." + new string('b', 90) + "@gmail.com";
        Assert.True(email.Length > AccountIdentityRules.EmailMaxLength);
        Assert.Equal(AccountIdentityRules.EmailTooLongMessage, AccountIdentityRules.ValidateEmail(email));
    }

    [Fact]
    public void HasAllowedEmailDomain_ComparesTheDomainExactly()
    {
        Assert.True(AccountIdentityRules.HasAllowedEmailDomain("a@fpt.edu.vn"));
        Assert.False(AccountIdentityRules.HasAllowedEmailDomain("a@sub.fpt.edu.vn"));
        Assert.False(AccountIdentityRules.HasAllowedEmailDomain("a@fpt.edu.vn.evil.com"));
    }

    // ── §6.3.3 Replacement reason ─────────────────────────────────────────────

    [Fact]
    public void ValidateReplacementReason_AcceptsAMeaningfulReason()
        => Assert.Null(AccountIdentityRules.ValidateReplacementReason(
            "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế từ tháng 8/2026."));

    [Theory]
    [InlineData("", AccountIdentityRules.ReasonRequiredMessage)]
    [InlineData("   ", AccountIdentityRules.ReasonRequiredMessage)]
    [InlineData("abc", AccountIdentityRules.ReasonTooShortMessage)]
    [InlineData("-----", AccountIdentityRules.ReasonTooShortMessage)]
    [InlineData("..............", AccountIdentityRules.ReasonNotMeaningfulMessage)]
    [InlineData("--- --- --- ---", AccountIdentityRules.ReasonNotMeaningfulMessage)]
    public void ValidateReplacementReason_RejectsInvalidValues(string value, string expectedMessage)
        => Assert.Equal(expectedMessage, AccountIdentityRules.ValidateReplacementReason(value));

    [Fact]
    public void ValidateReplacementReason_RejectsOver500Characters()
        => Assert.Equal(
            AccountIdentityRules.ReasonTooLongMessage,
            AccountIdentityRules.ValidateReplacementReason(new string('a', AccountIdentityRules.ReplacementReasonMaxLength + 1)));
}
