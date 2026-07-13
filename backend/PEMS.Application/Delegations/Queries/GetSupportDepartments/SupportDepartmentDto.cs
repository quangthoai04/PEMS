namespace PEMS.Application.Delegations.Queries.GetSupportDepartments;

/// <summary>A GENERAL department of the instance's campus, with its resolved active leader. The
/// same list feeds TWO independent host actions, each with its own capability flag:
/// <list type="bullet">
/// <item><see cref="CanInviteParticipant"/> — invite the leader as a DEPT_SUPPORT participant.
/// False when there is no active leader OR the leader already occupies an active slot
/// (INVITED/ACCEPTED/ASSIGNED) in this instance.</item>
/// <item><see cref="CanReceiveLogistics"/> — send the department a system logistics request.
/// False ONLY when there is no active leader; the leader being a participant never blocks
/// logistics (the two businesses are independent).</item>
/// </list></summary>
public sealed class SupportDepartmentDto
{
    public ulong DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public ulong CampusId { get; set; }
    public string? CampusName { get; set; }

    public ulong? LeaderUserId { get; set; }
    public string? LeaderName { get; set; }
    public string? LeaderEmail { get; set; }

    public bool CanInviteParticipant { get; set; }
    public string? ParticipantDisabledReason { get; set; }

    public bool CanReceiveLogistics { get; set; }
    public string? LogisticsDisabledReason { get; set; }
}
