namespace PEMS.Application.Emails.Commands.ToggleEmailTemplateStatus;

public sealed class ToggleEmailTemplateStatusResponse
{
    public ulong EmailTemplateId { get; set; }
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}
