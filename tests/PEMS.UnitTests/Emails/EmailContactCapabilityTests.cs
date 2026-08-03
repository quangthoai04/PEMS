using System;
using System.Linq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// Contact CAPABILITY — whether a template may carry <c>{{contactInformationBlock}}</c> at all — as
/// distinct from the contact POLICY, which is what an operator sets it to.
///
/// <para>
/// The reported defect lived exactly in the gap between those two questions, and it was answerable from
/// the screen alone. Card 4 offered the full requirement form on <c>ACCOUNT_EMAIL_CONFIRMATION</c>, whose
/// message is a one-time confirmation link; an operator chose "Tùy chọn", saved it — the settings
/// endpoint had no opinion — added the block that setting exists to place, and was refused with
/// <c>EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED</c>, because the CONTRACT was still reading the shipped
/// default. Three components, three different answers to one question.
/// </para>
/// <para>
/// The repair was NOT to open the block on every template — see the decision record: a credential-bearing
/// mail must not grow a block that widens what a forwarded copy discloses, and a REQUIRED level can block
/// a send outright when no contact resolves. What these tests pin is that the three states are declared
/// once and that every component reads the same declaration.
/// </para>
/// </summary>
public class EmailContactCapabilityTests
{
    private const string ContactBlock = EmailTrustedBlocks.ContactInformationBlock;
    private static readonly string Marker = "{{" + ContactBlock + "}}";

    // ── 1. The classification itself ─────────────────────────────────────────

    [Fact]
    public void Every_registered_template_resolves_a_capability()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var capability = EmailContactCapabilities.For(code);

            Assert.False(string.IsNullOrWhiteSpace(capability.ReasonCode));
            Assert.False(string.IsNullOrWhiteSpace(capability.ReasonVi));
            Assert.False(string.IsNullOrWhiteSpace(capability.ReasonEn));
        }
    }

    /// <summary>
    /// The audit's four: three whose whole message is a one-time credential, and one addressed to the very
    /// person the block would name.
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.AccountEmailConfirmation)]
    [InlineData(SystemEmailTemplates.AuthPasswordResetOtp)]
    [InlineData(SystemEmailTemplates.VisitRequestOtp)]
    [InlineData(SystemEmailTemplates.VisitReminderHost)]
    public void The_audited_templates_cannot_carry_the_block(string code)
    {
        Assert.Equal(EmailContactCapability.UNSUPPORTED, EmailContactCapabilities.For(code).Capability);
        Assert.False(EmailContactCapabilities.Supports(code));
    }

    /// <summary>
    /// Capability and the shipped default agree, in both directions.
    ///
    /// <para>
    /// They are declared separately on purpose — a default is where configuration starts, capability is a
    /// rule configuration may not reach — so this asserts they have not drifted while they are supposed to
    /// coincide. A template classified UNSUPPORTED but shipping OPTIONAL would seed a policy row the
    /// resolver then overrides, which is a contradiction nobody would see until a send.
    /// </para>
    /// </summary>
    [Fact]
    public void Unsupported_is_exactly_the_set_that_ships_with_no_block()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var shipsNone = EmailContactPolicyDefaults.For(code).Requirement == EmailContactRequirement.NONE;
            var unsupported = !EmailContactCapabilities.Supports(code);

            Assert.True(shipsNone == unsupported,
                $"{code}: shipped default says {(shipsNone ? "NONE" : "a block")} but capability says "
                + $"{(unsupported ? "UNSUPPORTED" : "supported")}.");
        }
    }

    [Fact]
    public void Business_mandated_is_exactly_the_set_that_ships_REQUIRED()
    {
        foreach (var code in SystemEmailTemplates.AllCodes)
        {
            var shipsRequired =
                EmailContactPolicyDefaults.For(code).Requirement == EmailContactRequirement.REQUIRED;

            Assert.Equal(shipsRequired, EmailContactCapabilities.For(code).BlockMandated);
        }
    }

    /// <summary>The levels an operator is offered, narrowed by capability rather than by the screen.</summary>
    [Fact]
    public void The_offered_levels_follow_the_capability()
    {
        Assert.Empty(EmailContactCapabilities
            .For(SystemEmailTemplates.AccountEmailConfirmation).AvailableRequirements);

        // A template whose text says "vui lòng liên hệ" may be relaxed to OPTIONAL — which is a decision
        // about what happens when nobody resolves — but not to NONE, which would leave the instruction
        // with no address at all.
        var mandated = EmailContactCapabilities.For(SystemEmailTemplates.VisitParticipantInvitation);
        Assert.Equal(EmailContactCapability.REQUIRED, mandated.Capability);
        Assert.DoesNotContain(nameof(EmailContactRequirement.NONE), mandated.AvailableRequirements);
        Assert.Contains(nameof(EmailContactRequirement.OPTIONAL), mandated.AvailableRequirements);

        var free = EmailContactCapabilities.For(SystemEmailTemplates.AccountRoleChanged);
        Assert.Equal(EmailContactCapability.SUPPORTED, free.Capability);
        Assert.Equal(3, free.AvailableRequirements.Count);
    }

    // ── 2. The contract reads the same declaration ───────────────────────────

    [Fact]
    public void An_unsupported_template_offers_the_block_nowhere()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountEmailConfirmation)!;

        Assert.False(contract.ContactSupported);
        Assert.False(contract.ContactRequired);
        Assert.False(contract.ContactSettingsEditable);
        Assert.False(contract.AllowsSystemBlock(ContactBlock));
        Assert.DoesNotContain(ContactBlock, contract.RequiredSystemBlocks);
        Assert.DoesNotContain(ContactBlock, contract.OptionalSystemBlocks);
        Assert.Equal(EmailContactCapabilities.ReasonOneTimeCredential, contract.ContactReasonCode);
    }

    /// <summary>
    /// The reported refusal, at its source.
    ///
    /// <para>
    /// A supported template whose CURRENT level is NONE must still admit the block into its body. It did
    /// not: the contract asked the shipped default whether the policy "renders a block", so an operator
    /// who had just switched the level on was told the block "không dùng được ở mẫu này" — about a
    /// template that supports it, in the state the setting they had just saved put it in.
    /// </para>
    /// </summary>
    [Fact]
    public void A_supported_template_admits_the_block_at_every_level()
    {
        foreach (var level in new[]
                 {
                     EmailContactRequirement.NONE,
                     EmailContactRequirement.OPTIONAL,
                     EmailContactRequirement.REQUIRED,
                 })
        {
            var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountRoleChanged, level)!;

            Assert.True(contract.ContactSupported);
            Assert.True(contract.AllowsSystemBlock(ContactBlock),
                $"level {level} must not make a supported template's block illegal");
        }
    }

    /// <summary>Required means required — and only at the level the send will actually use.</summary>
    [Fact]
    public void The_block_is_demanded_only_where_the_effective_level_is_REQUIRED()
    {
        var code = SystemEmailTemplates.VisitParticipantInvitation;

        var atRequired = EmailTemplateContracts.For(code, EmailContactRequirement.REQUIRED)!;
        Assert.True(atRequired.ContactRequired);
        Assert.Contains(ContactBlock, atRequired.RequiredSystemBlocks);

        // The other direction of the same drift: an operator who lowered the level must be able to remove
        // the block. Reading the shipped default here refused that edit, citing a level no screen showed.
        var atOptional = EmailTemplateContracts.For(code, EmailContactRequirement.OPTIONAL)!;
        Assert.False(atOptional.ContactRequired);
        Assert.Contains(ContactBlock, atOptional.OptionalSystemBlocks);
        Assert.DoesNotContain(ContactBlock, atOptional.RequiredSystemBlocks);
    }

    [Fact]
    public void Required_blocks_for_a_send_follow_the_effective_level_and_the_capability()
    {
        var code = SystemEmailTemplates.VisitParticipantInvitation;

        Assert.Contains(EmailTemplateContracts.RequiredBlocksFor(code, EmailContactRequirement.REQUIRED),
            b => b.Block == ContactBlock);

        Assert.DoesNotContain(EmailTemplateContracts.RequiredBlocksFor(code, EmailContactRequirement.OPTIONAL),
            b => b.Block == ContactBlock);

        // Capability wins over a level a stray policy row might carry.
        Assert.DoesNotContain(
            EmailTemplateContracts.RequiredBlocksFor(
                SystemEmailTemplates.AccountEmailConfirmation, EmailContactRequirement.REQUIRED),
            b => b.Block == ContactBlock);
    }

    // ── 3. The validator fails closed on the same rule ───────────────────────

    [Fact]
    public void The_validator_refuses_the_block_on_an_unsupported_template()
    {
        var contract = EmailTemplateContracts.For(SystemEmailTemplates.AccountEmailConfirmation)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Xác nhận email",
            bodyVi: "<p>Chào {{fullName}}.</p>" + Marker + "{{actionBlock}}",
            subjectEn: null, bodyEn: null);

        var refusal = Assert.Single(issues, i => i.VariableName == ContactBlock);
        Assert.Equal(EmailErrorCodes.TemplateSystemBlockNotAllowed, refusal.Code);
        Assert.Contains("xoá khối", refusal.MessageVi, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_validator_accepts_the_block_on_a_supported_template_currently_set_to_NONE()
    {
        var contract = EmailTemplateContracts.For(
            SystemEmailTemplates.AccountRoleChanged, EmailContactRequirement.NONE)!;

        var issues = EmailTemplateContentValidator.Validate(
            contract,
            subjectVi: "Vai trò của bạn đã thay đổi",
            bodyVi: "<p>Chào {{fullName}}.</p>" + Marker,
            subjectEn: null, bodyEn: null);

        Assert.DoesNotContain(issues, i => i.VariableName == ContactBlock);
    }

    /// <summary>
    /// A body without the block is a legal save once the level has been lowered — the save and the send
    /// have to agree, and before this they did not.
    /// </summary>
    [Fact]
    public void The_validator_stops_demanding_the_block_once_the_level_is_lowered()
    {
        var code = SystemEmailTemplates.VisitParticipantInvitation;
        var body = "<p>Chào {{recipientName}}.</p>{{actionBlock}}";

        var atRequired = EmailTemplateContentValidator.Validate(
            EmailTemplateContracts.For(code, EmailContactRequirement.REQUIRED)!,
            subjectVi: "Lời mời", bodyVi: body, subjectEn: null, bodyEn: null);

        Assert.Contains(atRequired,
            i => i.Code == EmailErrorCodes.TemplateRequiredContactBlockNotInBody);

        var atOptional = EmailTemplateContentValidator.Validate(
            EmailTemplateContracts.For(code, EmailContactRequirement.OPTIONAL)!,
            subjectVi: "Lời mời", bodyVi: body, subjectEn: null, bodyEn: null);

        Assert.DoesNotContain(atOptional,
            i => i.Code == EmailErrorCodes.TemplateRequiredContactBlockNotInBody);
    }

    // ── 4. The shipped wording agrees with the classification ────────────────

    /// <summary>
    /// No unsupported template's shipped body writes the placeholder. If one did, a restore would install
    /// content its own validator refuses — the operator's repair path would be the thing that breaks it.
    /// </summary>
    [Fact]
    public void No_unsupported_template_ships_a_body_carrying_the_block()
    {
        var offenders = EmailContactCapabilities.UnsupportedTemplateCodes
            .Select(code => (Code: code, Shipped: EmailTemplateDefaults.For(code)))
            .Where(x => x.Shipped is not null)
            .Where(x => (x.Shipped!.BodyVi ?? "").Contains(Marker, StringComparison.Ordinal)
                        || (x.Shipped.BodyEn ?? "").Contains(Marker, StringComparison.Ordinal))
            .Select(x => x.Code)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These templates cannot carry the contact block but ship a body that writes it: "
            + string.Join(", ", offenders));
    }
}
