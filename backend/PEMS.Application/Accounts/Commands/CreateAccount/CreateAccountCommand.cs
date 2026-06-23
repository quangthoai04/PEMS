using MediatR;

namespace PEMS.Application.Accounts.Commands.CreateAccount;

/// <summary>
/// UC-96 Create Account. Creates an internal or visitor account. Staff Leaders are
/// scoped to their own campus; HO/Admin may target any campus. A temporary password
/// is honoured only in dev (DevMixed) mode â€” production accounts sign in via SSO/FEID.
/// </summary>
public sealed class CreateAccountCommand : IRequest<CreateAccountResponse>
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;     // ADMIN | HO | STAFF | DEPARTMENT | STUDENT | VISITOR
    public string? SubRole { get; set; }                     // Leader | Staff (STAFF/DEPARTMENT only)
    public ulong? PrimaryCampusId { get; set; }
    public ulong? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public PEMS.Domain.Enums.Gender? Gender { get; set; }
    public string? StudentCode { get; set; }
    public string? Nationality { get; set; }

    /// <summary>Optional temporary password â€” honoured only in DevMixed mode.</summary>
    public string? Password { get; set; }
}
