using PEMS.Application.Accounts.Commands.CreateAccount;
using PEMS.Application.Accounts.Commands.ReplaceStaffLeader;
using PEMS.Application.Accounts.Commands.UpdateBasicAccountInfo;
using PEMS.Application.Accounts.Common;

namespace PEMS.UnitTests.Accounts.Common;

/// <summary>
/// Proves the three HO account validators enforce ONE shared identity rule set: the same payload is
/// accepted or rejected with the same message no matter which flow it arrives through
/// (PEMS_HO_ACCOUNT_IDENTITY_VALIDATION_IMPLEMENTATION_SPEC §10, AC-04/AC-10).
/// </summary>
public class AccountIdentityValidatorTests
{
    private static readonly CreateAccountCommandValidator Create = new();
    private static readonly UpdateBasicAccountInfoCommandValidator Update = new();
    private static readonly ReplaceStaffLeaderCommandValidator Replace = new();

    private static string? CreateError(string fullName, string email)
        => FirstError(Create.Validate(new CreateAccountCommand
        {
            FullName = fullName, Email = email, RoleCode = "HO", PrimaryCampusId = 1,
        }));

    private static string? UpdateError(string fullName, string email)
        => FirstError(Update.Validate(new UpdateBasicAccountInfoCommand
        {
            UserId = 810, FullName = fullName, Email = email,
        }));

    private static string? ReplaceCreateNewError(string fullName, string email)
        => FirstError(Replace.Validate(new ReplaceStaffLeaderCommand
        {
            CampusId = 1,
            Mode = ReplaceStaffLeaderModes.CreateNewUser,
            FullName = fullName,
            Email = email,
            Reason = "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế.",
        }));

    private static string? FirstError(FluentValidation.Results.ValidationResult result)
        => result.IsValid ? null : result.Errors[0].ErrorMessage;

    // ── The same value produces the same verdict in all three flows ───────────

    [Theory]
    [InlineData("Nguyễn Văn An", "an.nv@fpt.edu.vn", null)]
    [InlineData("O'Connor", "oconnor@gmail.com", null)]
    [InlineData("An", "an@gmail.com", null)]
    [InlineData("", "an@gmail.com", AccountIdentityRules.FullNameRequiredMessage)]
    [InlineData("A", "an@gmail.com", AccountIdentityRules.FullNameTooShortMessage)]
    [InlineData("Nguyễn Văn 123", "an@gmail.com", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("<script>alert(1)</script>", "an@gmail.com", AccountIdentityRules.FullNameInvalidCharsMessage)]
    [InlineData("Nguyễn Văn An", "", AccountIdentityRules.EmailRequiredMessage)]
    [InlineData("Nguyễn Văn An", "abc", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("Nguyễn Văn An", "abc..def@gmail.com", AccountIdentityRules.EmailFormatMessage)]
    [InlineData("Nguyễn Văn An", "an+test@gmail.com", AccountIdentityRules.EmailPlusNotAllowedMessage)]
    [InlineData("Nguyễn Văn An", "an@yahoo.com", AccountIdentityRules.EmailDomainNotAllowedMessage)]
    [InlineData("Nguyễn Văn An", "an@student.fpt.edu.vn", AccountIdentityRules.EmailDomainNotAllowedMessage)]
    // fe.edu.vn is no longer accepted — and it is refused identically in all three flows.
    [InlineData("Nguyễn Văn An", "an@fe.edu.vn", AccountIdentityRules.EmailDomainNotAllowedMessage)]
    [InlineData("Nguyễn Văn An", "an@gmail.com.vn", AccountIdentityRules.EmailDomainNotAllowedMessage)]
    public void AllThreeFlows_ShareTheSameIdentityVerdict(string fullName, string email, string? expected)
    {
        Assert.Equal(expected, CreateError(fullName, email));
        Assert.Equal(expected, UpdateError(fullName, email));
        Assert.Equal(expected, ReplaceCreateNewError(fullName, email));
    }

    [Fact]
    public void UppercaseEmail_IsAcceptedBecauseItNormalizes()
    {
        Assert.Null(CreateError("Nguyễn Văn An", "  AN.NV@FPT.EDU.VN  "));
        Assert.Null(UpdateError("Nguyễn Văn An", "  AN.NV@FPT.EDU.VN  "));
        Assert.Null(ReplaceCreateNewError("Nguyễn Văn An", "  AN.NV@FPT.EDU.VN  "));
    }

    // ── ReplaceStaffLeader: reason + mode-scoped identity rules (§6.3, AC-07/AC-08) ──

    [Theory]
    [InlineData("", AccountIdentityRules.ReasonRequiredMessage)]
    [InlineData("abc", AccountIdentityRules.ReasonTooShortMessage)]
    [InlineData("..............", AccountIdentityRules.ReasonNotMeaningfulMessage)]
    public void Replace_RejectsAWeakReason(string reason, string expected)
    {
        var result = Replace.Validate(new ReplaceStaffLeaderCommand
        {
            CampusId = 1,
            Mode = ReplaceStaffLeaderModes.ExistingUser,
            NewLeaderUserId = 900,
            Reason = reason,
        });
        Assert.Equal(expected, FirstError(result));
    }

    [Fact]
    public void Replace_RejectsAReasonOver500Characters()
    {
        var result = Replace.Validate(new ReplaceStaffLeaderCommand
        {
            CampusId = 1,
            Mode = ReplaceStaffLeaderModes.ExistingUser,
            NewLeaderUserId = 900,
            Reason = new string('a', 501),
        });
        Assert.Equal(AccountIdentityRules.ReasonTooLongMessage, FirstError(result));
    }

    /// <summary>AC-07 — in EXISTING_USER the hidden create-new inputs are never validated.</summary>
    [Fact]
    public void Replace_ExistingUserMode_IgnoresEmptyFullNameAndEmail()
    {
        var result = Replace.Validate(new ReplaceStaffLeaderCommand
        {
            CampusId = 1,
            Mode = ReplaceStaffLeaderModes.ExistingUser,
            NewLeaderUserId = 900,
            FullName = null,
            Email = null,
            Reason = "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế.",
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Replace_ExistingUserMode_StillRequiresACandidate()
    {
        var result = Replace.Validate(new ReplaceStaffLeaderCommand
        {
            CampusId = 1,
            Mode = ReplaceStaffLeaderModes.ExistingUser,
            NewLeaderUserId = null,
            Reason = "Điều chuyển nhân sự phụ trách Phòng Hợp tác Quốc tế.",
        });
        Assert.Equal("Vui lòng chọn nhân sự thay thế.", FirstError(result));
    }
}
