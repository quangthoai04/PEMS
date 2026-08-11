using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Authentication;

/// <summary>
/// Pins the one mapping that turns a security event into a severity.
///
/// <para>
/// This exists because the column used to be written by nobody: every producer left
/// <c>security_events.severity</c> on its LOW default, which made the Security Monitoring
/// "Mức độ" filter and the dashboard's HIGH/CRITICAL panel permanently empty — a feature that
/// looked implemented and measured nothing. The resolver is now the single writer, so the
/// scale has to be asserted here rather than rediscovered from whatever each call site passed.
/// </para>
///
/// <para>
/// Every (event type, result, reason) triple below is one a real production path emits — see the
/// runtime column in each case. CRITICAL in particular is asserted here because reaching it
/// end-to-end needs a genuine Google ID token whose <c>sub</c> differs from the one the account
/// is bound to, which no automated smoke can forge.
/// </para>
/// </summary>
public sealed class SecuritySeverityResolverTests
{
    // ── CRITICAL: the presented external identity is not the one this account is bound to ──
    // Runtime: LoginviaSSOCommandHandler "google_subject_mismatch" (403 ⇒ BLOCKED).

    [Fact]
    public void Blocked_sso_claim_mismatch_is_critical()
        => Assert.Equal(
            SecuritySeverities.Critical,
            SecuritySeverityResolver.Resolve(
                SecurityEventTypes.SecurityPolicyCheck, "BLOCKED",
                SecurityEventFailureReasonCodes.InvalidSsoClaims));

    // ── HIGH: an identified attempt refused by policy ──
    // Runtime: lockout triggered, disabled / pending / inactive account, inactive department or
    // campus, SSO or Visitor auto-provision switched off — all 403 ⇒ BLOCKED.

    [Theory]
    [InlineData(SecurityEventFailureReasonCodes.AccountDisabled)]
    [InlineData(SecurityEventFailureReasonCodes.AccountNotFound)]
    [InlineData(SecurityEventFailureReasonCodes.SsoProviderError)]
    [InlineData(SecurityEventFailureReasonCodes.VisitorAutoProvisionDisabled)]
    public void Blocked_for_any_other_reason_is_high(string reason)
        => Assert.Equal(
            SecuritySeverities.High,
            SecuritySeverityResolveHelper(SecurityEventTypes.SecurityPolicyCheck, "BLOCKED", reason));

    // ── MEDIUM: a sign-in that failed without being policy-blocked ──
    // Runtime: bad/expired Google token (401), unknown account (401).

    [Theory]
    [InlineData(SecurityEventTypes.SsoLogin, SecurityEventFailureReasonCodes.InvalidSsoClaims)]
    [InlineData(SecurityEventTypes.SecurityPolicyCheck, SecurityEventFailureReasonCodes.AccountNotFound)]
    public void Failed_is_medium(string eventType, string reason)
        => Assert.Equal(
            SecuritySeverities.Medium,
            SecuritySeverityResolver.Resolve(eventType, "FAILED", reason));

    // ── MEDIUM: a successful ADMINISTRATIVE security action ──
    // Runtime: campus disabled with sessions revoked (UC-86), LOCKED Staff Leader replaced (§18).
    // These succeed by design, but an operator still wants to see them above routine noise.

    [Fact]
    public void Successful_policy_action_is_medium_not_low()
        => Assert.Equal(
            SecuritySeverities.Medium,
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SecurityPolicyCheck, "SUCCESS", null));

    // ── LOW: routine success ──
    // Runtime: an SSO login that worked, a session revoked by logout.

    [Theory]
    [InlineData(SecurityEventTypes.SsoLogin)]
    [InlineData(SecurityEventTypes.SessionRevoked)]
    public void Routine_success_is_low(string eventType)
        => Assert.Equal(
            SecuritySeverities.Low,
            SecuritySeverityResolver.Resolve(eventType, "SUCCESS", null));

    /// <summary>
    /// The whole scale is reachable — no level is dead. A severity filter with an option that can
    /// never match anything is the exact defect this resolver was written to fix.
    /// </summary>
    [Fact]
    public void Every_severity_level_is_reachable()
    {
        var produced = new[]
        {
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SsoLogin, "SUCCESS", null),
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SecurityPolicyCheck, "SUCCESS", null),
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SsoLogin, "FAILED",
                SecurityEventFailureReasonCodes.InvalidSsoClaims),
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SecurityPolicyCheck, "BLOCKED",
                SecurityEventFailureReasonCodes.AccountDisabled),
            SecuritySeverityResolver.Resolve(SecurityEventTypes.SecurityPolicyCheck, "BLOCKED",
                SecurityEventFailureReasonCodes.InvalidSsoClaims),
        };

        Assert.Equal(
            new[]
            {
                SecuritySeverities.Low, SecuritySeverities.Medium, SecuritySeverities.High,
                SecuritySeverities.Critical,
            }.OrderBy(s => s),
            produced.Distinct().OrderBy(s => s));
    }

    private static string SecuritySeverityResolveHelper(string eventType, string result, string reason)
        => SecuritySeverityResolver.Resolve(eventType, result, reason);
}
