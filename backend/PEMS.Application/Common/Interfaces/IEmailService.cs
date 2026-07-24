using System.Collections.Generic;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// A ready-to-send email with optional attachments / inline images. The caller has already resolved
/// each file's bytes (e.g. via <see cref="IFileStorageService"/>) so the email service stays free of
/// DB/storage concerns and only builds MIME.
/// </summary>
public sealed class OutboundEmail
{
    public string ToEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    /// <summary>True = HTML body (text/html), false = PLAIN_TEXT.</summary>
    public bool IsHtml { get; init; } = true;
    public IReadOnlyList<OutboundAttachment> Attachments { get; init; } = new List<OutboundAttachment>();
}

/// <summary>
/// One attachment. <see cref="IsInline"/> images are referenced from the HTML body via
/// <c>&lt;img src="cid:{ContentId}"&gt;</c> and become MIME linked resources; plain attachments show
/// in the recipient's client as downloadable files.
/// </summary>
public sealed class OutboundAttachment
{
    public byte[] Content { get; init; } = System.Array.Empty<byte>();
    public string FileName { get; init; } = "attachment";
    public string? ContentType { get; init; }
    public bool IsInline { get; init; }
    /// <summary>Content-ID for inline images (required when <see cref="IsInline"/> is true).</summary>
    public string? ContentId { get; init; }
}

/// <summary>
/// Outbound email. When no SMTP server is configured the implementation logs
/// the message instead of sending — so the auth flow never breaks in dev.
/// </summary>
public interface IEmailService
{
    /// <summary>Generic send — caller provides the full HTML body.</summary>
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to send an email and returns the TRUTHFUL delivery outcome (Sent / Skipped / Failed)
    /// instead of throwing. Callers that PERSIST a delivery status MUST use this and map the outcome
    /// faithfully — never record "sent" for a Skipped/Failed result, and never set a sent-timestamp on
    /// a non-Sent outcome. The void <see cref="SendAsync(string,string,string,CancellationToken)"/>
    /// remains for fire-and-forget callers (it throws on a hard failure so the caller still observes it).
    /// </summary>
    Task<EmailDeliveryResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rich send: builds a real MIME message with the given body format, file attachments and inline
    /// (cid) images. Used by the email rich-editor send flows (drafts, participant invite, logistics).
    /// </summary>
    Task SendAsync(OutboundEmail message, CancellationToken cancellationToken = default);

    /// <summary>Sends a password-reset / forgot-password OTP email.</summary>
    Task SendPasswordResetAsync(string toEmail, string fullName, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the 6-digit OTP email for visit-request email verification.
    /// </summary>
    Task SendVisitRequestOtpAsync(
        string toEmail,
        string fullName,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the submission-confirmed email after OTP passes and the request is created.
    /// Includes the request code, pending-approval message, and the provisioned account email.
    /// Sent to the contact person.
    /// </summary>
    Task SendVisitorAccountCreatedOrLinkedEmailAsync(
        string toEmail,
        string contactFullName,
        string delegationName,
        string requestCode,
        string visitScope,
        string plannedTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a short confirmation email to the registrant if they are not the contact person.
    /// </summary>
    Task SendRegistrantConfirmationAsync(
        string toEmail,
        string registrantFullName,
        string contactFullName,
        string contactEmail,
        string delegationName,
        string requestCode,
        CancellationToken cancellationToken = default);
}
