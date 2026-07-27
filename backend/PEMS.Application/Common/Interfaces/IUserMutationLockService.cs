using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Shared pessimistic row locks over <c>users</c> and <c>departments</c>, taken inside the caller's
/// transaction.
///
/// Re-checking dependencies twice is not enough to make a role change safe: between the check and
/// the commit another transaction can still assign the account a Host/participant/logistics
/// responsibility. Every flow that either changes a role or creates such a responsibility takes the
/// same lock on the same rows first, so the two serialize (spec §13):
///
/// <code>
/// begin transaction → lock user(s) → re-read role/status/campus → validate → write → commit
/// </code>
///
/// Whichever transaction locks first wins; the other one blocks, then re-reads and sees the
/// committed state — either the assignment is refused because the role already changed, or the role
/// change returns 409 because the responsibility now exists.
/// </summary>
public interface IUserMutationLockService
{
    /// <summary>
    /// Locks the given user rows for update. Ids are locked in ascending order by the implementation
    /// so two flows holding overlapping sets can never deadlock each other. Unknown ids are ignored
    /// (nothing to lock); an empty collection is a no-op.
    /// </summary>
    Task LockUsersAsync(IReadOnlyCollection<ulong> userIds, CancellationToken cancellationToken);

    /// <summary>Same contract as <see cref="LockUsersAsync"/> for <c>departments</c> rows.</summary>
    Task LockDepartmentsAsync(IReadOnlyCollection<ulong> departmentIds, CancellationToken cancellationToken);
}
