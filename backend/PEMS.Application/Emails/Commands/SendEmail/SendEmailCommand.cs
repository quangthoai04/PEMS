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
public class SendEmailCommand : IRequest<SendEmailResponse>
{
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
}

public class EmailRecipientDto
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
}
