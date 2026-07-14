using System;
using System.Collections.Generic;
using PEMS.Application.Campuses.Common;

namespace PEMS.Application.Campuses.Queries.GetCampusStatusImpact;

/// <summary>
/// UC-86 §18 preview payload for the status confirmation modal. For a disable preview the
/// blocker fields are filled from <see cref="CampusStatusImpactCalculator"/>; for an enable
/// preview <see cref="EnableIssues"/> lists what still prevents activation (missing master
/// data fields / missing ACTIVE IC department). Preview is UX only — the command rechecks.
/// </summary>
public sealed class GetCampusStatusImpactResponse
{
    public ulong CampusId { get; init; }
    public string Name { get; init; } = null!;
    public string CurrentStatus { get; init; } = null!;
    public string TargetStatus { get; init; } = null!;

    /// <summary>False when disable blockers exist, enable requirements fail, or it is a no-op.</summary>
    public bool CanChange { get; init; }

    // ── Disable preview (targetStatus = INACTIVE) ──
    public int BlockerCount { get; init; }
    public IReadOnlyDictionary<string, int> BlockersByStatus { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyList<CampusVisitBlockerExample> BlockerExamples { get; init; } =
        Array.Empty<CampusVisitBlockerExample>();

    // ── Enable preview (targetStatus = ACTIVE) ──
    /// <summary>
    /// Machine-readable enable blockers: "MASTER_DATA_INCOMPLETE:{field}" per missing field and
    /// "ACTIVE_IC_DEPARTMENT_MISSING" when the campus has no ACTIVE IC department.
    /// </summary>
    public IReadOnlyList<string> EnableIssues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Operational readiness the campus WOULD have as ACTIVE (enable preview) or has now —
    /// lets the modal warn "sẽ ACTIVE nhưng chưa nhận đăng ký vì chưa có Staff Leader".
    /// </summary>
    public CampusOperationalReadinessDto? Readiness { get; init; }
}
