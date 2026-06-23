using MediatR;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Commands.ReplytoEmail;

public class ReplytoEmailCommand : IRequest<ReplytoEmailResponse>
{
    public ulong OriginalEmailId { get; set; }
    public string Body { get; set; } = null!;
    public List<EmailRecipientInput>? Cc { get; set; }
    public List<EmailRecipientInput>? Bcc { get; set; }
}

public class EmailRecipientInput
{
    public string Email { get; set; } = null!;
    public string? Name { get; set; }
}