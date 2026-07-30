using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The single place TO/CC/BCC lists are checked, for every path that sends mail — system callers, manual
/// compose, drafts and replies alike.
///
/// Having one implementation matters beyond tidiness: the draft screen, the send handler and the reply
/// handler each used to apply their own (or no) rules, so a list that a draft accepted could still fail
/// or, worse, silently send wrong at the point of dispatch. Everything below is enforced before a message
/// reaches SMTP.
/// </summary>
public static class EmailRecipientValidator
{
    /// <summary>Characters that would let a value break out of its header and inject another one.</summary>
    private static readonly char[] HeaderBreakers = { '\r', '\n', '\0' };

    /// <summary>
    /// Validates the whole envelope and returns it normalised (trimmed, empty entries dropped). Throws on
    /// the first rule broken, using the stable codes in <see cref="EmailErrorCodes"/>.
    /// </summary>
    /// <param name="requireTo">
    /// False only for the rare caller that legitimately builds an envelope in stages. Dispatch itself
    /// always requires a TO.
    /// </param>
    public static ValidatedEnvelope Validate(
        IReadOnlyList<EmailRecipient>? to,
        IReadOnlyList<EmailRecipient>? cc,
        IReadOnlyList<EmailRecipient>? bcc,
        int maxRecipients,
        bool requireTo = true)
    {
        var toList = Normalize(to, EmailRecipientTypes.To);
        var ccList = Normalize(cc, EmailRecipientTypes.Cc);
        var bccList = Normalize(bcc, EmailRecipientTypes.Bcc);

        if (requireTo && toList.Count == 0)
            throw new ValidationException(
                "Email phải có ít nhất một người nhận ở mục Đến.", EmailErrorCodes.RecipientRequired);

        var total = toList.Count + ccList.Count + bccList.Count;
        if (total > maxRecipients)
            throw new ValidationException(
                $"Tổng số người nhận ({total}) vượt quá giới hạn cho phép ({maxRecipients}).",
                EmailErrorCodes.RecipientLimitExceeded);

        // Cross-group duplicates are checked before within-group ones only because the message is more
        // specific; both are rejected either way.
        AssertNoCrossGroupDuplicate(toList, ccList, EmailRecipientTypes.To, EmailRecipientTypes.Cc);
        AssertNoCrossGroupDuplicate(toList, bccList, EmailRecipientTypes.To, EmailRecipientTypes.Bcc);
        AssertNoCrossGroupDuplicate(ccList, bccList, EmailRecipientTypes.Cc, EmailRecipientTypes.Bcc);

        return new ValidatedEnvelope(toList, ccList, bccList);
    }

    /// <summary>Convenience overload for a whole message.</summary>
    public static ValidatedEnvelope Validate(OutboundEmail message, int maxRecipients)
        => Validate(message.To, message.Cc, message.Bcc, maxRecipients);

    /// <summary>
    /// Trims, drops blanks, and rejects malformed addresses, header-breaking characters and duplicates
    /// within the group. Original casing is preserved in the returned list — only the comparison is
    /// case-insensitive, because "Ha.Nguyen@fpt.edu.vn" is the same mailbox as "ha.nguyen@fpt.edu.vn"
    /// but is not the same thing to show the user.
    /// </summary>
    private static List<EmailRecipient> Normalize(IReadOnlyList<EmailRecipient>? source, string groupLabel)
    {
        var result = new List<EmailRecipient>();
        if (source is null || source.Count == 0) return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in source)
        {
            if (raw is null) continue;

            var email = raw.Email?.Trim() ?? string.Empty;
            if (email.Length == 0) continue;

            var displayName = string.IsNullOrWhiteSpace(raw.DisplayName) ? null : raw.DisplayName!.Trim();

            AssertNoHeaderBreak(email, groupLabel);
            if (displayName is not null) AssertNoHeaderBreak(displayName, groupLabel);

            if (!IsWellFormed(email))
                throw new ValidationException(
                    $"Địa chỉ email không hợp lệ ở mục {groupLabel}: '{email}'.",
                    EmailErrorCodes.RecipientInvalid);

            if (!seen.Add(email))
                throw new ValidationException(
                    $"Địa chỉ '{email}' bị lặp trong cùng mục {groupLabel}.",
                    EmailErrorCodes.RecipientDuplicate);

            result.Add(new EmailRecipient(email, displayName));
        }

        return result;
    }

    private static void AssertNoCrossGroupDuplicate(
        List<EmailRecipient> first, List<EmailRecipient> second, string firstLabel, string secondLabel)
    {
        if (first.Count == 0 || second.Count == 0) return;

        var firstSet = first.Select(r => r.NormalizedEmail).ToHashSet(StringComparer.Ordinal);
        var clash = second.FirstOrDefault(r => firstSet.Contains(r.NormalizedEmail));

        if (clash is not null)
            throw new ValidationException(
                $"Địa chỉ '{clash.Email}' xuất hiện ở cả mục {firstLabel} và {secondLabel}. " +
                "Mỗi người nhận chỉ được thuộc một mục.",
                EmailErrorCodes.RecipientCrossGroupDuplicate);
    }

    /// <summary>
    /// Rejects CR/LF/NUL anywhere a value will be written into a message header. Without this an address
    /// like <c>"a@b.com\r\nBcc: attacker@evil.com"</c> becomes a second header line.
    /// </summary>
    public static void AssertNoHeaderBreak(string value, string what)
    {
        if (value.IndexOfAny(HeaderBreakers) >= 0)
            throw new ValidationException(
                $"Giá trị của {what} chứa ký tự xuống dòng không hợp lệ.",
                EmailErrorCodes.HeaderInvalid);
    }

    /// <summary>
    /// Headers the pipeline owns, which a caller may therefore never supply through
    /// <c>OutboundEmail.Headers</c>.
    ///
    /// <para>
    /// Two kinds are on this list, and both matter. <b>Identity</b> — From, Sender, Reply-To, Return-Path
    /// — decides who the message claims to be from and where bounces go; it comes from configuration, and
    /// a caller that could set it could send mail as anyone. <b>Envelope</b> — To, Cc, Bcc, Message-Id —
    /// is derived from the typed recipient lists and the message identity written to <c>sent_emails</c>; a
    /// header-bag copy could disagree with the row, which is how history stops describing what was sent.
    /// </para>
    /// <para>
    /// <c>In-Reply-To</c> and <c>References</c> are deliberately NOT here: they are what the bag exists
    /// for, they name a parent message rather than an identity, and threading breaks without them.
    /// </para>
    /// <para>
    /// Measured before this existed, on a real message serialised to disk: a From supplied through the bag
    /// was overwritten by configuration, Sender and Reply-To were dropped by the framework, and
    /// <c>Return-Path</c> survived into the file. So one of the four was genuinely settable, and the other
    /// three depended on framework behaviour nobody had written down. Refusing all of them makes the rule
    /// the code's rather than the library's.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> ReservedHeaderNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "From", "Sender", "Reply-To", "Return-Path",
            "To", "Cc", "Bcc", "Message-Id",
        };

    /// <summary>
    /// Refuses a caller-supplied header that the pipeline owns. Called for every entry in the bag, before
    /// anything is handed to the transport.
    /// </summary>
    public static void AssertHeaderNameAllowed(string name)
    {
        if (ReservedHeaderNames.Contains(name.Trim()))
            throw new ValidationException(
                $"Header '{name}' do hệ thống quyết định, không nhận từ nơi gọi.",
                EmailErrorCodes.HeaderInvalid);
    }

    /// <summary>
    /// Well-formed means: parseable as a mailbox, exactly one '@', and a dotted domain.
    /// <see cref="MailAddress"/> alone accepts things like "a@b" that no real mail server will route.
    /// </summary>
    public static bool IsWellFormed(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (email.Count(c => c == '@') != 1) return false;

        var at = email.IndexOf('@');
        var local = email[..at];
        var domain = email[(at + 1)..];

        if (local.Length == 0 || domain.Length < 3) return false;
        if (!domain.Contains('.')) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..")) return false;

        return MailAddress.TryCreate(email, out _);
    }
}

/// <summary>A normalised, rule-checked envelope. Produced only by <see cref="EmailRecipientValidator"/>.</summary>
public sealed record ValidatedEnvelope(
    IReadOnlyList<EmailRecipient> To,
    IReadOnlyList<EmailRecipient> Cc,
    IReadOnlyList<EmailRecipient> Bcc)
{
    public int Total => To.Count + Cc.Count + Bcc.Count;
    public bool HasCopies => Cc.Count > 0 || Bcc.Count > 0;
}
