using PEMS.Domain.Constants;

namespace PEMS.Application.Common.Security;

/// <summary>
/// The ONE place that decides <c>security_events.severity</c>. Producers describe WHAT happened
/// (event type + result + failure reason); how alarming that is, is a policy question answered here
/// so the Security Monitoring filter and the dashboard's HIGH/CRITICAL panel read one consistent
/// scale instead of whatever each call site happened to pass.
///
/// <para>
/// Before this existed every event fell through to the column default (LOW), which made the severity
/// filter and the HIGH/CRITICAL counters permanently empty. The scale below is derived only from
/// signals real production flows already emit — no new event types or failure codes were invented:
/// </para>
///
/// <list type="bullet">
///   <item><b>CRITICAL</b> — a presented external identity was refused because it did not match the
///     identity the account is bound to (<c>INVALID_SSO_CLAIMS</c> at a 403/BLOCKED outcome, i.e.
///     Google <c>sub</c> mismatch on an existing account). That is an account-takeover signal, not a
///     user mistake.</item>
///   <item><b>HIGH</b> — any other attempt actively BLOCKED by policy: lockout triggered, disabled /
///     pending / inactive account, inactive department or campus, SSO or Visitor auto-provision
///     switched off.</item>
///   <item><b>MEDIUM</b> — an attempt that FAILED without being policy-blocked (bad/expired Google
///     token, unknown account), and successful ADMINISTRATIVE security actions
///     (<c>SECURITY_POLICY_CHECK</c> + SUCCESS: campus disabled with sessions revoked, a LOCKED Staff
///     Leader replaced). Those succeed by design but an operator still wants to see them.</item>
///   <item><b>LOW</b> — routine successes: an SSO login that worked, a session revoked by logout.</item>
/// </list>
/// </summary>
public static class SecuritySeverityResolver
{
    /// <summary>
    /// Returns the severity for an event. <paramref name="result"/> and
    /// <paramref name="eventType"/> use the <c>security_events</c> ENUM values;
    /// <paramref name="failureReasonCode"/> is NULL on success.
    /// </summary>
    public static string Resolve(string eventType, string result, string? failureReasonCode)
        => result switch
        {
            "BLOCKED" => failureReasonCode == SecurityEventFailureReasonCodes.InvalidSsoClaims
                ? SecuritySeverities.Critical
                : SecuritySeverities.High,

            "FAILED" => SecuritySeverities.Medium,

            // SUCCESS: only an administrative security action is worth surfacing above routine noise.
            _ => eventType == SecurityEventTypes.SecurityPolicyCheck
                ? SecuritySeverities.Medium
                : SecuritySeverities.Low,
        };
}
