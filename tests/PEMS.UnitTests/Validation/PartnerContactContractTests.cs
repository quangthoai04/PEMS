using FluentValidation.TestHelper;
using PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;
using PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;
using PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;
using Xunit;

namespace PEMS.UnitTests.Validation;

/// <summary>
/// Plan CanhIter3FixBug ("Partner Contact / Business Card Data Capture") — the DELIBERATE opposite of
/// <c>PhoneContractTests</c>/<c>EmailContractTests</c> for exactly three validators
/// (<see cref="CreatePartnerContactCommandValidator"/>, <see cref="UpdatePartnerContactCommandValidator"/>,
/// <see cref="ConfirmBusinessCardContactCommandValidator"/>).
///
/// <para>
/// Partner Contact is external business-card/partner-supplied data — a foreign colleague's own printed
/// phone number, an email exactly as written on their card — never an authentication or identity field.
/// The Visit/Operational-Contact phone-shape rule (<c>PhoneNumberRules.MustBeAPhoneNumber</c>) and
/// FluentValidation's <c>EmailAddress()</c> both reject real, legitimate values a card can print: an
/// extension ("+1 (212) 555-1234 ext. 208"), a local format with no country code ("03-1234-5678"), or
/// non-ASCII/garbled OCR text the user is knowingly confirming as-is. Reported live: "ádsad" as a Phone
/// value was rejected with "Số điện thoại người liên hệ không hợp lệ." even though Phone has always been
/// optional here — the user typed SOMETHING, so the field could not simply be left blank to route around
/// the block. FullName stays required; PartnerId/ContactId stay real scope checks; only the
/// format-shape rule is gone. Length still bounds to the real DB column
/// (partner_contacts.phone VARCHAR(50), .email/.full_name/.job_title/.department_name VARCHAR(150)).
/// </para>
/// </summary>
public class PartnerContactContractTests
{
    // A representative spread of values the FORMER rules would have rejected but the real world produces.
    private const string ForeignWithDashesAndSpaces = "+82 10-1234-0001";
    private const string WithExtension = "+1 (212) 555-1234 ext. 208";
    private const string LocalNoCountryCode = "03-1234-5678";
    private const string WithTelPrefix = "Tel: +81 90 1234 5678";
    private const string WithOfficeExtensionMarker = "Office +44 (0)20 1234 5678 x204";
    private const string GarbledNonAscii = "ádsad";
    private const string LetterCode = "ABC-XYZ";
    private const string NonstandardEmail = "một giá trị user nhập"; // exactly what the bug report typed

    private static readonly CreatePartnerContactCommandValidator CreateValidator = new();
    private static readonly UpdatePartnerContactCommandValidator UpdateValidator = new();
    private static readonly ConfirmBusinessCardContactCommandValidator OcrValidator = new();

    private static CreatePartnerContactCommand CreateCmd(string? phone = null, string? email = null)
        => new() { PartnerId = 1, FullName = "Nguyễn Văn A", Phone = phone, Email = email };

    private static UpdatePartnerContactCommand UpdateCmd(string? phone = null, string? email = null)
        => new() { PartnerId = 1, ContactId = 5, FullName = "Nguyễn Văn A", Phone = phone, Email = email };

    private static ConfirmBusinessCardContactCommand OcrCmd(string? phone = null, string? email = null)
        => new() { OcrJobId = 1, PartnerId = 1, FullName = "Nguyễn Văn A", Phone = phone, Email = email };

    // ── Phone — format is never rejected on Create/Update/OCR confirm ──────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(ForeignWithDashesAndSpaces)]
    [InlineData(WithExtension)]
    [InlineData(LocalNoCountryCode)]
    [InlineData(WithTelPrefix)]
    [InlineData(WithOfficeExtensionMarker)]
    [InlineData(GarbledNonAscii)]
    [InlineData(LetterCode)]
    public void Create_phone_is_never_format_rejected(string? phone)
        => CreateValidator.TestValidate(CreateCmd(phone: phone)).ShouldNotHaveValidationErrorFor(x => x.Phone);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(ForeignWithDashesAndSpaces)]
    [InlineData(WithExtension)]
    [InlineData(LocalNoCountryCode)]
    [InlineData(WithTelPrefix)]
    [InlineData(WithOfficeExtensionMarker)]
    [InlineData(GarbledNonAscii)]
    [InlineData(LetterCode)]
    public void Update_phone_is_never_format_rejected(string? phone)
        => UpdateValidator.TestValidate(UpdateCmd(phone: phone)).ShouldNotHaveValidationErrorFor(x => x.Phone);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(ForeignWithDashesAndSpaces)]
    [InlineData(WithExtension)]
    [InlineData(LocalNoCountryCode)]
    [InlineData(WithTelPrefix)]
    [InlineData(WithOfficeExtensionMarker)]
    [InlineData(GarbledNonAscii)]
    [InlineData(LetterCode)]
    public void OcrConfirm_phone_is_never_format_rejected(string? phone)
        => OcrValidator.TestValidate(OcrCmd(phone: phone)).ShouldNotHaveValidationErrorFor(x => x.Phone);

    // ── Email — format is never rejected on Create/Update/OCR confirm ──────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(NonstandardEmail)]
    [InlineData("not-an-email-at-all")]
    [InlineData("missing-domain@")]
    public void Create_email_is_never_format_rejected(string? email)
        => CreateValidator.TestValidate(CreateCmd(email: email)).ShouldNotHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(NonstandardEmail)]
    [InlineData("not-an-email-at-all")]
    [InlineData("missing-domain@")]
    public void Update_email_is_never_format_rejected(string? email)
        => UpdateValidator.TestValidate(UpdateCmd(email: email)).ShouldNotHaveValidationErrorFor(x => x.Email);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(NonstandardEmail)]
    [InlineData("not-an-email-at-all")]
    [InlineData("missing-domain@")]
    public void OcrConfirm_email_is_never_format_rejected(string? email)
        => OcrValidator.TestValidate(OcrCmd(email: email)).ShouldNotHaveValidationErrorFor(x => x.Email);

    // ── Still-real rules: FullName required, PartnerId/ContactId/OcrJobId scope, length bounds ──────

    [Fact]
    public void Create_blank_fullname_is_still_rejected()
        => CreateValidator.TestValidate(new CreatePartnerContactCommand { PartnerId = 1, FullName = "  " })
            .ShouldHaveValidationErrorFor(x => x.FullName);

    [Fact]
    public void Update_blank_fullname_is_still_rejected()
        => UpdateValidator.TestValidate(new UpdatePartnerContactCommand { PartnerId = 1, ContactId = 5, FullName = "" })
            .ShouldHaveValidationErrorFor(x => x.FullName);

    [Fact]
    public void OcrConfirm_blank_fullname_is_still_rejected()
        => OcrValidator.TestValidate(new ConfirmBusinessCardContactCommand { OcrJobId = 1, PartnerId = 1, FullName = "" })
            .ShouldHaveValidationErrorFor(x => x.FullName);

    [Fact]
    public void Create_zero_partnerId_is_still_rejected()
        => CreateValidator.TestValidate(new CreatePartnerContactCommand { PartnerId = 0, FullName = "A" })
            .ShouldHaveValidationErrorFor(x => x.PartnerId);

    [Fact]
    public void OcrConfirm_zero_ocrJobId_is_still_rejected()
        => OcrValidator.TestValidate(new ConfirmBusinessCardContactCommand { OcrJobId = 0, PartnerId = 1, FullName = "A" })
            .ShouldHaveValidationErrorFor(x => x.OcrJobId);

    // Overlong values still hit the real DB-column length (VARCHAR(50) phone, VARCHAR(150) email) —
    // "arbitrary format" was never "arbitrary length".

    [Fact]
    public void Create_overlong_phone_is_rejected_by_length_not_format()
        => CreateValidator.TestValidate(CreateCmd(phone: new string('1', 51)))
            .ShouldHaveValidationErrorFor(x => x.Phone);

    [Fact]
    public void Create_overlong_email_is_rejected_by_length_not_format()
        => CreateValidator.TestValidate(CreateCmd(email: new string('a', 151) + "@x.com"))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Update_overlong_phone_is_rejected_by_length_not_format()
        => UpdateValidator.TestValidate(UpdateCmd(phone: new string('1', 51)))
            .ShouldHaveValidationErrorFor(x => x.Phone);

    [Fact]
    public void OcrConfirm_overlong_phone_is_rejected_by_length_not_format()
        => OcrValidator.TestValidate(OcrCmd(phone: new string('1', 51)))
            .ShouldHaveValidationErrorFor(x => x.Phone);

    // Phone at exactly the 50-char boundary still passes — proves the length rule is the real DB limit,
    // not an accidental off-by-one that would also reject legitimate long international numbers.
    [Fact]
    public void Create_phone_at_exact_length_boundary_passes()
        => CreateValidator.TestValidate(CreateCmd(phone: new string('1', 50)))
            .ShouldNotHaveValidationErrorFor(x => x.Phone);
}
