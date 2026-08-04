using MediatR;

using System.Collections.Generic;

namespace PEMS.Application.Emails.Commands.SendEmail;

/// <summary>
/// Manual compose: the sender writes the subject and the body themselves.
///
/// <para>
/// The three recipient groups are three separate lists. They used to be one <c>To</c>, so a CC or BCC the
/// screen collected had nowhere to go — the command could not even express the difference, let alone
/// preserve it through to the message.
/// </para>
/// </summary>
public class SendEmailCommand : IRequest<SendEmailResponse>, PEMS.Application.Emails.Idempotency.IIdempotentEmailSend
{
    public string OperationCode => PEMS.Application.Emails.Idempotency.EmailSendOperations.ManualCompose;

    /// <summary>
    /// What makes this compose request itself: the subject, the body, and the three recipient groups
    /// normalised to addresses only (see <c>Recipients</c>). A retry of the same click is recognised;
    /// adding or removing an address is not, and is refused rather than answered "already sent".
    /// </summary>
    public void DescribeRequest(PEMS.Application.Emails.Idempotency.EmailSendFingerprintBuilder builder) =>
        builder.Id("template", TemplateId is null ? null : (ulong)TemplateId.Value)
               .Text("subject", Subject)
               .Text("body", Body)
               .Recipients("to", To?.Select(r => r?.Email ?? string.Empty))
               .Recipients("cc", Cc?.Select(r => r?.Email ?? string.Empty))
               .Recipients("bcc", Bcc?.Select(r => r?.Email ?? string.Empty))
               // Attachments are part of what the message IS. Left out, adding or removing a file and
               // pressing send again under the same key would be answered "already sent" — and the
               // message that actually went out would be the one WITHOUT the change.
               .Text("attachments", Attachments is null
                   ? null
                   : string.Join(",", Attachments.Where(a => a is not null)
                       .Select(a => $"{a.FileId}:{a.AttachmentType}:{a.ContentId}")));

    /// <summary>
    /// The template the composer started from, if any. Recorded nowhere on the sent message: once a person
    /// has edited the text, the message is theirs, and pointing <c>sent_emails.email_template_id</c> at a
    /// system template would claim the content came from <c>email_templates</c>.
    /// </summary>
    public int? TemplateId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>PLAIN_TEXT | HTML. Defaults to HTML, which is what the composer produces.</summary>
    public string? BodyFormat { get; set; }

    /// <summary>Visible primary recipients. At least one is required.</summary>
    public List<EmailRecipientDto> To { get; set; } = new();

    /// <summary>Carbon copies — visible to every other recipient.</summary>
    public List<EmailRecipientDto> Cc { get; set; } = new();

    /// <summary>Blind copies — received by them, shown to nobody else.</summary>
    public List<EmailRecipientDto> Bcc { get; set; } = new();

    /// <summary>
    /// Files and inline images, by <c>file_id</c>, uploaded before the send.
    ///
    /// <para>
    /// New here. Attachments used to reach a message only by being written onto a draft row, so this
    /// command hard-coded an empty list — the composer could attach a document and the generic send
    /// silently posted the message without it. Every file is re-checked against the database at send time
    /// (ownership, size, mime, readable bytes) and one unreadable file refuses the whole send.
    /// </para>
    /// </summary>
    public List<PEMS.Application.Emails.Common.EmailComposeAttachmentInput> Attachments { get; set; } = new();

    /// <summary>
    /// What this message is ABOUT, for the history — a visit instance, a logistics request. Defaults to
    /// GENERAL, which is what a message composed from the mailbox screen is.
    /// </summary>
    public string? RelatedType { get; set; }

    public ulong? RelatedId { get; set; }
}

public class EmailRecipientDto
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}
