using System;
using System.Linq;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// Where the WORDS of a system email come from. Everything else about the message — which template it
/// is, who receives it, whether copies are allowed, whether it carries a secret, how much of it the
/// history may keep, which action block is attached — is decided by the template registry and the
/// dispatcher, in both modes.
///
/// <para>
/// This is a closed hierarchy on purpose (private constructor, nested cases): a third content mode
/// cannot be introduced from outside this file, so "where did this subject come from?" always has one
/// of exactly two answers. The alternative that was rejected is a pair of nullable
/// <c>SubjectOverride</c>/<c>BodyOverride</c> fields — with those, the mode is inferred from which
/// combination happens to be null, every reader has to re-derive the rule, and "subject overridden but
/// body not" is a representable state nobody designed.
/// </para>
/// </summary>
public abstract record SystemEmailContent
{
    private SystemEmailContent() { }

    /// <summary>
    /// The default and the only source of content when nobody has edited anything: subject and body are
    /// read from <c>email_templates</c> on every send.
    /// </summary>
    public sealed record FromTemplate : SystemEmailContent
    {
        public static readonly FromTemplate Instance = new();
        private FromTemplate() { }
    }

    /// <summary>
    /// Content a person was authorised to write — today, a Host rewriting an invitation before sending
    /// it. The template is still resolved and still decides identity and policy; only the sentences are
    /// the author's.
    ///
    /// <para>
    /// The constructor is private and <see cref="Create"/> demands an <see cref="IHtmlSanitizerService"/>,
    /// so an instance of this type cannot exist without having been validated and sanitised. That is
    /// deliberate: it moves "did anyone remember to sanitise?" from a review question to a compile-time
    /// one. The properties are get-only, so a <c>with</c> expression cannot swap the checked content for
    /// something else afterwards.
    /// </para>
    /// </summary>
    public sealed record AuthoredByUser : SystemEmailContent
    {
        /// <summary>The author's subject line, trimmed. Never HTML.</summary>
        public string Subject { get; }

        /// <summary>The author's message, sanitised. Carries no action block — the backend adds that.</summary>
        public string BodyHtml { get; }

        private AuthoredByUser(string subject, string bodyHtml)
        {
            Subject = subject;
            BodyHtml = bodyHtml;
        }

        /// <summary>
        /// Validates and sanitises authored content, or throws. Runs before anything is rendered, sent or
        /// recorded, so a rejected edit leaves no trace anywhere.
        /// </summary>
        public static AuthoredByUser Create(string? subject, string? bodyHtml, IHtmlSanitizerService sanitizer)
        {
            if (sanitizer is null) throw new ArgumentNullException(nameof(sanitizer));

            var trimmedSubject = subject?.Trim() ?? string.Empty;
            if (trimmedSubject.Length == 0)
                throw new ValidationException(
                    "Tiêu đề email không được để trống.", EmailErrorCodes.AuthoredSubjectRequired);
            if (trimmedSubject.Length > EmailOverrideLimits.SubjectMax)
                throw new ValidationException(
                    $"Tiêu đề email tối đa {EmailOverrideLimits.SubjectMax} ký tự.",
                    EmailErrorCodes.AuthoredSubjectTooLong);

            // A subject becomes a header; a newline in it starts a second one.
            EmailRecipientValidator.AssertNoHeaderBreak(trimmedSubject, "tiêu đề email do người dùng soạn");

            var raw = bodyHtml ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                throw new ValidationException(
                    "Nội dung email không được để trống.", EmailErrorCodes.AuthoredBodyRequired);
            if (raw.Length > EmailOverrideLimits.BodyMax)
                throw new ValidationException(
                    $"Nội dung email vượt quá {EmailOverrideLimits.BodyMax} ký tự.",
                    EmailErrorCodes.AuthoredBodyTooLong);

            // ── The two things an author may never supply ──
            //
            // Both checks run on the RAW text, before sanitising, because the sanitiser drops HTML
            // comments: sanitising first would silently swallow a forged marker instead of reporting it,
            // and "we quietly removed your attempt" is not the same answer as "that is not allowed".
            AssertNoActionBlockMarker(raw);
            AssertNoTrustedBlockPlaceholder(trimmedSubject, raw);

            // Checked before sanitising too, and for the same reason: the sanitiser has no opinion about
            // a table's SHAPE. A nested table survives it intact, so leaving this until afterwards would
            // only mean refusing the same content one step later, with the author's own markup already
            // rewritten in the error they are shown.
            EmailTableRules.AssertUsable(raw);

            var sanitized = sanitizer.SanitizeEmailHtml(raw);
            if (string.IsNullOrWhiteSpace(sanitized))
                throw new ValidationException(
                    "Nội dung email không hợp lệ sau khi lọc.", EmailErrorCodes.AuthoredBodyRequired);

            return new AuthoredByUser(trimmedSubject, sanitized);
        }

        /// <summary>
        /// Refuses a body carrying the canonical action-block markers. Those markers are the boundary the
        /// history policy strips on: an author who could write one could wrap the explanatory text in it
        /// (deleting the record) or close it early (leaving the real accept link outside the strip and
        /// therefore inside <c>body_snapshot</c>). Neither is a formatting mistake to clean up quietly.
        /// </summary>
        private static void AssertNoActionBlockMarker(string raw)
        {
            if (!EmailComposition.ContainsActionBlockMarker(raw)) return;

            throw new ValidationException(
                "Nội dung email không được chứa dấu mốc khối hành động của hệ thống. " +
                "Các nút thao tác do hệ thống tự chèn khi gửi.",
                EmailErrorCodes.AuthoredActionBlockForbidden);
        }

        /// <summary>
        /// Refuses <c>{{actionBlock}}</c> (or any future trusted block) written by hand. The backend
        /// appends the real block itself; letting an author place a second one would put two accept
        /// buttons, minted from the same token, in one message.
        /// </summary>
        private static void AssertNoTrustedBlockPlaceholder(string subject, string body)
        {
            var used = EmailTemplateVariables.ExtractPlaceholders(subject, body)
                .Where(EmailTrustedBlocks.All.Contains)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            if (used.Count == 0) return;

            throw new ValidationException(
                "Nội dung email không được tự chèn khối hành động: " +
                string.Join(", ", used.Select(n => "{{" + n + "}}")) + ".",
                EmailErrorCodes.AuthoredActionBlockForbidden);
        }
    }
}
