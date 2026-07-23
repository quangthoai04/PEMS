namespace PEMS.Application.Delegations.Services;

/// <summary>
/// Shared audit vocabulary for the two per-campus decision commands (approve+assign-host and reject).
/// Both write their audit row under the deciding instance's own campus/request/instance context so the
/// campus-scoped audit surfaces can find them; this constant keeps the two in step.
/// </summary>
public static class CampusDecisionAudit
{
    /// <summary>audit_logs.source_type for a Staff Leader decision on one campus instance.</summary>
    public const string SourceType = "CAMPUS_DECISION";
}
