using PEMS.Application.Common.Exceptions;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Enforces, at dispatch time, the recipient policy declared for a system template.
///
/// <para>
/// This runs even when a caller has already checked the same thing. The rule it protects is not a
/// formality: an email carrying a one-time accept link, a password-reset code or a visit OTP addresses
/// exactly one person, because a CC or BCC on such a message hands a second person a credential minted
/// for the first. Callers get added and edited over time; this gate does not depend on each of them
/// remembering.
/// </para>
/// <para>
/// User-authored mail (compose, draft, reply) has no template code and is therefore unrestricted here —
/// its envelope is the sender's decision, already validated for shape by
/// <see cref="EmailRecipientValidator"/>.
/// </para>
/// </summary>
public static class EmailRecipientPolicyEnforcer
{
    /// <summary>
    /// Throws when the envelope violates the template's policy. A null or unregistered
    /// <paramref name="templateCode"/> means user-authored mail and is allowed through.
    /// </summary>
    public static void Assert(string? templateCode, ValidatedEnvelope envelope)
    {
        var template = SystemEmailTemplates.Find(templateCode);
        if (template is null) return;

        if (template.RecipientPolicy != EmailRecipientPolicy.SingleRecipientNoCopies) return;

        if (envelope.HasCopies)
            throw new BusinessRuleException(
                $"Email hệ thống '{template.TemplateCode}' không cho phép CC/BCC. " +
                (template.HasSensitiveAction
                    ? "Email này chứa mã hoặc liên kết dùng riêng cho một người nhận."
                    : "Email này chỉ gửi riêng cho từng người nhận."),
                EmailErrorCodes.RecipientTypeNotAllowed);

        if (envelope.To.Count > 1)
            throw new BusinessRuleException(
                $"Email hệ thống '{template.TemplateCode}' phải gửi riêng cho từng người nhận " +
                $"(đang có {envelope.To.Count} địa chỉ ở mục Đến).",
                EmailErrorCodes.RecipientTypeNotAllowed);
    }
}
