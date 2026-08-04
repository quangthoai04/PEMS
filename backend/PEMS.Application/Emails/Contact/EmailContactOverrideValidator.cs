using System;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Validation;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Enums;
using PEMS.Shared;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// Turns a client's <see cref="EmailContactOverrideInput"/> into a
/// <see cref="NormalizedContactOverride"/>, or refuses it.
///
/// <para>
/// Two questions, deliberately answered by two methods, because they fail for different reasons and at
/// different times. <see cref="Normalize"/> asks whether the REQUEST is well-formed — a mode that exists,
/// fields that belong to that mode, an address that parses — and needs nothing from the database.
/// <see cref="AssertAllowed"/> asks whether this TEMPLATE will accept an override at all, which depends on
/// the capability and on a policy an operator can change between one send and the next. Merging them would
/// mean re-reading the policy to tell a caller they misspelled a mode.
/// </para>
/// <para>
/// Nothing here trusts a field the mode does not own. A <c>SYSTEM_USER</c> override carrying a
/// <c>displayName</c> is REFUSED rather than having the stray value ignored: silently dropping it would
/// let a client believe it had set the name shown to the recipient, and the difference between "ignored"
/// and "rejected" is exactly the difference between a caller that finds out and one that does not.
/// </para>
/// </summary>
public static class EmailContactOverrideValidator
{
    /// <summary>
    /// Validates shape and normalises whitespace. Returns null when the caller supplied nothing, or
    /// supplied a form the user never touched — both mean "use the policy".
    /// </summary>
    public static NormalizedContactOverride? Normalize(EmailContactOverrideInput? input)
    {
        if (input is null) return null;

        var mode = NormalizeMode(input.Mode);
        var replyToMode = NormalizeReplyToMode(input.ReplyToMode);
        var hide = input.HideForThisEmail == true;
        var reason = Text(input.Reason, nameof(input.Reason), EmailContactOverrideLimits.ReasonMax);

        // Nothing asked for, nothing to validate. An untouched contact form must never be the reason a
        // message cannot be sent.
        if (mode == EmailContactOverrideModes.TemplateDefault
            && !hide
            && replyToMode == EmailContactReplyToModes.PolicyDefault
            && reason is null)
        {
            AssertNoModeFields(input, mode);
            return null;
        }

        AssertNoModeFields(input, mode);

        return mode switch
        {
            EmailContactOverrideModes.SystemUser => SystemUser(input, replyToMode, hide, reason),
            EmailContactOverrideModes.Manual => Manual(input, replyToMode, hide, reason),
            _ => new NormalizedContactOverride(
                EmailContactOverrideModes.TemplateDefault,
                userId: null, displayName: null, roleLabel: null, email: null, phone: null,
                departmentName: null, campusName: null, replyToMode, hide, reason),
        };
    }

    /// <summary>
    /// Whether this template accepts this override at all, given what it can carry and how it is
    /// currently configured.
    /// </summary>
    /// <param name="templateCode">Named in every refusal — the operator has 31 templates to look at.</param>
    /// <param name="capability">Whether the block may EXIST here. Not an operator's to overrule.</param>
    /// <param name="requirement">The RESOLVED level for this send, after the policy cascade.</param>
    public static void AssertAllowed(
        NormalizedContactOverride? over,
        string templateCode,
        EmailContactCapabilityInfo capability,
        EmailContactRequirement requirement)
    {
        if (over is null || over.ChangesNothing) return;

        // Capability first, and it is absolute. A message whose whole content is a one-time code does not
        // grow a contact card because the person pressing send would like one there, and an override is
        // exactly the route by which that would otherwise happen — the settings endpoint already refuses
        // the same write, so leaving this open would make the send path the weaker of the two.
        if (!capability.Supported)
            throw new ValidationException(
                $"Mẫu email '{templateCode}' không dùng khối thông tin liên hệ, nên không thể đổi đầu mối "
                + "cho email này.",
                EmailErrorCodes.ContactOverrideNotAllowed);

        // A template the administrator has switched OFF is not a template a sender may switch back on for
        // one message. The block is a configuration decision; the override changes WHO is in it, never
        // WHETHER there is one.
        if (requirement == EmailContactRequirement.NONE)
            throw new ValidationException(
                $"Mức hiển thị thông tin liên hệ của mẫu '{templateCode}' đang là “Không hiển thị”, nên "
                + "email này không có khối liên hệ để đổi. Hãy đổi cấu hình mẫu nếu muốn bật.",
                EmailErrorCodes.ContactOverrideNotAllowed);

        // …and in the other direction: a template whose words tell the reader to get in touch may not have
        // the block hidden for one message, because the sentence would still be there.
        if (over.HideForThisEmail && requirement == EmailContactRequirement.REQUIRED)
            throw new ValidationException(
                $"Mẫu email '{templateCode}' bắt buộc hiển thị thông tin liên hệ, nên không thể ẩn khối "
                + "này cho email đang gửi.",
                EmailErrorCodes.ContactOverrideHideNotAllowed);

        // Hiding the block and naming somebody to put in it are contradictory instructions, and picking
        // one for the caller would guess at which they meant.
        if (over.HideForThisEmail && over.Mode != EmailContactOverrideModes.TemplateDefault)
            throw new ValidationException(
                "Không thể vừa ẩn khối thông tin liên hệ vừa chọn đầu mối khác cho email này.",
                EmailErrorCodes.ContactOverrideInvalid);

        // A hidden block has no address in it, so "replies go to the contact" would point at nothing the
        // recipient can see.
        if (over.HideForThisEmail && over.ReplyToMode == EmailContactReplyToModes.Contact)
            throw new ValidationException(
                "Không thể đặt Reply-To về đầu mối khi khối thông tin liên hệ bị ẩn cho email này.",
                EmailErrorCodes.ContactOverrideInvalid);
    }

    // ── Mode branches ───────────────────────────────────────────────────────

    private static NormalizedContactOverride SystemUser(
        EmailContactOverrideInput input, string replyToMode, bool hide, string? reason)
    {
        if (input.UserId is not { } userId || userId == 0)
            throw new ValidationException(
                "Hãy chọn một người trong hệ thống làm đầu mối liên hệ.",
                EmailErrorCodes.ContactOverrideInvalid);

        // Everything else about this person is read from the database by the resolver. The client sends an
        // id and nothing else, so a chosen contact can never be presented to a recipient under a name or
        // an address that is not the one PEMS holds for them.
        return new NormalizedContactOverride(
            EmailContactOverrideModes.SystemUser,
            userId,
            displayName: null, roleLabel: null, email: null, phone: null,
            departmentName: null, campusName: null,
            replyToMode, hide, reason);
    }

    private static NormalizedContactOverride Manual(
        EmailContactOverrideInput input, string replyToMode, bool hide, string? reason)
    {
        var displayName = Text(input.DisplayName, "Họ tên", EmailContactOverrideLimits.DisplayNameMax);
        var roleLabel = Text(input.RoleLabel, "Vai trò", EmailContactOverrideLimits.RoleLabelMax);
        var email = Text(input.Email, "Email", EmailContactOverrideLimits.EmailMax);
        var phone = Text(input.Phone, "Số điện thoại", EmailContactOverrideLimits.PhoneMax);
        var department = Text(input.DepartmentName, "Phòng ban", EmailContactOverrideLimits.DepartmentNameMax);
        var campus = Text(input.CampusName, "Cơ sở", EmailContactOverrideLimits.CampusNameMax);

        if (displayName is null)
            throw new ValidationException(
                "Hãy nhập họ tên của đầu mối liên hệ.", EmailErrorCodes.ContactOverrideInvalid);

        if (roleLabel is null)
            throw new ValidationException(
                "Hãy nhập vai trò của đầu mối liên hệ.", EmailErrorCodes.ContactOverrideInvalid);

        // A name under "please get in touch" with no way to get in touch is the original defect. It is
        // refused here for a hand-entered contact exactly as the renderer refuses it for a resolved one.
        if (email is null && phone is null)
            throw new ValidationException(
                "Đầu mối liên hệ phải có ít nhất email hoặc số điện thoại.",
                EmailErrorCodes.ContactOverrideInvalid);

        if (email is not null && !EmailRecipientValidator.IsWellFormed(email))
            throw new ValidationException(
                "Email của đầu mối liên hệ không hợp lệ.", EmailErrorCodes.ContactOverrideInvalid);

        // The project's own phone rule, not a second regex written here: a number the visit form accepts
        // and the contact block rejects would be a distinction nobody can explain.
        if (phone is not null && !PhoneNumber.IsValid(phone))
            throw new ValidationException(
                $"Số điện thoại của đầu mối liên hệ không hợp lệ. {PhoneNumberRules.FormatHint}",
                EmailErrorCodes.ContactOverrideInvalid);

        if (replyToMode == EmailContactReplyToModes.Contact && email is null)
            throw new ValidationException(
                "Reply-To trỏ về đầu mối nhưng đầu mối chưa có email.",
                EmailErrorCodes.ContactOverrideInvalid);

        // Why somebody outside PEMS is being presented as the contact is the one thing no later reader can
        // reconstruct — not from the audit row, not from the message. It is asked for while the sender
        // still knows the answer.
        if (reason is null)
            throw new ValidationException(
                "Hãy nhập lý do dùng đầu mối liên hệ nhập tay.",
                EmailErrorCodes.ContactOverrideReasonRequired);

        return new NormalizedContactOverride(
            EmailContactOverrideModes.Manual,
            userId: null,
            displayName, roleLabel, email, phone, department, campus,
            replyToMode, hide, reason);
    }

    // ── Shared field rules ──────────────────────────────────────────────────

    private static string NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return EmailContactOverrideModes.TemplateDefault;

        var trimmed = mode.Trim().ToUpperInvariant();
        if (!EmailContactOverrideModes.All.Contains(trimmed, StringComparer.Ordinal))
            throw new ValidationException(
                $"Chế độ đầu mối liên hệ '{mode.Trim()}' không hợp lệ.",
                EmailErrorCodes.ContactOverrideInvalid);

        return trimmed;
    }

    private static string NormalizeReplyToMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return EmailContactReplyToModes.PolicyDefault;

        var trimmed = mode.Trim().ToUpperInvariant();
        if (!EmailContactReplyToModes.All.Contains(trimmed, StringComparer.Ordinal))
            throw new ValidationException(
                $"Chế độ Reply-To '{mode.Trim()}' không hợp lệ.",
                EmailErrorCodes.ContactOverrideInvalid);

        return trimmed;
    }

    /// <summary>
    /// Refuses a field that does not belong to the declared mode.
    ///
    /// <para>
    /// The point is not tidiness. A <c>SYSTEM_USER</c> request carrying <c>email</c> is either a client
    /// that has misunderstood which value wins, or an attempt to show a chosen colleague's name over
    /// somebody else's address; both deserve an answer rather than a silent discard.
    /// </para>
    /// </summary>
    private static void AssertNoModeFields(EmailContactOverrideInput input, string mode)
    {
        var hasManualFields =
            !string.IsNullOrWhiteSpace(input.DisplayName)
            || !string.IsNullOrWhiteSpace(input.RoleLabel)
            || !string.IsNullOrWhiteSpace(input.Email)
            || !string.IsNullOrWhiteSpace(input.Phone)
            || !string.IsNullOrWhiteSpace(input.DepartmentName)
            || !string.IsNullOrWhiteSpace(input.CampusName);

        if (mode != EmailContactOverrideModes.Manual && hasManualFields)
            throw new ValidationException(
                mode == EmailContactOverrideModes.SystemUser
                    ? "Khi chọn người trong hệ thống, thông tin liên hệ được lấy từ hồ sơ của họ — không "
                      + "nhập tay tên/email/điện thoại."
                    : "Chỉ chế độ nhập thủ công mới nhận thông tin liên hệ do người dùng điền.",
                EmailErrorCodes.ContactOverrideInvalid);

        if (mode != EmailContactOverrideModes.SystemUser && input.UserId is not null)
            throw new ValidationException(
                "Chỉ chế độ chọn người trong hệ thống mới nhận mã người dùng.",
                EmailErrorCodes.ContactOverrideInvalid);
    }

    /// <summary>
    /// Trims, refuses markup and template braces, enforces the ceiling. Returns null for blank.
    ///
    /// <para>
    /// Markup is refused rather than encoded even though the renderer encodes everything anyway. The
    /// encoding is what keeps a recipient safe; this is what keeps a sender honest — a "name" of
    /// <c>&lt;b&gt;Phòng Đào tạo&lt;/b&gt;</c> that arrives as visible angle brackets is a support ticket,
    /// and one that arrives as bold text would be an authoring surface this feature exists not to open.
    /// </para>
    /// <para>
    /// Braces are refused for a concrete reason, not a cautious one: authored content and its trusted
    /// blocks are substituted TOGETHER, so a value of <c>{{hostName}}</c> in the block would be replaced
    /// with the real host's name, and any other brace pair would trip the unresolved-placeholder guard and
    /// fail the send with an error naming the template rather than the field.
    /// </para>
    /// </summary>
    private static string? Text(string? value, string fieldLabel, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        if (trimmed.IndexOf('<') >= 0 || trimmed.IndexOf('>') >= 0)
            throw new ValidationException(
                $"{fieldLabel} không được chứa mã HTML.", EmailErrorCodes.ContactOverrideInvalid);

        if (trimmed.Contains("{{", StringComparison.Ordinal) || trimmed.Contains("}}", StringComparison.Ordinal))
            throw new ValidationException(
                $"{fieldLabel} không được chứa dấu ngoặc nhọn kép của biến hệ thống.",
                EmailErrorCodes.ContactOverrideInvalid);

        // A contact value becomes a Reply-To display name or a header-adjacent string; a line break in one
        // is an injection, never formatting.
        EmailRecipientValidator.AssertNoHeaderBreak(trimmed, fieldLabel.ToLowerInvariant());

        if (trimmed.Length > max)
            throw new ValidationException(
                $"{fieldLabel} tối đa {max} ký tự.", EmailErrorCodes.ContactOverrideInvalid);

        return trimmed;
    }
}
