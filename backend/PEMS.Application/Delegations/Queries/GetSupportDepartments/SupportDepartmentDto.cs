namespace PEMS.Application.Delegations.Queries.GetSupportDepartments;

/// <summary>A GENERAL department of the instance's campus that the host can invite to support, with
/// its resolved active leader (the actual invitee). <see cref="CanInvite"/> is false when the
/// department has no active leader or the leader already occupies a slot in this instance.</summary>
public sealed class SupportDepartmentDto
{
    public ulong DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public ulong CampusId { get; set; }
    public string? CampusName { get; set; }

    public ulong? LeaderUserId { get; set; }
    public string? LeaderName { get; set; }
    public string? LeaderEmail { get; set; }

    public bool CanInvite { get; set; }
    public string? DisabledReason { get; set; }
}
