using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.Logout;

public sealed class LogoutCommand : IRequest<MessageResponse>
{
    /// <summary>Optional — when supplied the matching session is revoked in addition to the current one.</summary>
    public string? RefreshToken { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
