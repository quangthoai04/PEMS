using System;
using System.Collections.Generic;

namespace PEMS.Application.Delegations.Queries.GetHostCandidates;

public sealed class HostCandidateDto
{
    public ulong UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public ulong? CampusId { get; set; }
    public string? DepartmentName { get; set; }
    public string? SubRole { get; set; }
    /// <summary>Number of campus instances this user is currently hosting (not cancelled/closed).</summary>
    public int ActiveAssignmentCount { get; set; }
    public bool HasScheduleConflict { get; set; }
    public List<HostConflictDto> Conflicts { get; set; } = new();
}

public sealed class HostConflictDto
{
    public ulong VisitRequestId { get; set; }
    public ulong VisitInstanceId { get; set; }
    public string? DelegationName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
