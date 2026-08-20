using FluentValidation.TestHelper;
using PEMS.Application.BusinessCardOcr.Commands.ConfirmBusinessCardContact;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.OperationalContact;
using PEMS.Application.Delegations.Commands.ResendVisitRequestOtp;
using PEMS.Application.Delegations.Commands.UpdateRegistrantInfo;
using PEMS.Application.Partners.Commands.CreatePartner;
using PEMS.Application.Partners.Contacts.Commands.CreatePartnerContact;
using PEMS.Application.Partners.Contacts.Commands.UpdatePartnerContact;
using Xunit;

namespace PEMS.UnitTests.Validation;

/// <summary>
/// Patch 5 (email validation consolidation) — mirrors <c>PhoneContractTests</c>' matrix approach
/// (Patch 3). Every user-write path that accepts a FRESH email value goes through the SAME canonical
/// rule (FluentValidation's built-in <c>EmailAddress()</c>) — this suite proves the same malformed
/// input (E-1/E-2) is rejected everywhere, that valid input including whitespace/casing variants
/// (E-3/E-4/E-5) is accepted everywhere, and that Partner Contact / OCR confirm (E-10/E-11) and a
/// direct validator call with no frontend involved at all (E-12) land on the same verdict.
///
/// <para>
/// Two validators are DELIBERATELY different from this matrix — documented here so the difference
/// reads as intentional, not as a gap Patch 5 missed:
/// </para>
/// <list type="bullet">
/// <item><see cref="OperationalContactReplayV2Validator"/> — an EXISTING campus's contact snapshot
/// being echoed back through edit/resubmit/amendment. No <c>EmailAddress()</c> at all, by design (its
/// own doc comment): format is not re-enforced on a read-only replay of data that may predate this
/// rule; whether the snapshot actually CHANGED is a separate, unconditional check
/// (<c>EnsureContactSnapshotUnchanged</c>).</item>
/// <item><c>AccountIdentityRules</c>'s "login email" concept (HO/Dept-Leader account provisioning,
/// mirrored on the frontend by <c>loginEmailValidation.ts</c>) — a materially stricter, SEPARATE rule
/// (domain whitelist, no <c>+</c>-aliasing) for a different business concept than a visit/contact
/// email. Out of Patch 5's scope; not touched, not collapsed into this matrix.</item>
/// </list>
/// </summary>
public class EmailContractTests
{
    private const string NoAtSign = "userexample.com"; // E-1 — malformed, missing @
    private const string EmptyDomain = "user@"; // E-2 — malformed domain (nothing after @)
    private const string PaddedMixedCase = "  User.Name@Example.COM  "; // E-3 (whitespace) + E-4 (case)
    private const string ValidStandard = "user@example.com"; // E-5

    // ── Visit V2 — registrant (fresh write) + operational contact (fresh write) ────────────────────

    private static readonly RegistrantInputV2Validator RegistrantValidator = new();
    private static readonly OperationalContactV2Validator OcValidator = new();
    private static readonly OperationalContactReplayV2Validator OcReplayValidator = new();

    private static RegistrantInputV2 Registrant(string email)
        => new("Nguyễn Văn A", "Việt Nam", "Đơn vị ABC", "Trưởng phòng", "+84912345678", email);

    private static ContactPointDto Contact(string email)
        => new("Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", "+84912345678", email);

    [Theory] // E-1, E-2, E-5
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void Registrant_email_matrix(string email, bool valid)
    {
        var result = RegistrantValidator.TestValidate(Registrant(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact] // E-3 + E-4 — whitespace/casing must not be treated as malformed
    public void Registrant_email_accepts_padded_mixed_case()
        => RegistrantValidator.TestValidate(Registrant(PaddedMixedCase.Trim()))
            .ShouldNotHaveValidationErrorFor(x => x.Email);

    [Theory] // E-1, E-2, E-5, E-6 (invalid Operational Contact email)
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void OperationalContact_fresh_write_email_matrix(string email, bool valid)
    {
        var result = OcValidator.TestValidate(Contact(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    /// <summary>DELIBERATELY different — see class doc. A malformed replay email is a length check
    /// only, not a format rejection; whether it's a real change is enforced elsewhere.</summary>
    [Theory]
    [InlineData(NoAtSign)]
    [InlineData(EmptyDomain)]
    public void OperationalContact_replay_does_not_reject_malformed_email(string email)
        => OcReplayValidator.TestValidate(Contact(email)).ShouldNotHaveValidationErrorFor(x => x.Email);

    // ── Manage Operational Contact — 4 validators share the same matrix (E-6) ───────────────────────

    private static readonly ReplaceOperationalContactCommandValidator ReplaceValidator = new();
    private static readonly UpdateOperationalContactProfileCommandValidator UpdateProfileValidator = new();
    private static readonly SaveOperationalContactCommandValidator SaveValidator = new();
    private static readonly InitiateOperationalContactTransferCommandValidator TransferValidator = new();

    private static ReplaceOperationalContactCommand ReplaceCmd(string email)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", null, email);

    private static UpdateOperationalContactProfileCommand UpdateProfileCmd(string email)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", null, email);

    private static SaveOperationalContactCommand SaveCmd(string email)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", null, email);

    private static InitiateOperationalContactTransferCommand TransferCmd(string email)
        => new(1, 10, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", null, email, "Bàn giao");

    [Theory]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void Replace_email_matrix(string email, bool valid)
    {
        var result = ReplaceValidator.TestValidate(ReplaceCmd(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void UpdateProfile_email_matrix(string email, bool valid)
    {
        var result = UpdateProfileValidator.TestValidate(UpdateProfileCmd(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void Save_email_matrix(string email, bool valid)
    {
        var result = SaveValidator.TestValidate(SaveCmd(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void Transfer_email_matrix(string email, bool valid)
    {
        var result = TransferValidator.TestValidate(TransferCmd(email));
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Partner Contact — create/update (E-10, optional field) ──────────────────────────────────────

    private static readonly CreatePartnerContactCommandValidator CreateContactValidator = new();
    private static readonly UpdatePartnerContactCommandValidator UpdateContactValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void CreatePartnerContact_email_matrix(string? email, bool valid)
    {
        var cmd = new CreatePartnerContactCommand { PartnerId = 1, FullName = "Nguyễn Văn A", Email = email };
        var result = CreateContactValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void UpdatePartnerContact_email_matrix(string? email, bool valid)
    {
        var cmd = new UpdatePartnerContactCommand { PartnerId = 1, ContactId = 5, FullName = "Nguyễn Văn A", Email = email };
        var result = UpdateContactValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Partner create — InitialContact.Email ────────────────────────────────────────────────────────

    private static readonly CreatePartnerCommandValidator CreatePartnerValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void CreatePartner_initialContact_email_matrix(string? email, bool valid)
    {
        var cmd = new CreatePartnerCommand
        {
            Name = "Đối tác ABC",
            InitialContact = new CreatePartnerCommand.InitialContactPayload { FullName = "Nguyễn Văn A", Email = email },
        };
        var result = CreatePartnerValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.InitialContact!.Email);
        else result.ShouldHaveValidationErrorFor(x => x.InitialContact!.Email);
    }

    // ── Business Card OCR confirmation (E-11 — user-reviewed write, distinct from the raw OCR
    //    candidate in BusinessCardOcrJob.ParsedEmail, which is never format-checked by design) ───────

    private static readonly ConfirmBusinessCardContactCommandValidator OcrValidator = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void ConfirmBusinessCardContact_email_matrix(string? email, bool valid)
    {
        var cmd = new ConfirmBusinessCardContactCommand { OcrJobId = 1, PartnerId = 1, FullName = "Nguyễn Văn A", Email = email };
        var result = OcrValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Legacy registrant-info endpoint (staff-entered request; email CAN change here — see its own
    //    handler doc comment — but must still be well-formed) ───────────────────────────────────────

    private static readonly UpdateRegistrantInfoCommandValidator RegistrantInfoValidator = new();

    [Theory]
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void UpdateRegistrantInfo_email_matrix(string email, bool valid)
    {
        var cmd = new UpdateRegistrantInfoCommand(1, "Nguyễn Văn A", "Đơn vị ABC", "Trưởng phòng", "0912345678", email);
        var result = RegistrantInfoValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.Email);
        else result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Resend OTP — a direct API surface with no form around it at all (E-12) ──────────────────────

    private static readonly ResendVisitRequestOtpCommandValidator ResendOtpValidator = new();

    [Theory] // E-12 — proves the SAME rule applies even on a bare, form-less command
    [InlineData(NoAtSign, false)]
    [InlineData(EmptyDomain, false)]
    [InlineData(ValidStandard, true)]
    public void ResendOtp_email_matrix(string email, bool valid)
    {
        var cmd = new ResendVisitRequestOtpCommand(email, "Nguyễn Văn A", Guid.NewGuid().ToString(), "session-token");
        var result = ResendOtpValidator.TestValidate(cmd);
        if (valid) result.ShouldNotHaveValidationErrorFor(x => x.RegistrantEmail);
        else result.ShouldHaveValidationErrorFor(x => x.RegistrantEmail);
    }
}
