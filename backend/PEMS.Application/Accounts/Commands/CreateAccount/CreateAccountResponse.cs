namespace PEMS.Application.Accounts.Commands.CreateAccount;

public sealed class CreateAccountResponse
{
    public ulong UserId { get; init; }
    public string Email { get; init; } = default!;
    public string RoleCode { get; init; } = default!;
    public ulong? PrimaryCampusId { get; init; }
    public bool PasswordSet { get; init; }
    public string Message { get; init; } = "Account created successfully.";
}
