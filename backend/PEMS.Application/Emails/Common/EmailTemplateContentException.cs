using System;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Thrown when edited template content is refused, carrying every problem at once rather than the first
/// one (G11-J).
///
/// <para>
/// It exists as its own type because <see cref="PEMS.Application.Common.Exceptions.ValidationException"/>
/// can express "which field" but not "which variable, and why, in both languages". An operator fixing a
/// template needs all three: the screen has four editable content fields, and a message that says only
/// "some variables are undefined or malformed" — which is what this replaces — leaves them hunting
/// through the whole template for something the backend already knows the exact position of.
/// </para>
///
/// <para>
/// It is deliberately NOT a conflict and NOT a business-rule failure: nothing about the system's state
/// is wrong, the submitted content is. It maps to 400 alongside the other validation failures.
/// </para>
/// </summary>
public sealed class EmailTemplateContentException : Exception
{
    public EmailTemplateContentException(IReadOnlyList<EmailTemplateIssue> issues)
        : base(BuildMessage(issues))
    {
        Issues = issues;
        // The single code is the FIRST error's, so a caller that only reads errorCode still gets
        // something specific. The full list is what the screen renders.
        ErrorCode = issues.FirstOrDefault(i => i.IsError)?.Code
                    ?? EmailErrorCodes.TemplateContentInvalid;
    }

    public IReadOnlyList<EmailTemplateIssue> Issues { get; }

    public string ErrorCode { get; }

    private static string BuildMessage(IReadOnlyList<EmailTemplateIssue> issues)
    {
        var errors = issues.Where(i => i.IsError).ToList();
        if (errors.Count == 0) return "Nội dung mẫu email không hợp lệ.";
        if (errors.Count == 1) return errors[0].MessageVi;

        return $"Nội dung mẫu email có {errors.Count} vấn đề cần sửa.";
    }
}
