using System;

namespace PEMS.Application.Emails.Commands.CreateEmailTemplate;

public sealed class CreateEmailTemplateResponse
{
    public ulong EmailTemplateId { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}