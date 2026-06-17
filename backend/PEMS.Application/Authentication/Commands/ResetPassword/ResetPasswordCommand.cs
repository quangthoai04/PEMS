using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommand : IRequest<MessageResponse>
{
    public string Email { get; set; } = string.Empty;
    public string OtpOrToken { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
