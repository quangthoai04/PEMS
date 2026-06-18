using MediatR;

namespace PEMS.Application.Profiles.Commands.ChangePassword;

public sealed class ChangePasswordCommand : IRequest<ChangePasswordResponse>
{
    public string? CurrentPassword { get; set; }
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}