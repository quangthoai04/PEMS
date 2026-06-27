using System;
using System.Collections.Generic;

namespace PEMS.Application.Emails.Queries.ViewEmail;

public class ViewEmailDto
{
    public ulong Id { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string SenderEmail { get; set; } = null!;
    public List<EmailRecipientDto> To { get; set; } = new();
    public List<EmailRecipientDto> Cc { get; set; } = new();
    public List<EmailRecipientDto> Bcc { get; set; } = new();
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = null!;
    public string ProcessStatus { get; set; } = null!;
    public bool CanReply { get; set; }
    public bool CanConfirm { get; set; }
    public bool CanMarkComplete { get; set; }
}

public class EmailRecipientDto
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? DeliveryStatus { get; set; }
}