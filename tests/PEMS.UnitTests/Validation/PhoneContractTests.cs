using FluentValidation.TestHelper;
using PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Partners.Commands.CreatePartner;
using PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;
using PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;
using Xunit;

namespace PEMS.UnitTests.Validation;

/// <summary>
/// PEMS_FIELD_CONTRACT_VALIDATION_HISTORY_REMEDIATION_MASTER_PLAN Patch 3 (§8, P3.8) — every user-write
/// path that accepts a phone number now goes through the SAME canonical rule
/// (<c>PEMS.Shared.PhoneNumber</c> / <c>PhoneNumberRules.MustBeAPhoneNumber</c>) instead of
/// <c>MaximumLength</c> alone. Before this suite, a direct API call to Safe Edit, Manage Operational
/// Contact, Partner Contact create/update, the Partner initial-contact payload, Business Card OCR
/// confirmation or the legacy registrant-info endpoint could all persist a value like
/// "+821012340001123213sd" — 23 characters, all letters/digits, comfortably under every
/// <c>MaximumLength(50)</c> cap — because none of those validators checked phone SHAPE, only length.
///
/// Every validator below is exercised against the same matrix (P3.8): blank (optional fields only),
/// valid VN local, valid +84, valid non-VN international, letters, the exact regression value, and
/// too-short. Phone is optional everywhere covered here (no command in this list requires it), so blank
/// always passes and only a NON-BLANK malformed value is ever rejected.
/// </summary>
public class PhoneContractTests
{
    private const string ExactRegressionValue = "+821012340001123213sd";

    // ── Safe Edit — SubmitVisitSafeEditCommandValidator (the reported regression) ────────────────────

    private static readonly SubmitVisitSafeEditCommandValidator SafeEditValidator = new();

    private static SubmitVisitSafeEditCommand SafeEditCmd(string? phone) => new(
        1,
        new VisitRequestSafeEditDto(
            1,
            new SafeRegistrantPatchDto("Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "VN"),
            null));

    [Fact] // P3.8 — empty optional
    public void SafeEdit_blank_phone_passes()
        => SafeEditValidator.TestValidate(SafeEditCmd(null))
            .ShouldNotHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — valid VN local
    public void SafeEdit_valid_vn_local_passes()
        => SafeEditValidator.TestValidate(SafeEditCmd("0912345678"))
            .ShouldNotHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — valid +84
    public void SafeEdit_valid_e164_vn_passes()
        => SafeEditValidator.TestValidate(SafeEditCmd("+84912345678"))
            .ShouldNotHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — valid international non-VN
    public void SafeEdit_valid_international_passes()
        => SafeEditValidator.TestValidate(SafeEditCmd("+1 202 555 0134"))
            .ShouldNotHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — alphabetic chars
    public void SafeEdit_letters_only_is_invalid()
        => SafeEditValidator.TestValidate(SafeEditCmd("abcdefgh"))
            .ShouldHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — exact reported bad value
    public void SafeEdit_exact_regression_value_is_invalid()
        => SafeEditValidator.TestValidate(SafeEditCmd(ExactRegressionValue))
            .ShouldHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    [Fact] // P3.8 — too short
    public void SafeEdit_too_short_is_invalid()
        => SafeEditValidator.TestValidate(SafeEditCmd("+8412"))
            .ShouldHaveValidationErrorFor(x => x.Patch.Registrant!.Phone);

    // ── Manage Operational Contact — 4 validators share the same matrix ─────────────────────────────

    private static readonly ReplaceOperationalContactCommandValidator ReplaceValidator = new();
    private static readonly UpdateOperationalContactProfileCommandValidator UpdateProfileValidator = new();
    private static readonly SaveOperationalContactCommandValidator SaveValidator = new();
    private static readonly InitiateOperationalContactTransferCommandValidator TransferValidator = new();

    private static ReplaceOperationalContactCommand ReplaceCmd(string? phone)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "new-contact@example.com");

    private static UpdateOperationalContactProfileCommand UpdateProfileCmd(string? phone)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "contact@example.com");

    private static SaveOperationalContactCommand SaveCmd(string? phone)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "contact@example.com");

    private static InitiateOperationalContactTransferCommand TransferCmd(string? phone)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "successor@example.com", "Bàn giao");

    [Theory] // P3.8 — blank passes, letters/regression/too-short fail, both accepted shapes pass
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void Replace_phone_matrix(string? phone, bool valid)
    {
        var result = ReplaceValidator.TestValidate(ReplaceCmd(phone));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void UpdateProfile_phone_matrix(string? phone, bool valid)
    {
        var result = UpdateProfileValidator.TestValidate(UpdateProfileCmd(phone));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void Save_phone_matrix(string? phone, bool valid)
    {
        var result = SaveValidator.TestValidate(SaveCmd(phone));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void Transfer_phone_matrix(string? phone, bool valid)
    {
        var result = TransferValidator.TestValidate(TransferCmd(phone));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    // ── Partner Contact — create/update ──────────────────────────────────────────────────────────────

    private static readonly CreatePartnerContactCommandValidator CreateContactValidator = new();
    private static readonly UpdatePartnerContactCommandValidator UpdateContactValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void CreatePartnerContact_phone_matrix(string? phone, bool valid)
    {
        var cmd = new CreatePartnerContactCommand { PartnerId = 1, FullName = "Nguyễn Văn A", Phone = phone };
        var result = CreateContactValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void UpdatePartnerContact_phone_matrix(string? phone, bool valid)
    {
        var cmd = new UpdatePartnerContactCommand { PartnerId = 1, ContactId = 5, FullName = "Nguyễn Văn A", Phone = phone };
        var result = UpdateContactValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    // ── Partner create — InitialContact.Phone (previously had NO rule at all, not even length) ──────

    private static readonly CreatePartnerCommandValidator CreatePartnerValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void CreatePartner_initialContact_phone_matrix(string? phone, bool valid)
    {
        var cmd = new CreatePartnerCommand
        {
            Name = "Đối tác ABC",
            InitialContact = new CreatePartnerCommand.InitialContactPayload { FullName = "Nguyễn Văn A", Phone = phone },
        };
        var result = CreatePartnerValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.InitialContact!.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.InitialContact!.Phone);
    }

    // ── Business Card OCR confirmation ───────────────────────────────────────────────────────────────

    private static readonly ConfirmBusinessCardContactCommandValidator OcrValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void ConfirmBusinessCardContact_phone_matrix(string? phone, bool valid)
    {
        var cmd = new ConfirmBusinessCardContactCommand { OcrJobId = 1, PartnerId = 1, FullName = "Nguyễn Văn A", Phone = phone };
        var result = OcrValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    // ── Legacy registrant-info endpoint ──────────────────────────────────────────────────────────────

    private static readonly UpdateRegistrantInfoCommandValidator RegistrantInfoValidator = new();

    [Theory]
    [InlineData("", true)] // Phone is a non-nullable string on this legacy command — "" is its "blank"
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("+1 202 555 0134", true)]
    [InlineData("abcdefgh", false)]
    [InlineData(ExactRegressionValue, false)]
    [InlineData("+8412", false)]
    public void UpdateRegistrantInfo_phone_matrix(string phone, bool valid)
    {
        var cmd = new UpdateRegistrantInfoCommand(1, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", phone, "a@b.com");
        var result = RegistrantInfoValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Phone);
        else result.ShouldHaveValidationErrorFor(x => x.Phone);
    }
}
