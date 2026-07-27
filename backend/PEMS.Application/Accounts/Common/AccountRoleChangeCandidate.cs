namespace PEMS.Application.Accounts.Common;

/// <summary>
/// One <c>visit_request_campuses</c> row where the target user is the current Host, projected with
/// the campus-instance status the blocker matrix keys off.
/// </summary>
public sealed class HostAssignmentCandidate
{
    public ulong VisitInstanceId { get; init; }
    public string VisitStatus { get; init; } = default!;
}

/// <summary>Same as <see cref="HostAssignmentCandidate"/> for <c>coordinator_user_id</c>.</summary>
public sealed class CoordinatorAssignmentCandidate
{
    public ulong VisitInstanceId { get; init; }
    public string VisitStatus { get; init; } = default!;
}

/// <summary>
/// One <c>visit_participants</c> row of the target user, joined with its campus instance. Carries
/// <see cref="InstanceHostUserId"/> so the rule can recognise the canonical Host participant row and
/// avoid counting the same responsibility twice (spec §8.4), and
/// <see cref="DelegatedToSubstitute"/> so it can recognise a Department Leader who has already
/// handed this visit down to one of their staff (spec §8.4b).
/// </summary>
public sealed class ParticipantResponsibilityCandidate
{
    public ulong VisitInstanceId { get; init; }
    public string ParticipantRole { get; init; } = default!;
    public string ParticipantStatus { get; init; } = default!;
    public string VisitStatus { get; init; } = default!;
    public ulong? InstanceHostUserId { get; init; }

    /// <summary>
    /// True when, on this same campus instance, somebody else holds a DEPT_SUPPORT row that the
    /// target user assigned and that is still alive (ASSIGNED / ACCEPTED). False once that staff
    /// member declines or is removed — the duty bounces back to whoever delegated it.
    /// </summary>
    public bool DelegatedToSubstitute { get; init; }
}

/// <summary>
/// One <c>visit_logistics_items</c> row where the target user is either the assignee or the
/// receiver, joined with its campus instance. The rule decides which of the two references is a
/// live personal responsibility and which is mere handling history (spec §8.5).
/// </summary>
public sealed class LogisticsResponsibilityCandidate
{
    public ulong LogisticsItemId { get; init; }
    public ulong VisitInstanceId { get; init; }
    public string LogisticsStatus { get; init; } = default!;
    public string VisitStatus { get; init; } = default!;
    public ulong? AssignedToUserId { get; init; }
    public ulong? ReceivedBy { get; init; }
}

/// <summary>One <c>departments</c> row whose <c>head_user_id</c> still points at the target user.</summary>
public sealed class DepartmentHeadCandidate
{
    public ulong DepartmentId { get; init; }
    public string DepartmentName { get; init; } = default!;
}
