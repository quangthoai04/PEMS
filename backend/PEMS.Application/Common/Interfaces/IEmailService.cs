using System;
using System.Collections.Generic;
using System.Linq;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// A ready-to-send email with its full envelope and optional attachments / inline images. The caller has
/// already resolved each file's bytes (e.g. via <see cref="IFileStorageService"/>) and, for system mail,
/// has already rendered the body from <c>email_templates</c> — so the email service stays free of
/// DB/storage concerns and only builds and dispatches MIME.
///
/// <para>
/// <b>The envelope is three separate lists.</b> The previous shape had a single <c>ToEmail</c> string, so
/// every CC and BCC the UI collected was either dropped or silently demoted to TO. Keeping TO/CC/BCC
/// apart here is what lets the SMTP layer put CC in the <c>Cc</c> header and BCC only in the SMTP
/// envelope, where the other recipients cannot see it.
/// </para>
/// </summary>
public sealed class OutboundEmail
{
    private IReadOnlyList<EmailRecipient> _to = Array.Empty<EmailRecipient>();

    /// <summary>Visible primary recipients. At least one is required.</summary>
    public IReadOnlyList<EmailRecipient> To
    {
        get => _to;
        init => _to = value ?? Array.Empty<EmailRecipient>();
    }

    /// <summary>
    /// Carbon copies — visible to every other recipient in the <c>Cc</c> header. Forbidden for templates
    /// whose policy is single-recipient (OTP, one-time tokens, personalised action links).
    /// </summary>
    public IReadOnlyList<EmailRecipient> Cc { get; init; } = Array.Empty<EmailRecipient>();

    /// <summary>
    /// Blind copies. They receive the message but must never appear in any header the TO/CC recipients
    /// can read. Forbidden for single-recipient templates.
    /// </summary>
    public IReadOnlyList<EmailRecipient> Bcc { get; init; } = Array.Empty<EmailRecipient>();

    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;

    /// <summary>True = HTML body (text/html), false = PLAIN_TEXT.</summary>
    public bool IsHtml { get; init; } = true;

    public IReadOnlyList<OutboundAttachment> Attachments { get; init; } = Array.Empty<OutboundAttachment>();

    /// <summary>
    /// The system template this message was rendered from, when it is system mail. The dispatcher uses it
    /// to re-check the recipient policy itself rather than trusting the caller to have done so.
    /// Null for user-authored mail (compose / draft / reply).
    /// </summary>
    public string? TemplateCode { get; init; }

    /// <summary>Overrides the configured Reply-To for this message only.</summary>
    public EmailRecipient? ReplyTo { get; init; }

    /// <summary>
    /// Extra headers to preserve a mail thread (<c>In-Reply-To</c>, <c>References</c>). Values are
    /// header-validated like any other address/subject; no header may contain CR/LF.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Every addressee across all three groups, in TO → CC → BCC order.</summary>
    public IEnumerable<EmailRecipient> AllRecipients => To.Concat(Cc).Concat(Bcc);

    /// <summary>Total addressee count across all three groups.</summary>
    public int RecipientCount => To.Count + Cc.Count + Bcc.Count;

    /// <summary>
    /// Single-address convenience shim kept ONLY while callers are migrated onto <see cref="To"/>.
    /// It writes through to the same backing list, so setting it is equivalent to a one-element
    /// <see cref="To"/>. Do not set both on one object.
    /// </summary>
    [Obsolete("Use To = new[] { new EmailRecipient(address) }. This shim is removed once every caller is migrated.")]
    public string ToEmail
    {
        get => _to.Count > 0 ? _to[0].Email : string.Empty;
        init => _to = string.IsNullOrWhiteSpace(value)
            ? Array.Empty<EmailRecipient>()
            : new[] { new EmailRecipient(value) };
    }
}

/// <summary>
/// One attachment. <see cref="IsInline"/> images are referenced from the HTML body via
/// <c>&lt;img src="cid:{ContentId}"&gt;</c> and become MIME linked resources; plain attachments show
/// in the recipient's client as downloadable files.
/// </summary>
public sealed class OutboundAttachment
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = "attachment";
    public string? ContentType { get; init; }
    public bool IsInline { get; init; }
    /// <summary>Content-ID for inline images (required when <see cref="IsInline"/> is true).</summary>
    public string? ContentId { get; init; }
}

/// <summary>
/// Outbound email transport. When no SMTP server is configured the implementation records the message
/// instead of sending — so a flow never breaks in dev — but it reports that truthfully as
/// <see cref="EmailDeliveryStatus.Skipped"/> and never as "sent".
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends one MIME message to the whole envelope (TO + CC + BCC together) and returns the TRUTHFUL
    /// outcome without throwing. Callers that PERSIST a delivery status MUST use this and map the outcome
    /// faithfully — never record "sent" for a Skipped/Failed result, and never set a sent-timestamp on a
    /// non-Sent outcome.
    /// </summary>
    Task<EmailDeliveryResult> TrySendAsync(OutboundEmail message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Same dispatch as <see cref="TrySendAsync(OutboundEmail,CancellationToken)"/> but throws
    /// <c>EmailDeliveryException</c> on a hard failure, for callers that treat sending as mandatory.
    /// </summary>
    Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default);

    // ── Legacy surface — removed once Giai đoạn 4 finishes migrating callers ──

    /// <summary>Generic send — caller provides the full HTML body.</summary>
    [Obsolete("Render from email_templates and use SendAsync(OutboundEmail). Removed after the caller migration.")]
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>Truthful variant of the legacy single-address send.</summary>
    [Obsolete("Render from email_templates and use TrySendAsync(OutboundEmail). Removed after the caller migration.")]
    Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    // The two OTP-composing methods that used to live here are gone: SendPasswordResetAsync in Batch 2
    // and SendVisitRequestOtpAsync in Batch 3, once AUTH_PASSWORD_RESET_OTP and VISIT_REQUEST_OTP took
    // over their content. They are deleted rather than deprecated — a method that writes a code into an
    // HTML string is a second source of a message the template screen is supposed to own, and that is
    // exactly how the account mails drifted apart before this work started.
}
