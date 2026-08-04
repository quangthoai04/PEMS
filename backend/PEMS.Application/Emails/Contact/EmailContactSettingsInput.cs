using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Emails.Common;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Contact;

/// <summary>
/// One proposed contact configuration, with its three enum fields already parsed.
///
/// <para>
/// A parsed value rather than the raw command, because two callers now write these settings — the
/// standalone <c>PUT /contact-settings</c> endpoint and the combined template save — and the parse itself
/// is a validation step that has to answer identically for both. Passing the raw strings around would let
/// one caller accept a spelling the other refused.
/// </para>
/// </summary>
public sealed record EmailContactSettingsInput(
    EmailContactRequirement Requirement,
    EmailContactSource ContactSource,
    bool ShowEmail,
    bool ShowPhone,
    bool ShowDepartment,
    bool ShowCampus,
    bool ShowSender,
    string? HeadingVi,
    string? HeadingEn,
    EmailReplyToSource ReplyToSource)
{
    /// <summary>
    /// The shipped policy for one template, in the form a write takes.
    ///
    /// <para>
    /// The two headings are nulled when they equal the shipped wording, and that is not cosmetic: a null
    /// heading means "inherit", so writing the default text literally would pin this template to today's
    /// wording and stop a future change to the system-wide heading from reaching it. Both restore paths —
    /// the standalone contact restore and the combined template restore — go through here so they cannot
    /// disagree about it.
    /// </para>
    /// </summary>
    public static EmailContactSettingsInput ShippedFor(string templateCode)
    {
        var shipped = EmailContactPolicyDefaults.For(templateCode);

        return new EmailContactSettingsInput(
            shipped.Requirement,
            shipped.ContactSource,
            shipped.ShowEmail,
            shipped.ShowPhone,
            shipped.ShowDepartment,
            shipped.ShowCampus,
            shipped.ShowSender,
            shipped.HeadingVi == EmailContactPolicyDefaults.DefaultHeadingVi ? null : shipped.HeadingVi,
            shipped.HeadingEn == EmailContactPolicyDefaults.DefaultHeadingEn ? null : shipped.HeadingEn,
            shipped.ReplyToSource);
    }
}

/// <summary>
/// Every rule a contact configuration must satisfy before it may be written, in one place.
///
/// <para>
/// <b>Why it was extracted.</b> These checks used to live inside
/// <c>UpdateEmailContactSettingsCommandHandler</c>, where they judged the settings against the bodies as
/// STORED. That is correct while settings and content are saved separately, and wrong the moment they are
/// saved together: an operator who deletes the block and switches to NONE in one action would have had
/// their new settings judged against the old body — refused for a block they had just removed — while one
/// who adds the block and switches to REQUIRED would have been refused for a block they had just added.
/// The bodies are therefore parameters, and each caller passes the pair that will actually be stored.
/// </para>
/// </summary>
public static class EmailContactSettingsValidator
{
    /// <summary>Long enough for a sentence-length heading, short enough not to become a paragraph.</summary>
    public const int MaxHeadingLength = 150;

    /// <summary>
    /// Parses the three enum fields, refusing an unknown name rather than silently defaulting it — a
    /// misspelt requirement that fell back to OPTIONAL would switch a block ON for a template somebody was
    /// trying to switch it off for.
    /// </summary>
    public static EmailContactSettingsInput Parse(
        string? requirement,
        string? contactSource,
        bool showEmail,
        bool showPhone,
        bool showDepartment,
        bool showCampus,
        bool showSender,
        string? headingVi,
        string? headingEn,
        string? replyToSource)
        => new(
            ParseEnum<EmailContactRequirement>(requirement, "mức bắt buộc"),
            ParseEnum<EmailContactSource>(contactSource, "nguồn đầu mối"),
            showEmail,
            showPhone,
            showDepartment,
            showCampus,
            showSender,
            CleanHeading(headingVi),
            CleanHeading(headingEn),
            ParseEnum<EmailReplyToSource>(replyToSource, "nguồn Reply-To"));

    /// <summary>
    /// Refuses a configuration that cannot be honoured, judged against the bodies that will be stored
    /// alongside it.
    /// </summary>
    /// <param name="bodyVi">The Vietnamese body as it will be AFTER this save, not as it is now.</param>
    /// <param name="bodyEn">The English body as it will be AFTER this save. Empty means unmaintained.</param>
    /// <exception cref="BusinessRuleException">Every failure, each with a code naming its own repair.</exception>
    public static void Validate(
        string templateCode,
        EmailContactSettingsInput input,
        string? bodyVi,
        string? bodyEn)
    {
        // ── Capability, before any value is looked at ────────────────────────
        // Fail-closed and FIRST: a template that cannot carry the block has no combination of the fields
        // below that would make this request meaningful.
        var capability = EmailContactCapabilities.For(templateCode);

        if (!capability.Supported)
            throw new BusinessRuleException(
                $"Mẫu '{templateCode}' không dùng khối thông tin liên hệ nên không có cấu hình liên hệ để lưu. "
                + capability.ReasonVi,
                EmailErrorCodes.ContactNotSupportedForTemplate);

        if (capability.BlockMandated && input.Requirement == EmailContactRequirement.NONE)
            throw new BusinessRuleException(
                $"Không đặt được mức Không hiển thị cho '{templateCode}': {capability.ReasonVi} "
                + "Hãy chọn Tùy chọn hoặc Bắt buộc.",
                EmailErrorCodes.ContactConfigurationInvalid);

        // The same two rules the resolver enforces, applied at SAVE time so the operator is told which
        // combination is wrong while they are looking at it — rather than discovering it when a send is
        // refused days later.
        if (input.Requirement != EmailContactRequirement.NONE && !input.ShowEmail && !input.ShowPhone)
            throw new BusinessRuleException(
                "Cấu hình không hợp lệ: mức bắt buộc khác NONE nhưng đã tắt cả email lẫn số điện thoại, "
                + "nên khối liên hệ sẽ không có cách liên hệ nào.",
                EmailErrorCodes.ContactConfigurationInvalid);

        if (input.ReplyToSource == EmailReplyToSource.CONTACT && !input.ShowEmail)
            throw new BusinessRuleException(
                "Cấu hình không hợp lệ: Reply-To trỏ về đầu mối nhưng email của đầu mối bị ẩn, "
                + "nên người nhận không thấy được nơi thư trả lời sẽ đến.",
                EmailErrorCodes.ContactConfigurationInvalid);

        // ── The two directions of the body/policy contract ───────────────────
        // REQUIRED needs the placeholder present; NONE needs it absent. Both are checked here so that a
        // combined save cannot satisfy one of them through the content validator and the other through
        // nothing at all.
        //
        // An English body that is empty is NOT a missing translation — it means this template does not
        // maintain English — so it is exempt from the REQUIRED check but not from the NONE one: an empty
        // body cannot contain a stray block either way.
        if (input.Requirement == EmailContactRequirement.REQUIRED)
        {
            var viMissing = !EmailContactBlockText.Contains(bodyVi);
            var enMissing = !string.IsNullOrWhiteSpace(bodyEn) && !EmailContactBlockText.Contains(bodyEn);

            if (viMissing || enMissing)
                throw new BusinessRuleException(
                    $"Không đặt được mức BẮT BUỘC cho '{templateCode}': "
                    + $"{DescribeMissing(viMissing, enMissing)} chưa có {EmailContactBlockText.Marker}. "
                    + "Hãy thêm khối vào nội dung trước, rồi mới đặt mức bắt buộc.",
                    EmailErrorCodes.TemplateRequiredContactBlockNotInBody);
        }

        if (input.Requirement == EmailContactRequirement.NONE)
        {
            var viHasBlock = EmailContactBlockText.Contains(bodyVi);
            var enHasBlock = EmailContactBlockText.Contains(bodyEn);

            if (viHasBlock || enHasBlock)
                throw new BusinessRuleException(
                    "Không thể lưu mẫu vì mức hiển thị là “Không hiển thị” nhưng "
                    + $"{DescribeMissing(viHasBlock, enHasBlock).ToLowerInvariant()} vẫn chứa "
                    + $"{EmailContactBlockText.Marker}. Hãy xóa khối khỏi nội dung, hoặc chọn lại "
                    + "“Tùy chọn”/“Bắt buộc”.",
                    EmailErrorCodes.ContactBlockNotAllowedWhenHidden);
        }
    }

    /// <summary>Which language(s) a body-level failure is about, in one readable phrase.</summary>
    private static string DescribeMissing(bool vi, bool en) => (vi, en) switch
    {
        (true, true) => "Nội dung tiếng Việt và tiếng Anh",
        (true, false) => "Nội dung tiếng Việt",
        (false, true) => "Nội dung tiếng Anh",
        _ => "Nội dung",
    };

    private static T ParseEnum<T>(string? value, string label) where T : struct, Enum
        => Enum.TryParse<T>(value?.Trim(), ignoreCase: false, out var parsed)
            ? parsed
            : throw new BusinessRuleException(
                $"Giá trị '{value}' không hợp lệ cho {label}. Hợp lệ: {string.Join(", ", Enum.GetNames<T>())}.",
                EmailErrorCodes.ContactConfigurationInvalid);

    /// <summary>
    /// A heading is TEXT. Any markup is stripped rather than escaped, because the renderer encodes it
    /// again anyway and an operator who pastes a tag should see it disappear at save time instead of
    /// meeting "&amp;lt;b&amp;gt;" in a preview. Empty means "use the default", not "no heading".
    /// </summary>
    public static string? CleanHeading(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = Regex
            .Replace(value, "<[^>]*>", string.Empty)
            .Replace("\r", " ").Replace("\n", " ")
            .Trim();

        if (text.Length > MaxHeadingLength) text = text[..MaxHeadingLength].TrimEnd();

        return text.Length == 0 ? null : text;
    }
}

/// <summary>
/// Writes one template's contact policy row — the TEMPLATE level of the cascade — without saving.
///
/// <para>
/// It does not call <c>SaveChangesAsync</c>, and that omission is the point. The combined template save
/// has to write the content and the policy inside ONE transaction that commits once, so a helper that
/// committed on its own would put a policy change on disk before the content write had been accepted —
/// which is the partial save this whole change exists to remove.
/// </para>
/// </summary>
public static class EmailContactPolicyWriter
{
    /// <summary>
    /// Finds or creates the TEMPLATE-scope row and applies <paramref name="input"/> to it. Returns the
    /// tracked row so a caller that audits the change can snapshot it.
    /// </summary>
    public static async Task<EmailContactPolicy> ApplyAsync(
        IApplicationDbContext db,
        string templateCode,
        EmailContactSettingsInput input,
        ulong? actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var row = await db.EmailContactPolicies
            .FirstOrDefaultAsync(
                p => p.ScopeType == EmailContactScopeType.TEMPLATE && p.ScopeKey == templateCode,
                cancellationToken);

        if (row is null)
        {
            row = new EmailContactPolicy
            {
                ScopeType = EmailContactScopeType.TEMPLATE,
                ScopeKey = templateCode,
                CreatedAt = now,
                CreatedBy = actorUserId,
            };
            db.EmailContactPolicies.Add(row);
        }

        row.Requirement = input.Requirement;
        row.ContactSource = input.ContactSource;
        row.ShowEmail = input.ShowEmail;
        row.ShowPhone = input.ShowPhone;
        row.ShowDepartment = input.ShowDepartment;
        row.ShowCampus = input.ShowCampus;
        row.ShowSender = input.ShowSender;
        row.HeadingVi = input.HeadingVi;
        row.HeadingEn = input.HeadingEn;
        row.ReplyToSource = input.ReplyToSource;
        row.UpdatedAt = now;
        row.UpdatedBy = actorUserId;

        return row;
    }
}
