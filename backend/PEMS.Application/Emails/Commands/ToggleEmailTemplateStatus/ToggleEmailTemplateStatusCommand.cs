using MediatR;

namespace PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus;

public class ToggleEmailTemplateStatusCommand : IRequest<ToggleEmailTemplateStatusResponse>
{
    public ulong EmailTemplateId { get; set; }
    public string Status { get; set; } = null!;
}
