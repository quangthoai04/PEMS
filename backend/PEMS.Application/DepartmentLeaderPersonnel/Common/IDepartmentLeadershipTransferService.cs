using System.Threading;
using System.Threading.Tasks;

namespace PEMS.Application.DepartmentLeaderPersonnel.Common;

/// <summary>
/// The one transactional core behind every "hand the department head seat to someone else" flow —
/// shared by the self-service <c>TransferDepartmentLeadershipCommandHandler</c> and the third-party
/// legacy <c>ReassignDepartmentLeadCommandHandler</c> (SEC-09 remediation).
///
/// <para>
/// <paramref name="expectedCurrentLeaderUserId"/> is the caller's OWN, best-effort, pre-lock read of
/// <c>department.HeadUserId</c> — never trusted for authorization, only used to know which two users
/// to lock. It is re-verified, unconditionally, for BOTH callers, once the department row is re-read
/// under lock: a third-party caller never knows in advance whether the seat moved between its own
/// read and this call, so skipping that re-check would risk mutating whichever account it merely
/// guessed was the outgoing head.
/// </para>
/// </summary>
public interface IDepartmentLeadershipTransferService
{
    /// <exception cref="PEMS.Application.Common.Exceptions.BusinessRuleException">
    /// The candidate equals the expected current leader, the department is not ACTIVE, or the
    /// candidate fails <c>EnsureUsableSuccessor</c> (wrong role/department/campus/not active).
    /// </exception>
    /// <exception cref="PEMS.Application.Common.Exceptions.ConflictException">
    /// Under lock, <c>department.HeadUserId</c> no longer equals <paramref name="expectedCurrentLeaderUserId"/> —
    /// a concurrent transfer already moved the seat.
    /// </exception>
    /// <exception cref="PEMS.Application.Common.Exceptions.ForbiddenException">
    /// <paramref name="actorMustBeCurrentLeader"/> is true and, under lock, the actor is not the
    /// department's current head (a race between the caller's own scope check and this call).
    /// </exception>
    Task<DepartmentLeadershipTransferResult> TransferAsync(
        ulong departmentId,
        ulong expectedCurrentLeaderUserId,
        ulong newLeaderUserId,
        ulong actorUserId,
        bool actorMustBeCurrentLeader,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of a completed (committed) leadership transfer.</summary>
public sealed record DepartmentLeadershipTransferResult(
    ulong DepartmentId,
    string DepartmentName,
    ulong PreviousLeaderUserId,
    string PreviousLeaderName,
    ulong NewLeaderUserId,
    string NewLeaderName,
    int RevokedSessions,
    string EmailNotificationStatus);
