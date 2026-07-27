using System;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Where the words of a MANUAL email come from: the person who typed them.
///
/// <para>
/// Manual compose, draft-send and reply are the one family of messages that is deliberately NOT rendered
/// from <c>email_templates</c> — the whole point is that the sender writes the sentences. That freedom is
/// exactly why the content still has to pass the same gate a Host's authored invitation passes: an email
/// body reaches other people's inboxes and the message subject reaches a header.
/// </para>
/// <para>
/// HTML content is delegated to <see cref="SystemEmailContent.AuthoredByUser"/> so there is ONE
/// implementation of "what may a person put in an email" rather than a second, drifting copy. The only
/// thing added here is the plain-text branch, which needs the same subject/marker rules but must not be
/// HTML-sanitised — running an HTML sanitiser over plain text silently eats characters like &lt; that a
/// plain-text writer is entitled to use.
/// </para>
/// </summary>
public static class ManualEmailContent
{
    /// <summary>Validated, ready-to-send manual content. Produced only by <see cref="Validate"/>.</summary>
    /// <param name="Subject">Trimmed subject, header-safe.</param>
    /// <param name="Body">Sanitised HTML, or the plain text as typed.</param>
    /// <param name="IsHtml">Whether <paramref name="Body"/> is HTML (drives the MIME content type).</param>
    public sealed record Result(string Subject, string Body, bool IsHtml);

    /// <summary>
    /// Validates and normalises what a person wrote, or throws with a stable code. Runs before any row is
    /// written and before anything reaches SMTP, so a rejected message leaves no trace.
    /// </summary>
    public static Result Validate(
        string? subject, string? body, EmailBodyFormat format, IHtmlSanitizerService sanitizer)
    {
        if (sanitizer is null) throw new ArgumentNullException(nameof(sanitizer));

        if (format == EmailBodyFormat.HTML)
        {
            var authored = SystemEmailContent.AuthoredByUser.Create(subject, body, sanitizer);
            return new Result(authored.Subject, authored.BodyHtml, IsHtml: true);
        }

        return new Result(ValidateSubject(subject), ValidatePlainTextBody(body), IsHtml: false);
    }

    /// <summary>
    /// Subject rules, shared by both formats: present, within the column limit, and free of the CR/LF that
    /// would let it open a second header.
    /// </summary>
    public static string ValidateSubject(string? subject)
    {
        var trimmed = subject?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            throw new ValidationException(
                "Tiêu đề email không được để trống.", EmailErrorCodes.AuthoredSubjectRequired);

        if (trimmed.Length > EmailOverrideLimits.SubjectMax)
            throw new ValidationException(
                $"Tiêu đề email tối đa {EmailOverrideLimits.SubjectMax} ký tự.",
                EmailErrorCodes.AuthoredSubjectTooLong);

        EmailRecipientValidator.AssertNoHeaderBreak(trimmed, "tiêu đề email do người dùng soạn");
        return trimmed;
    }

    /// <summary>
    /// Plain-text body rules. Same two prohibitions as the HTML path — the sender may not forge the
    /// action-block markers the history strip depends on, nor hand-write a trusted-block placeholder —
    /// but the text itself is kept verbatim rather than run through an HTML sanitiser.
    /// </summary>
    private static string ValidatePlainTextBody(string? body)
    {
        var raw = body ?? string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
            throw new ValidationException(
                "Nội dung email không được để trống.", EmailErrorCodes.AuthoredBodyRequired);

        if (raw.Length > EmailOverrideLimits.BodyMax)
            throw new ValidationException(
                $"Nội dung email vượt quá {EmailOverrideLimits.BodyMax} ký tự.",
                EmailErrorCodes.AuthoredBodyTooLong);

        AssertNoActionBlockForgery(raw);
        return raw;
    }

    /// <summary>
    /// Refuses a body carrying the canonical action-block markers or a <c>{{actionBlock}}</c> placeholder.
    /// Both are minted by the backend from a real one-time token: a sender-supplied one would either put a
    /// second set of buttons in the message or move the boundary that keeps one-time links out of
    /// <c>sent_emails.body_snapshot</c>.
    /// </summary>
    public static void AssertNoActionBlockForgery(string? raw)
    {
        if (EmailComposition.ContainsActionBlockMarker(raw))
            throw new ValidationException(
                "Nội dung email không được chứa dấu mốc khối hành động của hệ thống. " +
                "Các nút thao tác do hệ thống tự chèn khi gửi.",
                EmailErrorCodes.AuthoredActionBlockForbidden);

        var used = EmailTemplateVariables.ExtractPlaceholders(raw)
            .Where(EmailTrustedBlocks.All.Contains)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (used.Count > 0)
            throw new ValidationException(
                "Nội dung email không được tự chèn khối hành động: " +
                string.Join(", ", used.Select(n => "{{" + n + "}}")) + ".",
                EmailErrorCodes.AuthoredActionBlockForbidden);
    }
}
