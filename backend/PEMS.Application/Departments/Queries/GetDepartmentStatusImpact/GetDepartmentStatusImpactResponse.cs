using System.Collections.Generic;
using PEMS.Application.Departments.Common;

namespace PEMS.Application.Departments.Queries.GetDepartmentStatusImpact;

public sealed class GetDepartmentStatusImpactResponse
{
    public ulong DepartmentId { get; init; }
    public string Name { get; init; } = default!;
    public string CurrentStatus { get; init; } = default!;
    public string TargetStatus { get; init; } = default!;

    /// <summary>DEPARTMENT+LEADER/STAFF accounts that would lose access on disable.</summary>
    public int AffectedAccountCount { get; init; }

    /// <summary>Active sessions of those accounts that would be revoked on disable.</summary>
    public int ActiveSessionCount { get; init; }

    /// <summary>False when blockers exist (disable) or the campus is inactive (enable).</summary>
    public bool CanChangeStatus { get; init; }

    public IReadOnlyList<DepartmentStatusBlocker> Blockers { get; init; } = new List<DepartmentStatusBlocker>();
}
