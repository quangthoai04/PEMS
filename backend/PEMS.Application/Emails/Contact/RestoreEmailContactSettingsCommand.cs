using MediatR;

namespace PEMS.Application.Emails.Contact;

public sealed class RestoreEmailContactSettingsCommand : IRequest<EmailContactSettingsDto>
{
    public string TemplateCode { get; set; } = string.Empty;
}
