using MediatR;

namespace PEMS.Application.Accounts.Commands.UpdateAccountRole;

/// <summary>
/// UC-100 Update Account Role. Typical use: a Staff Leader promotes a VISITOR to an
/// internal role for their campus. Campus/department are (re)assigned per role rules,
/// existing auth providers are kept, and all active sessions are revoked so the user
/// re-authenticates with the new role.
/// </summary>
public sealed class UpdateAccountRoleCommand : IRequest<UpdateAccountRoleResponse>
{
    public ulong UserId { get; set; }
    public string NewRoleCode { get; set; } = string.Empty;  // ADMIN | HO | STAFF | DEPT | STUDENT | VISITOR
    public string? SubRole { get; set; }                     // Leader | Staff (STAFF/DEPT only)
    public ulong? PrimaryCampusId { get; set; }
    public ulong? DepartmentId { get; set; }
}
