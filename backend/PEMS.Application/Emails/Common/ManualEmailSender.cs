using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PEMS.Application.Common;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Enums;

namespace PEMS.Application.Emails.Common;

/// <summary>
/// The one pipeline every MANUAL email goes through — compose, draft-send and reply alike.
///
/// <para>
/// <b>One message, not one per recipient.</b> All three handlers used to loop the recipient list and call
/// SMTP once per address. That produced N separate messages: a CC recipient could not see who else was
/// addressed (so a "carbon copy" was indistinguishable from a direct one), each copy carried its own
/// provider identity, and the reply handler wrote CC/BCC rows it then never sent to at all — a history
/// that claimed delivery to people who received nothing. Here the envelope is handed to
/// <see cref="IEmailService"/> once and becomes exactly one MIME message, so what the database says and
/// what the recipients received are the same thing.
/// </para>
/// <para>
/// <b>The SMTP call is deliberately outside a database transaction.</b> A transaction cannot span a
/// network send: holding one open across SMTP either pins a connection for the duration of an external
/// call or, worse, rolls back rows describing a message the provider has already accepted. Instead the
/// history row is committed first, the send is attempted, and the outcome is written back — so a crash in
/// the middle leaves a row that says QUEUED, which is true, rather than nothing at all.
/// </para>
/// <para>
/// <b>Status is what happened.</b> Provider acceptance is <c>SENT</c>; a skipped send (SMTP off outside
/// production) stays <c>QUEUED</c> because it never reached a provider; a provider error is
/// <c>FAILED</c> with a safe message. Nothing here ever writes <c>DELIVERED</c> or a
/// <c>delivered_at</c> — PEMS has no delivery webhook to learn that from — and no exception text ever
/// reaches the database.
/// </para>
/// </summary>
public interface IManualEmailSender
{
    Task<ManualEmailResult> SendAsync(ManualEmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>One attachment on a manual email: the row to record plus the bytes to send.</summary>
public sealed record ManualEmailAttachment(
    ulong FileId,
    EmailAttachmentType Type,
    string? ContentId,
    string? DisplayName,
    uint DisplayOrder,
    OutboundAttachment Outbound);

/// <summary>
/// A validated manual email ready to record and send. <paramref name="Envelope"/> must come from
/// <see cref="EmailRecipientValidator"/> and <paramref name="Subject"/>/<paramref name="Body"/> from
/// <see cref="ManualEmailContent"/> — this class assembles and dispatches, it does not re-author.
/// </summary>
/// <param name="InReplyTo">The message being replied to, when this is a reply. Drives the threading headers.</param>
public sealed record ManualEmailMessage(
    ulong SenderUserId,
    string Subject,
    string Body,
    EmailBodyFormat BodyFormat,
    ValidatedEnvelope Envelope,
    IReadOnlyList<ManualEmailAttachment> Attachments,
    string? RelatedType = null,
    ulong? RelatedId = null,
    SentEmail? InReplyTo = null);

/// <summary>The recorded outcome. <paramref name="Success"/> is true only for a real provider acceptance.</summary>
public sealed record ManualEmailResult(ulong SentEmailId, string Status, bool Success, string Message);

public sealed class ManualEmailSender : IManualEmailSender
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailService _email;

    public ManualEmailSender(IApplicationDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<ManualEmailResult> SendAsync(
        ManualEmailMessage message, CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now();

        // The id we put in the outgoing message's Message-Id header and store as provider_message_id. It
        // is generated here rather than left to the SMTP server because a reply has to be able to point at
        // its parent: without an id we control, In-Reply-To/References cannot be written truthfully at all.
        var messageId = NewMessageId();

        // A reply belongs to the thread its parent already belongs to; only a parent that started a thread
        // contributes its own id. Nothing is invented when the parent has neither.
        var threadId = message.InReplyTo is { } parent
            ? (string.IsNullOrWhiteSpace(parent.ProviderThreadId) ? parent.ProviderMessageId : parent.ProviderThreadId)
            : messageId;

        // ── 1. Record the message BEFORE attempting delivery ──────────────────
        //
        // email_template_id is NULL by design: this content was typed by a person, so pointing the row at
        // a system template would make the history claim the message came from email_templates and would
        // hand it that template's sensitivity policy.
        var sentEmail = new SentEmail
        {
            EmailTemplateId = null,
            RelatedType = message.RelatedType,
            RelatedId = message.RelatedId,
            Subject = message.Subject,
            BodySnapshot = message.Body,
            BodyFormat = message.BodyFormat,
            ProviderMessageId = messageId,
            ProviderThreadId = threadId,
            Status = "QUEUED",
            SentBy = message.SenderUserId,
            CreatedAt = now,
            LastAttemptAt = now,
        };

        AddRecipients(sentEmail, message.Envelope.To, EmailRecipientTypes.To, now);
        AddRecipients(sentEmail, message.Envelope.Cc, EmailRecipientTypes.Cc, now);
        AddRecipients(sentEmail, message.Envelope.Bcc, EmailRecipientTypes.Bcc, now);

        foreach (var a in message.Attachments)
        {
            sentEmail.Attachments.Add(new SentEmailAttachment
            {
                FileId = a.FileId,
                AttachmentType = a.Type,
                ContentId = a.ContentId,
                DisplayName = a.DisplayName,
                DisplayOrder = a.DisplayOrder,
                CreatedAt = now,
            });
        }

        _db.SentEmails.Add(sentEmail);
        await _db.SaveChangesAsync(cancellationToken);

        // ── 2. One MIME message for the whole envelope ────────────────────────
        var delivery = await _email.TrySendAsync(new OutboundEmail
        {
            To = message.Envelope.To,
            Cc = message.Envelope.Cc,
            Bcc = message.Envelope.Bcc,
            Subject = message.Subject,
            Body = message.Body,
            IsHtml = message.BodyFormat == EmailBodyFormat.HTML,
            Attachments = message.Attachments.Select(a => a.Outbound).ToList(),
            // No TemplateCode: manual mail has no template policy to re-check. CC/BCC are the sender's
            // choice here, which is exactly the freedom the single-recipient system templates deny.
            MessageId = messageId,
            Headers = BuildThreadHeaders(message.InReplyTo),
        }, cancellationToken);

        // ── 3. Write back what actually happened ──────────────────────────────
        var completedAt = VietnamTime.Now();
        sentEmail.LastAttemptAt = completedAt;

        switch (delivery.Status)
        {
            case EmailDeliveryStatus.Sent:
                sentEmail.Status = "SENT";
                sentEmail.SentAt = completedAt;
                sentEmail.ErrorMessage = null;
                SetRecipientOutcome(sentEmail, "SENT", completedAt, error: null);
                break;

            case EmailDeliveryStatus.Failed:
                sentEmail.Status = "FAILED";
                sentEmail.ErrorMessage = delivery.SafeMessage;
                SetRecipientOutcome(sentEmail, "FAILED", sentAt: null, error: delivery.SafeMessage);
                break;

            default: // Skipped — never handed to a provider, so it is still queued, not sent and not failed.
                sentEmail.Status = "QUEUED";
                sentEmail.ErrorMessage = delivery.SafeMessage;
                SetRecipientOutcome(sentEmail, "QUEUED", sentAt: null, error: null);
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ManualEmailResult(
            sentEmail.SentEmailId,
            sentEmail.Status,
            delivery.Status == EmailDeliveryStatus.Sent,
            DescribeOutcome(delivery.Status));
    }

    private static void AddRecipients(
        SentEmail sentEmail, IReadOnlyList<EmailRecipient> group, string type, DateTime now)
    {
        foreach (var r in group)
        {
            sentEmail.Recipients.Add(new SentEmailRecipient
            {
                RecipientEmail = r.Email,
                RecipientName = r.DisplayName,
                RecipientType = type,
                DeliveryStatus = "QUEUED",
                CreatedAt = now,
            });
        }
    }

    /// <summary>
    /// One message, one outcome: every addressee shares the fate of the single send, because there was a
    /// single send. Recording per-recipient results would imply a per-recipient attempt that never happened.
    /// </summary>
    private static void SetRecipientOutcome(
        SentEmail sentEmail, string status, DateTime? sentAt, string? error)
    {
        foreach (var r in sentEmail.Recipients)
        {
            r.DeliveryStatus = status;
            r.SentAt = sentAt;
            r.ErrorMessage = error;
            r.ProviderMessageId = sentEmail.ProviderMessageId;
            // delivered_at stays NULL: acceptance by a provider is not delivery to a mailbox.
        }
    }

    /// <summary>
    /// <c>In-Reply-To</c>/<c>References</c>, and only when the parent really has an id to point at. A
    /// reply to a message sent before ids were recorded simply carries no threading headers rather than a
    /// fabricated one.
    ///
    /// <para>
    /// <c>Message-Id</c> used to be built here too. It moved to <c>OutboundEmail.MessageId</c> when the
    /// header bag stopped accepting anything that decides a message's identity — the id belongs to the
    /// message, not to a bag of thread references a caller assembles.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildThreadHeaders(SentEmail? parent)
    {
        var headers = new Dictionary<string, string>();

        if (parent is null || string.IsNullOrWhiteSpace(parent.ProviderMessageId))
            return headers;

        headers["In-Reply-To"] = parent.ProviderMessageId!;
        headers["References"] = string.IsNullOrWhiteSpace(parent.ProviderThreadId)
            || string.Equals(parent.ProviderThreadId, parent.ProviderMessageId, StringComparison.Ordinal)
                ? parent.ProviderMessageId!
                : parent.ProviderThreadId + " " + parent.ProviderMessageId;

        return headers;
    }

    /// <summary>
    /// An RFC 5322 message identifier. The domain is a fixed literal rather than the configured sending
    /// domain: uniqueness is what threading needs, and reading SMTP configuration here would put transport
    /// concerns back into the application layer that <see cref="IEmailService"/> exists to keep them out of.
    /// </summary>
    private static string NewMessageId() => $"<{Guid.NewGuid():N}@pems.local>";

    private static string DescribeOutcome(EmailDeliveryStatus status) => status switch
    {
        EmailDeliveryStatus.Sent => "Đã gửi email thành công.",
        EmailDeliveryStatus.Failed => "Gửi email thất bại. Vui lòng thử lại hoặc liên hệ quản trị hệ thống.",
        _ => "Hệ thống chưa bật gửi email ở môi trường này nên email chưa được gửi đi.",
    };
}
