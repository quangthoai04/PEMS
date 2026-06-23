using MediatR;

using System.Collections.Generic;

namespace PEMS.Application.Emails.Commands.SendEmail;

public class SendEmailCommand : IRequest<SendEmailResponse>
{
    public int? TemplateId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<EmailRecipientDto> To { get; set; } = new();
}

public class EmailRecipientDto
{
    public string Email { get; set; } = string.Empty;
}