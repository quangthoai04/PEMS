using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommand : IRequest<MessageResponse>
{
    public string Email { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
