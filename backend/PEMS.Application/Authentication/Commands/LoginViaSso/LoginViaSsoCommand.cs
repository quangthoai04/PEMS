using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.LoginviaSSO;

public sealed class LoginviaSSOCommand : IRequest<AuthResponse>
{
    public string IdToken { get; set; } = string.Empty;
    public string LoginPortal { get; set; } = string.Empty;
    public string? SelectedCampusId { get; set; }

    // Set by the controller from the HTTP context — never bound from the body.
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
