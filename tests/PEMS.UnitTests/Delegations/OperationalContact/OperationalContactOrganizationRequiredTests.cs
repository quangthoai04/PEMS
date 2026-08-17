using FluentValidation.TestHelper;
using PEMS.Application.Delegations.Commands.OperationalContact;
using Xunit;

namespace PEMS.UnitTests.Delegations.OperationalContact;

/// <summary>
/// Organization is REQUIRED on every write path that mutates a campus's operational-contact snapshot,
/// matching Create/Pending Edit/Resubmit's <c>OperationalContactV2Validator</c> — not just FullName/
/// JobTitle/Email. Before this suite, <c>Save</c>/<c>UpdateProfile</c>/<c>Replace</c>/<c>Transfer</c> only
/// capped Organization's length, so "Manage the contact role" could blank an organization Create never
/// allowed empty in the first place.
/// </summary>
public class OperationalContactOrganizationRequiredTests
{
    private const ulong RequestId = 1;
    private const ulong InstanceId = 10;
    private const string Name200 = // exactly 200 chars
        "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGGHHHHHHHHHHIIIIIIIIIIJJJJJJJJJJ" +
        "AAAAAAAAAABBBBBBBBBBCCCCCCCCCCDDDDDDDDDDEEEEEEEEEEFFFFFFFFFFGGGGGGGGGGHHHHHHHHHHIIIIIIIIIIJJJJJJJJJJ";
    private const string Name201 = Name200 + "X";

    // ── SaveOperationalContactCommandValidator ──────────────────────────────────────────────────────

    private static readonly SaveOperationalContactCommandValidator SaveValidator = new();

    private static SaveOperationalContactCommand SaveCmd(string? organization)
        => new(RequestId, InstanceId, "Nguyễn Văn A", organization, "Trưởng phòng", "+84912345678",
            "contact@example.com");

    [Fact] // ORG-01
    public void Save_null_organization_is_invalid()
        => SaveValidator.TestValidate(SaveCmd(null)).ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact] // ORG-02
    public void Save_empty_organization_is_invalid()
        => SaveValidator.TestValidate(SaveCmd("")).ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact] // ORG-03
    public void Save_whitespace_only_organization_is_invalid()
        => SaveValidator.TestValidate(SaveCmd("   ")).ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact] // ORG-04
    public void Save_valid_organization_passes()
        => SaveValidator.TestValidate(SaveCmd("Đơn vị ABC")).ShouldNotHaveValidationErrorFor(x => x.Organization);

    [Fact] // ORG-08
    public void Save_organization_at_200_chars_passes()
        => SaveValidator.TestValidate(SaveCmd(Name200)).ShouldNotHaveValidationErrorFor(x => x.Organization);

    [Fact] // ORG-09
    public void Save_organization_at_201_chars_is_invalid()
        => SaveValidator.TestValidate(SaveCmd(Name201)).ShouldHaveValidationErrorFor(x => x.Organization);

    // ── UpdateOperationalContactProfileCommandValidator (ORG-05) ────────────────────────────────────

    private static readonly UpdateOperationalContactProfileCommandValidator UpdateProfileValidator = new();

    private static UpdateOperationalContactProfileCommand UpdateProfileCmd(string? organization)
        => new(RequestId, InstanceId, "Nguyễn Văn A", organization, "Trưởng phòng", "+84912345678",
            "contact@example.com");

    [Fact]
    public void UpdateProfile_blank_organization_is_invalid()
        => UpdateProfileValidator.TestValidate(UpdateProfileCmd("   "))
            .ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact]
    public void UpdateProfile_valid_organization_passes()
        => UpdateProfileValidator.TestValidate(UpdateProfileCmd("Đơn vị ABC"))
            .ShouldNotHaveValidationErrorFor(x => x.Organization);

    // ── ReplaceOperationalContactCommandValidator (ORG-06) ───────────────────────────────────────────

    private static readonly ReplaceOperationalContactCommandValidator ReplaceValidator = new();

    private static ReplaceOperationalContactCommand ReplaceCmd(string? organization)
        => new(RequestId, InstanceId, "Nguyễn Văn A", organization, "Trưởng phòng", "+84912345678",
            "new-contact@example.com");

    [Fact]
    public void Replace_blank_organization_is_invalid()
        => ReplaceValidator.TestValidate(ReplaceCmd("")).ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact]
    public void Replace_valid_organization_passes()
        => ReplaceValidator.TestValidate(ReplaceCmd("Đơn vị ABC")).ShouldNotHaveValidationErrorFor(x => x.Organization);

    // ── InitiateOperationalContactTransferCommandValidator (ORG-07) ─────────────────────────────────

    private static readonly InitiateOperationalContactTransferCommandValidator TransferValidator = new();

    private static InitiateOperationalContactTransferCommand TransferCmd(string? organization)
        => new(RequestId, InstanceId, "Nguyễn Văn A", organization, "Trưởng phòng", "+84912345678",
            "successor@example.com", "Bàn giao cuối kỳ");

    [Fact]
    public void Transfer_blank_organization_is_invalid()
        => TransferValidator.TestValidate(TransferCmd(null)).ShouldHaveValidationErrorFor(x => x.Organization);

    [Fact]
    public void Transfer_valid_organization_passes()
        => TransferValidator.TestValidate(TransferCmd("Đơn vị ABC"))
            .ShouldNotHaveValidationErrorFor(x => x.Organization);
}
