namespace PEMS.Application.Profiles.Commands.ChangePassword;

public sealed class ChangePasswordResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}