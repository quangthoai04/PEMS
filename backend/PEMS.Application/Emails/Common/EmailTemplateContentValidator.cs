using System;
using System.Collections.Generic;
using System.Linq;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>Which edited field an issue belongs to. Matches the property names the API accepts.</summary>
public static class EmailTemplateFields
{
    public const string SubjectVi = "subjectVi";
    public const string SubjectEn = "subjectEn";
    public const string BodyVi = "bodyVi";
    public const string BodyEn = "bodyEn";
}

public static class EmailTemplateIssueSeverity
{
    /// <summary>Blocks the save.</summary>
    public const string Error = "ERROR";

    /// <summary>Shown, but does not block.</summary>
    public const string Warning = "WARNING";
}

/// <summary>
/// One problem with edited template content, addressed to a specific field and — where it makes sense —
/// a specific variable.
/// </summary>
public sealed record EmailTemplateIssue(
    string Field,
    string Code,
    string? VariableName,
    string MessageVi,
    string MessageEn,
    string Severity)
{
    public bool IsError => Severity == EmailTemplateIssueSeverity.Error;
}

/// <summary>
/// Validates edited template content against the one contract (G11-J). The editor calls it through the
/// API to render field-level messages; the update and restore handlers call it to decide whether to
/// save. Both use this class, so a save can never succeed on content the editor flagged, nor fail on
/// content it called clean.
///
/// <para>
/// It deliberately produces a LIST of coded issues rather than one sentence. The screen previously
/// showed "Một số biến chưa được định nghĩa hoặc sai định dạng" for every problem in every field, which
/// tells an operator neither which field nor which variable nor what to do — and the same sentence
/// appeared when nothing was wrong at all, because the list it checked against belonged to a different
/// template.
/// </para>
/// </summary>
public static class EmailTemplateContentValidator
{
    /// <summary>
    /// A placeholder-shaped fragment that the strict parser does NOT accept — an unclosed brace, a name
    /// starting with a digit, a name containing a hyphen or space. Detected separately so the operator is
    /// told "this is malformed" instead of the text silently surviving into a recipient's inbox.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex MalformedPattern =
        new(@"\{\{(?<inner>[^{}]{0,120}?)\}\}|\{\{(?<open>[^{}]{0,40})(?![^{}]*\}\})",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IReadOnlyList<EmailTemplateIssue> Validate(
        EmailTemplateContract contract,
        string? subjectVi, string? bodyVi, string? subjectEn, string? bodyEn)
    {
        var issues = new List<EmailTemplateIssue>();

        ValidateOneLanguage(contract, issues, EmailTemplateFields.SubjectVi, subjectVi, isSubject: true);
        ValidateOneLanguage(contract, issues, EmailTemplateFields.BodyVi, bodyVi, isSubject: false);
        ValidateOneLanguage(contract, issues, EmailTemplateFields.SubjectEn, subjectEn, isSubject: true);
        ValidateOneLanguage(contract, issues, EmailTemplateFields.BodyEn, bodyEn, isSubject: false);

        // Required variables are judged per language across subject+body together: a template may
        // legitimately put the OTP in the body and keep the subject plain, and it must anyway, because
        // a credential in a subject is refused outright a few lines above.
        RequireIn(contract, issues, EmailTemplateFields.BodyVi, subjectVi, bodyVi);
        RequireIn(contract, issues, EmailTemplateFields.BodyEn, subjectEn, bodyEn);

        return issues;
    }

    private static void ValidateOneLanguage(
        EmailTemplateContract contract, List<EmailTemplateIssue> issues,
        string field, string? content, bool isSubject)
    {
        if (string.IsNullOrEmpty(content)) return;

        var normalized = EmailTemplateVariables.NormalizeEncodedBraces(content);

        foreach (var name in EmailTemplateVariables.ExtractPlaceholders(normalized))
        {
            if (!EmailTemplateVariables.IsValidName(name))
            {
                // A name the parser matched but the naming rule rejects — PascalCase or snake_case left
                // over from an older template. Reported rather than silently accepted, because the
                // renderer will not resolve it and the recipient would see the raw braces.
                issues.Add(new EmailTemplateIssue(
                    field, EmailErrorCodes.TemplateVariableMalformed, name,
                    $"Biến {{{{{name}}}}} không đúng quy ước đặt tên (phải là camelCase, ví dụ fullName).",
                    $"Variable {{{{{name}}}}} does not follow the naming rule (camelCase, e.g. fullName).",
                    EmailTemplateIssueSeverity.Error));
                continue;
            }

            // A system block is judged as a BLOCK, before any variable rule can reach it. It is not a
            // variable, it is not absent from the variable catalog because somebody mistyped it, and
            // reporting it as an unknown variable told an operator that {{contactInformationBlock}} —
            // the very thing the contact settings had just required them to keep — "does not exist in
            // the system". The block lists are the authority here; the variable lists have no opinion.
            if (EmailTemplateContract.IsSystemBlock(name))
            {
                if (!contract.AllowsSystemBlock(name))
                {
                    issues.Add(new EmailTemplateIssue(
                        field, EmailErrorCodes.TemplateSystemBlockNotAllowed, name,
                        $"Khối hệ thống {{{{{name}}}}} không dùng được ở mẫu {contract.TemplateCode}; "
                        + "khi gửi sẽ không có gì thay thế vào chỗ này. Hãy xoá khối khỏi nội dung.",
                        $"System block {{{{{name}}}}} is not available on {contract.TemplateCode}; nothing "
                        + "would be substituted for it when sending. Remove it from the content.",
                        EmailTemplateIssueSeverity.Error));
                    continue;
                }

                if (isSubject)
                {
                    // A block is the only route by which markup — and therefore a live one-time URL —
                    // enters a message, and subjects are stored in sent_emails and shown in history.
                    issues.Add(new EmailTemplateIssue(
                        field, EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable, name,
                        $"Khối hệ thống {{{{{name}}}}} không được đặt trong tiêu đề: khối sinh ra HTML và "
                        + "có thể chứa liên kết dùng một lần, trong khi tiêu đề được lưu lại và hiển thị "
                        + "trong lịch sử email.",
                        $"System block {{{{{name}}}}} may not appear in a subject: a block produces HTML "
                        + "and may carry a one-time link, while subjects are stored and shown in the "
                        + "email history.",
                        EmailTemplateIssueSeverity.Error));
                }

                continue;
            }

            if (!contract.AllowedVariables.Contains(name, StringComparer.Ordinal))
            {
                var known = EmailVariableCatalog.Find(name) is not null;
                issues.Add(new EmailTemplateIssue(
                    field, EmailErrorCodes.TemplateVariableUnknown, name,
                    known
                        ? $"Biến {{{{{name}}}}} không thuộc mẫu {contract.TemplateCode}; khi gửi sẽ không có giá trị."
                        : $"Biến {{{{{name}}}}} không tồn tại trong hệ thống.",
                    known
                        ? $"Variable {{{{{name}}}}} does not belong to {contract.TemplateCode}; it would have no value when sent."
                        : $"Variable {{{{{name}}}}} does not exist in the system.",
                    EmailTemplateIssueSeverity.Error));
                continue;
            }

            if (isSubject && SensitiveEmailVariables.ForbiddenInSubject(name))
            {
                // A subject IS stored in sent_emails and shown in the history screen. A credential or a
                // one-time link placed there is persisted, backed up and readable long after the fact.
                issues.Add(new EmailTemplateIssue(
                    field, EmailErrorCodes.TemplateSubjectForbiddenSensitiveVariable, name,
                    $"Biến {{{{{name}}}}} không được đặt trong tiêu đề: tiêu đề được lưu lại và hiển thị trong lịch sử email.",
                    $"Variable {{{{{name}}}}} may not appear in a subject: subjects are stored and shown in the email history.",
                    EmailTemplateIssueSeverity.Error));
            }
        }

        foreach (var malformed in FindMalformed(normalized))
        {
            issues.Add(new EmailTemplateIssue(
                field, EmailErrorCodes.TemplateVariableMalformed, null,
                $"Đoạn \"{malformed}\" trông như một biến nhưng không đúng định dạng {{{{tenBien}}}}.",
                $"\"{malformed}\" looks like a variable but is not in the {{{{variableName}}}} form.",
                EmailTemplateIssueSeverity.Error));
        }
    }

    private static void RequireIn(
        EmailTemplateContract contract, List<EmailTemplateIssue> issues,
        string field, string? subject, string? body)
    {
        // An empty language is not a partial edit — it means this language is not maintained for this
        // template, and demanding required variables inside it would block every save.
        if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(body)) return;

        var present = EmailTemplateVariables.ExtractPlaceholders(
            EmailTemplateVariables.NormalizeEncodedBraces(subject ?? ""),
            EmailTemplateVariables.NormalizeEncodedBraces(body ?? ""));

        // Variables and blocks are required for different reasons and repaired by different people, but
        // "it must be in the body and it is not" is one check. Iterating both lists here is what keeps a
        // required block enforced now that it is no longer smuggled inside RequiredVariables.
        //
        // Distinct(): a required action block is already IN RequiredSystemBlocks — adding it again for
        // ActionRequired reported the identical error twice, so the operator saw the same sentence
        // stacked in the issue list and had no way to tell whether two different things were wrong.
        var requiredItems = contract.RequiredVariables
            .Concat(contract.RequiredSystemBlocks)
            .Concat(contract.ActionRequired ? new[] { EmailTrustedBlocks.ActionBlock } : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal);

        foreach (var required in requiredItems)
        {
            if (present.Contains(required)) continue;

            var isActionBlock = required == EmailTrustedBlocks.ActionBlock;
            var isTrustedBlock = EmailTrustedBlocks.All.Contains(required);

            var (vi, en) = required switch
            {
                _ when isActionBlock => (
                    "Mẫu này cần {{actionBlock}} — đây là khu vực nút thao tác do hệ thống gắn khi gửi. Bỏ nó đi thì người nhận không có nút nào để bấm.",
                    "This template needs {{actionBlock}} — the action area the system attaches when sending. Without it the recipient has no button to press."),

                EmailTrustedBlocks.SetupSummaryBlock => (
                    "Mẫu này cần {{setupSummaryBlock}} — đây là các bảng thông tin chuẩn bị do hệ thống dựng khi gửi. Bỏ nó đi thì email chỉ còn câu dẫn, không có nội dung cập nhật nào.",
                    "This template needs {{setupSummaryBlock}} — the setup tables the system builds when sending. Without it the mail is a covering sentence with no update in it."),

                EmailTrustedBlocks.ContactInformationBlock => (
                    "Mẫu này cần {{contactInformationBlock}} — đây là khối đầu mối liên hệ do hệ thống điền khi gửi. Nội dung email có câu bảo người nhận liên hệ, nên bỏ khối này đi thì họ được yêu cầu liên hệ mà không có địa chỉ nào. Nếu không muốn hiển thị, hãy đổi mức bắt buộc trong phần Cấu hình thông tin liên hệ.",
                    "This template needs {{contactInformationBlock}} — the reply-contact card the system fills in when sending. The text tells the recipient to get in touch, so without it they are asked to make contact and given no way to do so. To leave it out, change the requirement level in Contact information settings."),

                _ => (
                    $"Mẫu này bắt buộc phải chứa biến {{{{{required}}}}}; bỏ nó đi thì email gửi ra sẽ thiếu thông tin người nhận cần.",
                    $"This template must contain {{{{{required}}}}}; without it the email would go out missing what the recipient needs."),
            };

            // Each block reports under the code that names ITS repair. Lumping every trusted block under
            // "action block required" was survivable while the action block was the only one an operator
            // could delete; it stops being true the moment a second one exists, and the screen would tell
            // somebody who removed the contact card to go and restore a button.
            var code = required switch
            {
                _ when isActionBlock => EmailErrorCodes.TemplateActionBlockRequired,
                EmailTrustedBlocks.ContactInformationBlock =>
                    EmailErrorCodes.TemplateRequiredContactBlockNotInBody,
                EmailTrustedBlocks.SetupSummaryBlock => EmailErrorCodes.TemplateRequiredBlockNotInBody,
                _ when isTrustedBlock => EmailErrorCodes.TemplateRequiredBlockNotInBody,
                _ => EmailErrorCodes.TemplateRequiredVariableMissing,
            };

            issues.Add(new EmailTemplateIssue(
                field,
                code,
                required,
                vi,
                en,
                EmailTemplateIssueSeverity.Error));
        }
    }

    /// <summary>
    /// Finds brace-shaped fragments the strict placeholder parser will not match. Anything the parser
    /// DOES match is handled above, so this reports only the genuinely broken remainder.
    /// </summary>
    private static IEnumerable<string> FindMalformed(string content)
    {
        var wellFormed = EmailTemplateVariables.PlaceholderPattern.Matches(content)
            .Select(m => m.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (System.Text.RegularExpressions.Match m in MalformedPattern.Matches(content))
        {
            var value = m.Value;
            if (wellFormed.Contains(value)) continue;
            if (string.IsNullOrWhiteSpace(value)) continue;

            yield return value.Length > 40 ? value[..40] + "…" : value;
        }
    }
}
