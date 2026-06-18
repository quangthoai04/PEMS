namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountResponse
{
    public string UserId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public string? PrimaryCampusId { get; init; }
    public bool PasswordSet { get; init; }
    public string Message { get; init; } = "Account created successfully.";
}
