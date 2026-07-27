using System.Threading;
using System.Threading.Tasks;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Users;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// Resolved, database-verified scope of the Department Leader making the current request. Every value
/// here comes from <c>users</c> / <c>departments</c> rows read inside the request — never from the JWT
/// alone and never from the client payload (spec §5.1/§7).
/// </summary>
public sealed class DepartmentLeaderScope
{
    public required ulong ActorUserId { get; init; }
    public required ulong DepartmentId { get; init; }
    public required ulong CampusId { get; init; }
    public required string DepartmentName { get; init; }
    public required string CampusName { get; init; }
}

/// <summary>
/// Single source of truth for the Department Leader personnel scope (spec §7). Every endpoint under
/// <c>/api/department-leader</c> goes through this service instead of re-deriving the rules, so no
/// handler can accidentally ship a weaker check.
///
/// The gate is deliberately stricter than the JWT: a token minted before a leadership transfer still
/// carries <c>DEPARTMENT + LEADER</c>, so the service re-reads the actor and the department and
/// requires <c>departments.head_user_id == actor.user_id</c> at request time (spec §4).
/// </summary>
public interface IDepartmentLeaderPersonnelScopeService
{
    /// <summary>
    /// The full authorization gate. Verifies, against the database:
    /// actor authenticated · DEPARTMENT + LEADER · ACTIVE · has a department · department exists ·
    /// GENERAL · ACTIVE · campus ACTIVE · <c>head_user_id</c> is the actor · the department is the
    /// actor's own <c>department_id</c>. Throws 401/403/422 otherwise; never returns a partial scope.
    /// </summary>
    Task<DepartmentLeaderScope> EnsureCurrentUserIsActualDepartmentLeaderAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the gate and returns the caller's department as a TRACKED entity (with campus loaded) so a
    /// command can mutate it inside its own transaction. Read-only callers should use the scope's
    /// projected fields instead.
    /// </summary>
    Task<Department> ResolveCurrentDepartmentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a personnel row that is provably inside <paramref name="scope"/>: a DEPARTMENT account
    /// whose <c>department_id</c> equals the caller's. Anything else — missing, another department,
    /// another role — raises the SAME 404 <c>PERSONNEL_NOT_FOUND</c> so the endpoint cannot be used to
    /// enumerate accounts elsewhere (spec §11).
    /// </summary>
    Task<User> GetScopedPersonnelAsync(DepartmentLeaderScope scope, ulong userId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-asserts membership for an id that was already loaded (e.g. after taking a row lock, when the
    /// row may have moved to another department in the meantime). Throws the same 404 as
    /// <see cref="GetScopedPersonnelAsync"/>.
    /// </summary>
    Task EnsureTargetBelongsToCurrentDepartmentAsync(DepartmentLeaderScope scope, ulong userId, CancellationToken cancellationToken);
}
