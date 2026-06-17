using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.LoginviaCredentials;

public sealed class LoginviaCredentialsCommand : IRequest<AuthResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string LoginPortal { get; set; } = string.Empty;

    // Set by the controller from the HTTP context — never bound from the body.
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
