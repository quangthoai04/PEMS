using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.RefreshToken;

public sealed class RefreshTokenCommand : IRequest<AuthResponse>
{
    public string RefreshToken { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
