using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Whether a template may carry <c>{{contactInformationBlock}}</c> AT ALL — a different question from
/// what its policy is currently set to.
///
/// <para>
/// The distinction is the whole point. <see cref="EmailContactRequirement"/> is CONFIGURATION: an
/// operator moves it between NONE, OPTIONAL and REQUIRED and the block appears or does not. Capability is
/// not configurable, because the reasons behind it are not an operator's to overrule — a message whose
/// entire content is a one-time credential must not grow a block that widens what a forwarded copy
/// discloses, and a message addressed to the Host must not print the Host's own details back at them.
/// </para>
/// <para>
/// Without this split the two questions were answered by one value, and both answers were wrong in one
/// direction each. An operator could set OPTIONAL on a NONE-by-default template and save it — the
/// settings endpoint had no opinion — and then be refused with
/// <c>EMAIL_TEMPLATE_SYSTEM_BLOCK_NOT_ALLOWED</c> when they added the block the setting had just invited
/// them to add, because the contract read the SHIPPED default rather than the stored one. And in the
/// other direction, the screen offered the whole configuration form on templates that can never render a
/// block, so the only honest thing the card could say was said nowhere.
/// </para>
/// </summary>
public enum EmailContactCapability
{
    /// <summary>The block can never appear. No form, no save, no block in the body.</summary>
    UNSUPPORTED,

    /// <summary>The operator decides between NONE, OPTIONAL and REQUIRED.</summary>
    SUPPORTED,

    /// <summary>
    /// The text instructs the recipient to contact somebody, so a block is business-mandated. The
    /// operator may still choose between OPTIONAL and REQUIRED — that is a choice about what happens when
    /// no contact can be resolved — but not NONE, which would leave the instruction with no address.
    /// </summary>
    REQUIRED,
}

/// <param name="ReasonCode">
/// Stable, matched by clients; never the Vietnamese sentence, which is written for a person.
/// </param>
public sealed record EmailContactCapabilityInfo(
    EmailContactCapability Capability,
    string ReasonCode,
    string ReasonVi,
    string ReasonEn)
{
    /// <summary>True when the body may carry the block and the settings card may be shown.</summary>
    public bool Supported => Capability != EmailContactCapability.UNSUPPORTED;

    /// <summary>True when NONE is not one of the operator's choices.</summary>
    public bool BlockMandated => Capability == EmailContactCapability.REQUIRED;

    /// <summary>The requirement levels an operator may choose, in the order the screen shows them.</summary>
    public IReadOnlyList<string> AvailableRequirements => Capability switch
    {
        EmailContactCapability.UNSUPPORTED => Array.Empty<string>(),
        EmailContactCapability.REQUIRED => new[]
        {
            nameof(EmailContactRequirement.OPTIONAL), nameof(EmailContactRequirement.REQUIRED),
        },
        _ => Enum.GetNames<EmailContactRequirement>(),
    };
}

/// <summary>
/// Classifies every registered template into one of the three capability states, from an audit of what
/// each message carries and who receives it — never from its name.
///
/// <para>
/// The UNSUPPORTED set is enumerated here rather than derived from "the shipped default is NONE", even
/// though the two coincide today. They mean different things: a default is where configuration starts,
/// and this is a rule configuration may not reach. A future template could legitimately ship with the
/// block switched off and still be one an operator is allowed to switch on; it would then be SUPPORTED
/// with a NONE default, which this arrangement expresses and the derived one could not.
/// <c>EmailContactCapabilityTests</c> asserts the two agree while they are supposed to.
/// </para>
/// </summary>
public static class EmailContactCapabilities
{
    public const string ReasonOneTimeCredential = "ONE_TIME_CREDENTIAL";
    public const string ReasonRecipientIsTheContact = "RECIPIENT_IS_THE_CONTACT";
    public const string ReasonTextInstructsContact = "TEXT_INSTRUCTS_CONTACT";
    public const string ReasonOperatorChoice = "OPERATOR_CHOICE";

    /// <summary>
    /// Templates whose message is a credential. The link or code IS the mail; anything else in it is
    /// extra surface on a message that may be forwarded, quoted or read over a shoulder, and none of
    /// these texts asks the reader to contact anybody in the first place.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Code, string Vi, string En)> UnsupportedByTemplate =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            [SystemEmailTemplates.AccountEmailConfirmation] = (
                ReasonOneTimeCredential,
                "Mẫu này không dùng khối thông tin liên hệ vì email chứa liên kết xác nhận dùng một lần.",
                "This template carries a one-time confirmation link, so it does not use the contact block."),

            [SystemEmailTemplates.AuthPasswordResetOtp] = (
                ReasonOneTimeCredential,
                "Mẫu này không dùng khối thông tin liên hệ vì email chứa mã OTP đặt lại mật khẩu.",
                "This template carries a one-time password-reset code, so it does not use the contact block."),

            [SystemEmailTemplates.VisitRequestOtp] = (
                ReasonOneTimeCredential,
                "Mẫu này không dùng khối thông tin liên hệ vì email chứa mã OTP xác thực đăng ký.",
                "This template carries a one-time verification code, so it does not use the contact block."),

            [SystemEmailTemplates.VisitReminderHost] = (
                ReasonRecipientIsTheContact,
                "Mẫu này gửi cho chính người phụ trách tiếp đón, nên khối liên hệ sẽ in lại thông tin của "
                + "chính người nhận.",
                "This template goes to the Host, so a contact block would print the recipient's own details."),
        };

    /// <summary>The capability of a template, with the reason a person can read.</summary>
    public static EmailContactCapabilityInfo For(string? templateCode)
    {
        if (templateCode is not null && UnsupportedByTemplate.TryGetValue(templateCode, out var reason))
            return new EmailContactCapabilityInfo(
                EmailContactCapability.UNSUPPORTED, reason.Code, reason.Vi, reason.En);

        // The shipped default is REQUIRED exactly where the wording tells the reader to get in touch
        // (see EmailContactPolicyDefaults). That instruction is what makes the block business-mandated,
        // so the same classification answers both questions.
        if (EmailContactPolicyDefaults.For(templateCode).Requirement == EmailContactRequirement.REQUIRED)
            return new EmailContactCapabilityInfo(
                EmailContactCapability.REQUIRED,
                ReasonTextInstructsContact,
                "Nội dung mẫu này có câu yêu cầu người nhận liên hệ, nên email phải kèm khối thông tin liên hệ.",
                "This template's text tells the recipient to make contact, so the mail must carry the contact block.");

        return new EmailContactCapabilityInfo(
            EmailContactCapability.SUPPORTED,
            ReasonOperatorChoice,
            "Mẫu này có thể kèm khối thông tin liên hệ; mức hiển thị do người quản trị chọn.",
            "This template may carry the contact block; the requirement level is the administrator's choice.");
    }

    /// <summary>True when the body of this template may contain the contact placeholder at all.</summary>
    public static bool Supports(string? templateCode) => For(templateCode).Supported;

    /// <summary>Every template the audit classified as unable to carry the block.</summary>
    public static IReadOnlyCollection<string> UnsupportedTemplateCodes =>
        UnsupportedByTemplate.Keys.ToList();
}
