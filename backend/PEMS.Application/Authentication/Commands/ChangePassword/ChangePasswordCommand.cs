using MediatR;
using PEMS.Application.Authentication.Models;

namespace PEMS.Application.Authentication.Commands.ChangePassword;

public sealed class ChangePasswordCommand : IRequest<MessageResponse>
{
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
