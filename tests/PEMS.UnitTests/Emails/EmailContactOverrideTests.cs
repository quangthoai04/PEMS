using System;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Emails.Common;
using PEMS.Application.Emails.Contact;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.Emails;

/// <summary>
/// The per-message contact override: what a sender may ask for, and what they may not.
///
/// <para>
/// The feature exists because the contact a template resolves is not always the right one for a
/// particular message — the Host is on leave, the event is actually being run by a colleague, the caterer
/// has no PEMS account. The risk it introduces is the mirror image: a form that lets somebody put a name
/// and an address into an email and have the system present them as the Host's. Nearly every assertion
/// below is about that second thing, which is why so many of them are refusals.
/// </para>
/// </summary>
public class EmailContactOverrideTests
{
    private static readonly EmailContactCapabilityInfo Supported =
        EmailContactCapabilities.For(SystemEmailTemplates.AccountRoleChanged);

    private static readonly EmailContactCapabilityInfo Unsupported =
        EmailContactCapabilities.For(SystemEmailTemplates.AccountEmailConfirmation);

    private static readonly EmailContactCapabilityInfo Mandated =
        EmailContactCapabilities.For(SystemEmailTemplates.VisitParticipantInvitation);

    // ── 1. Nothing asked for is not an error ────────────────────────────────

    /// <summary>
    /// A sender who opens the contact editor and closes it again sends an untouched form. Treating that
    /// as a validation failure would make "I looked at it" a reason the message cannot go out.
    /// </summary>
    [Fact]
    public void An_untouched_form_normalises_to_no_override()
    {
        Assert.Null(EmailContactOverrideValidator.Normalize(null));
        Assert.Null(EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput()));
        Assert.Null(EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "TEMPLATE_DEFAULT", ReplyToMode: "POLICY_DEFAULT")));
    }

    [Fact]
    public void An_unknown_mode_is_refused_by_name()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(Mode: "HOST")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void An_unknown_reply_to_mode_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(
                new EmailContactOverrideInput(Mode: "TEMPLATE_DEFAULT", ReplyToMode: "SOMEBODY_ELSE")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    // ── 2. SYSTEM_USER carries an id and nothing else ───────────────────────

    [Fact]
    public void Choosing_a_system_user_requires_an_id()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(Mode: "SYSTEM_USER")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    /// <summary>
    /// The one that matters most. Accepting a name alongside a user id would let a caller show a chosen
    /// colleague's identity over an address the sender typed — which is precisely the impersonation the
    /// block's "values come from the database" rule exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("Nguyễn Văn A", null, null)]
    [InlineData(null, "gia.mao@example.invalid", null)]
    [InlineData(null, null, "0900000000")]
    public void Choosing_a_system_user_refuses_hand_typed_identity(string? name, string? email, string? phone)
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "SYSTEM_USER", UserId: 42, DisplayName: name, Email: email, Phone: phone)));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Choosing_a_system_user_keeps_only_the_id()
    {
        var over = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42, ReplyToMode: "CONTACT"));

        Assert.NotNull(over);
        Assert.True(over!.IsSystemUser);
        Assert.Equal(42UL, over.UserId);
        Assert.Null(over.DisplayName);
        Assert.Null(over.Email);
        Assert.Equal(EmailContactReplyToModes.Contact, over.ReplyToMode);
    }

    [Fact]
    public void A_user_id_on_a_non_system_user_mode_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(
                new EmailContactOverrideInput(Mode: "TEMPLATE_DEFAULT", UserId: 42)));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    // ── 3. MANUAL: a contact somebody can actually reach ────────────────────

    [Fact]
    public void Manual_requires_a_name()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", RoleLabel: "Bếp trưởng", Email: "bep@example.invalid", Reason: "Nhà thầu ngoài")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    /// <summary>
    /// A name under "please get in touch" with no way to get in touch is the original defect. The manual
    /// path has to refuse it for the same reason the renderer refuses it for a resolved contact.
    /// </summary>
    [Fact]
    public void Manual_requires_at_least_an_email_or_a_phone()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng", Reason: "Nhà thầu ngoài")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Manual_refuses_an_unparseable_email()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng",
                Email: "khong-phai-email", Reason: "Nhà thầu ngoài")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Manual_refuses_an_unparseable_phone()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng",
                Phone: "gọi cho tôi", Reason: "Nhà thầu ngoài")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Manual_reply_to_contact_requires_an_email()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng",
                Phone: "0912345678", ReplyToMode: "CONTACT", Reason: "Nhà thầu ngoài")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    /// <summary>
    /// Why somebody outside PEMS is presented to a guest as the contact is the one thing no later reader
    /// can reconstruct — not from the message, not from the audit row.
    /// </summary>
    [Fact]
    public void Manual_requires_a_reason()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng",
                Email: "bep@example.invalid")));

        Assert.Equal(EmailErrorCodes.ContactOverrideReasonRequired, ex.ErrorCode);
    }

    [Fact]
    public void Manual_accepts_a_complete_contact_and_trims_it()
    {
        var over = EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
            Mode: "manual",
            DisplayName: "  Trần B  ", RoleLabel: " Bếp trưởng ",
            Email: " bep@example.invalid ", Phone: " 0912345678 ",
            DepartmentName: " Nhà thầu ", CampusName: " FPTU Hà Nội ",
            Reason: " Host đi công tác "));

        Assert.NotNull(over);
        Assert.True(over!.IsManual);
        Assert.Equal("Trần B", over.DisplayName);
        Assert.Equal("Bếp trưởng", over.RoleLabel);
        Assert.Equal("bep@example.invalid", over.Email);
        Assert.Equal("Host đi công tác", over.Reason);
    }

    // ── 4. Text fields are text, not markup and not templates ───────────────

    /// <summary>
    /// The renderer HTML-encodes every value it prints, so this is not what keeps a recipient safe — it
    /// is what keeps the block from becoming an authoring surface. A "name" that arrives as bold text
    /// would be exactly the thing the design refuses on the template screen.
    /// </summary>
    [Fact]
    public void Manual_fields_refuse_markup()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "<b>Trần B</b>", RoleLabel: "Bếp trưởng",
                Email: "bep@example.invalid", Reason: "Nhà thầu")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    /// <summary>
    /// Authored bodies and their trusted blocks are substituted TOGETHER, so a braced value inside the
    /// block is not inert: <c>{{hostName}}</c> would be replaced with the real Host's name, and anything
    /// else would fail the send with an unresolved-placeholder error naming the template rather than the
    /// field the sender typed it into.
    /// </summary>
    [Fact]
    public void Manual_fields_refuse_template_braces()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "{{hostName}}", RoleLabel: "Bếp trưởng",
                Email: "bep@example.invalid", Reason: "Nhà thầu")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Manual_fields_refuse_a_header_break()
    {
        Assert.ThrowsAny<Exception>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B\r\nBcc: ai.do@example.invalid", RoleLabel: "Bếp trưởng",
                Email: "bep@example.invalid", Reason: "Nhà thầu")));
    }

    [Fact]
    public void Manual_fields_are_length_capped()
    {
        var ex = Assert.Throws<ValidationException>(
            () => EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
                Mode: "MANUAL",
                DisplayName: new string('a', EmailContactOverrideLimits.DisplayNameMax + 1),
                RoleLabel: "Bếp trưởng", Email: "bep@example.invalid", Reason: "Nhà thầu")));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    // ── 5. Capability and requirement outrank the sender ────────────────────

    /// <summary>
    /// A message whose whole content is a one-time credential does not grow a contact card because the
    /// person sending it would like one there. The settings endpoint already refuses the equivalent
    /// write; leaving this open would make the send path the weaker of the two.
    /// </summary>
    [Fact]
    public void An_unsupported_template_refuses_every_override()
    {
        var over = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42));

        var ex = Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            over, SystemEmailTemplates.AccountEmailConfirmation, Unsupported,
            EmailContactRequirement.NONE));

        Assert.Equal(EmailErrorCodes.ContactOverrideNotAllowed, ex.ErrorCode);
    }

    /// <summary>
    /// The credential set, pinned by code rather than by shape, so adding a template to it is a decision
    /// somebody writes down.
    /// </summary>
    [Theory]
    [InlineData(SystemEmailTemplates.AccountEmailConfirmation)]
    [InlineData(SystemEmailTemplates.AuthPasswordResetOtp)]
    [InlineData(SystemEmailTemplates.VisitRequestOtp)]
    [InlineData(SystemEmailTemplates.VisitReminderHost)]
    public void The_credential_and_self_addressed_templates_never_accept_an_override(string code)
    {
        var over = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 7));

        Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            over, code, EmailContactCapabilities.For(code), EmailContactRequirement.NONE));
    }

    /// <summary>
    /// An administrator switched the block off. A sender may change WHO is in the block; whether there is
    /// one at all is not theirs to decide for a single message.
    /// </summary>
    [Fact]
    public void A_policy_of_none_refuses_an_override_even_on_a_supported_template()
    {
        var over = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42));

        var ex = Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            over, SystemEmailTemplates.AccountRoleChanged, Supported, EmailContactRequirement.NONE));

        Assert.Equal(EmailErrorCodes.ContactOverrideNotAllowed, ex.ErrorCode);
    }

    [Fact]
    public void Optional_accepts_default_system_user_manual_and_hide()
    {
        foreach (var input in new[]
        {
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42),
            new EmailContactOverrideInput(
                Mode: "MANUAL", DisplayName: "Trần B", RoleLabel: "Bếp trưởng",
                Email: "bep@example.invalid", Reason: "Nhà thầu"),
            new EmailContactOverrideInput(Mode: "TEMPLATE_DEFAULT", HideForThisEmail: true),
        })
        {
            var over = EmailContactOverrideValidator.Normalize(input);
            EmailContactOverrideValidator.AssertAllowed(
                over, SystemEmailTemplates.AccountRoleChanged, Supported, EmailContactRequirement.OPTIONAL);
        }
    }

    /// <summary>
    /// REQUIRED means the template's own words tell the reader to make contact. Hiding the block for one
    /// message would leave that sentence with nothing behind it — the defect, arrived at one send at a
    /// time instead of by configuration.
    /// </summary>
    [Fact]
    public void Required_refuses_hide_but_accepts_a_different_contact()
    {
        var hide = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "TEMPLATE_DEFAULT", HideForThisEmail: true));

        var ex = Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            hide, SystemEmailTemplates.VisitParticipantInvitation, Mandated,
            EmailContactRequirement.REQUIRED));

        Assert.Equal(EmailErrorCodes.ContactOverrideHideNotAllowed, ex.ErrorCode);

        var chosen = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42));

        EmailContactOverrideValidator.AssertAllowed(
            chosen, SystemEmailTemplates.VisitParticipantInvitation, Mandated,
            EmailContactRequirement.REQUIRED);
    }

    [Fact]
    public void Hiding_the_block_and_naming_a_contact_are_refused_together()
    {
        var over = EmailContactOverrideValidator.Normalize(
            new EmailContactOverrideInput(Mode: "SYSTEM_USER", UserId: 42, HideForThisEmail: true));

        var ex = Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            over, SystemEmailTemplates.AccountRoleChanged, Supported, EmailContactRequirement.OPTIONAL));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Hiding_the_block_refuses_a_reply_to_pointed_at_it()
    {
        var over = EmailContactOverrideValidator.Normalize(new EmailContactOverrideInput(
            Mode: "TEMPLATE_DEFAULT", HideForThisEmail: true, ReplyToMode: "CONTACT"));

        var ex = Assert.Throws<ValidationException>(() => EmailContactOverrideValidator.AssertAllowed(
            over, SystemEmailTemplates.AccountRoleChanged, Supported, EmailContactRequirement.OPTIONAL));

        Assert.Equal(EmailErrorCodes.ContactOverrideInvalid, ex.ErrorCode);
    }

    // ── 6. The client can never name an address for Reply-To ────────────────

    /// <summary>
    /// The four modes select between outcomes the BACKEND computes: the contact's own address, the signed
    /// in sender's, the configured policy, or none. There is deliberately no mode that accepts an
    /// address, because a Reply-To is a header the recipient trusts and nobody would have verified it.
    /// </summary>
    [Fact]
    public void Reply_to_offers_only_backend_computed_sources()
    {
        Assert.Equal(
            new[] { "POLICY_DEFAULT", "CONTACT", "SENDER", "NONE" },
            EmailContactReplyToModes.All.ToArray());

        Assert.DoesNotContain(
            typeof(EmailContactOverrideInput).GetProperties(),
            p => p.Name.Contains("ReplyToAddress", StringComparison.Ordinal)
                 || p.Name.Contains("ReplyToEmail", StringComparison.Ordinal));
    }

    [Fact]
    public void The_three_modes_are_the_whole_set()
    {
        Assert.Equal(
            new[] { "TEMPLATE_DEFAULT", "SYSTEM_USER", "MANUAL" },
            EmailContactOverrideModes.All.ToArray());
    }
}
