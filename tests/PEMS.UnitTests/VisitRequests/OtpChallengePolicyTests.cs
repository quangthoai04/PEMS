using System;
using PEMS.Application.Common.Security;
using Xunit;

namespace PEMS.UnitTests.VisitRequests;

/// <summary>
/// Unit tests for <see cref="OtpChallengePolicy"/> — the pure UC-17 OTP challenge rules
/// (attempt counting, 10-wrong → human verification, progressive cooldown, issue quotas).
/// No database, no clock reads: everything is passed in.
/// </summary>
public class OtpChallengePolicyTests
{
    private const int MaxAttempts = 10;
    private const int StartAttempt = 6;

    private static readonly DateTime Now = new(2026, 7, 11, 10, 0, 0, DateTimeKind.Utc);

    private static OtpChallengeSnapshot Snapshot(
        int attemptCount = 0,
        int maxAttempts = MaxAttempts,
        DateTime? expiresAt = null,
        DateTime? usedAt = null,
        DateTime? invalidatedAt = null,
        DateTime? humanVerificationRequiredAt = null,
        DateTime? nextAttemptAllowedAt = null)
        => new(
            expiresAt ?? Now.AddMinutes(5),
            usedAt,
            invalidatedAt,
            humanVerificationRequiredAt,
            nextAttemptAllowedAt,
            attemptCount,
            maxAttempts);

    // ── Progressive cooldown schedule (spec §9): 6→2s, 7→4s, 8→8s, 9→15s ──────────

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 2)]
    [InlineData(7, 4)]
    [InlineData(8, 8)]
    [InlineData(9, 15)]
    [InlineData(10, 0)] // the final wrong attempt burns the challenge — no cooldown
    public void CooldownSchedule_MatchesSpec(int wrongAttemptNumber, int expectedSeconds)
    {
        var seconds = OtpChallengePolicy.CooldownSecondsAfterWrongAttempt(
            wrongAttemptNumber, StartAttempt, MaxAttempts);
        Assert.Equal(expectedSeconds, seconds);
    }

    // ── Wrong attempts increment and report remaining (spec §29.1) ────────────────

    [Theory]
    [InlineData(0, 9)]
    [InlineData(3, 6)]
    [InlineData(7, 2)]
    public void WrongCode_RegistersAttempt_AndReportsRemaining(int currentAttempts, int expectedRemaining)
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: currentAttempts), codeMatches: false, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.Invalid, verdict.Outcome);
        Assert.True(verdict.RegisterWrongAttempt);
        Assert.False(verdict.BurnChallenge);
        Assert.Equal(expectedRemaining, verdict.RemainingAttempts);
    }

    // ── Up to the 9th wrong attempt no CAPTCHA is required (spec §29.2) ────────────

    [Fact]
    public void NinthWrongAttempt_DoesNotRequireHumanVerification()
    {
        // 8 wrong so far → this is wrong attempt #9.
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 8), codeMatches: false, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.Invalid, verdict.Outcome);
        Assert.False(verdict.HumanVerificationRequired);
        Assert.False(verdict.BurnChallenge);
        Assert.Equal(1, verdict.RemainingAttempts);
        Assert.Equal(15, verdict.NextCooldownSeconds);
    }

    // ── The 10th wrong attempt burns the challenge (spec §29.3) ────────────────────

    [Fact]
    public void TenthWrongAttempt_BurnsChallenge_AndRequiresHumanVerification()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 9), codeMatches: false, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.HumanVerificationRequired, verdict.Outcome);
        Assert.True(verdict.HumanVerificationRequired);
        Assert.True(verdict.RegisterWrongAttempt);
        Assert.True(verdict.BurnChallenge);
        Assert.Equal(0, verdict.RemainingAttempts);
    }

    // ── Correct code on the 10th (final) attempt still succeeds (spec §29.4) ──────

    [Fact]
    public void CorrectCode_OnFinalAttempt_Succeeds()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 9), codeMatches: true, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.Success, verdict.Outcome);
        Assert.False(verdict.RegisterWrongAttempt);
        Assert.False(verdict.BurnChallenge);
    }

    // ── Cooldown boundaries (spec §29.5): before → RetryLater w/o attempt; at/after → allowed ──

    [Fact]
    public void VerifyDuringCooldown_ReturnsRetryLater_WithoutConsumingAttempt()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 6, nextAttemptAllowedAt: Now.AddSeconds(4)),
            codeMatches: true, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.RetryLater, verdict.Outcome);
        Assert.False(verdict.RegisterWrongAttempt);
        Assert.Equal(4, verdict.RetryAfterSeconds);
    }

    [Fact]
    public void VerifyExactlyAtCooldownEnd_IsAllowed()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 6, nextAttemptAllowedAt: Now),
            codeMatches: true, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.Success, verdict.Outcome);
    }

    // ── Dead-state challenges (spec §29.6) ─────────────────────────────────────────

    [Fact]
    public void ExpiredChallenge_ReturnsExpired()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(expiresAt: Now.AddSeconds(-1)), codeMatches: true, Now, StartAttempt);
        Assert.Equal(OtpVerifyOutcome.Expired, verdict.Outcome);
    }

    [Fact]
    public void UsedChallenge_ReturnsSessionInvalid()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(usedAt: Now.AddMinutes(-1)), codeMatches: true, Now, StartAttempt);
        Assert.Equal(OtpVerifyOutcome.SessionInvalid, verdict.Outcome);
    }

    [Fact]
    public void InvalidatedChallenge_ReturnsSessionInvalid()
    {
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(invalidatedAt: Now.AddMinutes(-1)), codeMatches: true, Now, StartAttempt);
        Assert.Equal(OtpVerifyOutcome.SessionInvalid, verdict.Outcome);
    }

    [Fact]
    public void BurnedChallenge_NeverAcceptsMoreCodes_EvenCorrectOnes()
    {
        // Burned = human_verification_required_at + invalidated_at both set.
        var verdict = OtpChallengePolicy.EvaluateVerify(
            Snapshot(attemptCount: 10,
                humanVerificationRequiredAt: Now.AddMinutes(-1),
                invalidatedAt: Now.AddMinutes(-1)),
            codeMatches: true, Now, StartAttempt);

        Assert.Equal(OtpVerifyOutcome.HumanVerificationRequired, verdict.Outcome);
        Assert.True(verdict.HumanVerificationRequired);
        Assert.False(verdict.RegisterWrongAttempt);
    }

    // ── Issue quotas (spec §29.10): standard soft / recovery / absolute hard ───────

    /// <summary>
    /// Builds the per-email issue history the policy receives: <paramref name="standard"/>
    /// standard issues then <paramref name="recovery"/> recovery issues, most recent at
    /// <paramref name="lastIssuedAt"/> and older ones one minute apart (all inside the window).
    /// </summary>
    private static IReadOnlyList<(bool IsRecovery, DateTime CreatedAt)> Issues(
        int standard, int recovery, DateTime lastIssuedAt)
    {
        var list = new System.Collections.Generic.List<(bool IsRecovery, DateTime CreatedAt)>();
        var createdAt = lastIssuedAt;
        for (var i = 0; i < standard; i++)
        {
            list.Add((false, createdAt));
            createdAt = createdAt.AddMinutes(-1);
        }
        for (var i = 0; i < recovery; i++)
        {
            list.Add((true, createdAt));
            createdAt = createdAt.AddMinutes(-1);
        }
        return list;
    }

    [Fact]
    public void StandardIssue_UnderLimits_Allowed()
    {
        var decision = OtpChallengePolicy.EvaluateIssue(
            isHumanRecovery: false, Issues(standard: 4, recovery: 0, lastIssuedAt: Now.AddMinutes(-5)), Now,
            minResendIntervalSeconds: 60, maxStandardPerHour: 5, maxRecoveryPerHour: 1, absoluteMaxPerHour: 7);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void StandardIssue_AtSoftLimit_Denied()
    {
        var decision = OtpChallengePolicy.EvaluateIssue(
            false, Issues(standard: 5, recovery: 0, lastIssuedAt: Now.AddMinutes(-5)), Now, 60, 5, 1, 7);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void RecoveryIssue_MayExceedSoftStandardLimit_ButNotAbsolute()
    {
        // 5 standard issues used up — recovery still allowed once (5+0 < 7 absolute).
        var allowed = OtpChallengePolicy.EvaluateIssue(
            isHumanRecovery: true, Issues(standard: 5, recovery: 0, lastIssuedAt: Now.AddMinutes(-5)), Now, 60, 5, 1, 7);
        Assert.True(allowed.Allowed);

        // Second recovery in the window exceeds the recovery quota.
        var recoveryQuota = OtpChallengePolicy.EvaluateIssue(
            true, Issues(standard: 5, recovery: 1, lastIssuedAt: Now.AddMinutes(-5)), Now, 60, 5, 1, 7);
        Assert.False(recoveryQuota.Allowed);

        // Absolute hard limit trumps everything, including recovery.
        var absolute = OtpChallengePolicy.EvaluateIssue(
            true, Issues(standard: 7, recovery: 0, lastIssuedAt: Now.AddMinutes(-5)), Now, 60, 5, 1, 7);
        Assert.False(absolute.Allowed);
    }

    [Fact]
    public void MinResendInterval_Boundary()
    {
        // 59s since the last issue → denied with retryAfter 1s.
        var tooSoon = OtpChallengePolicy.EvaluateIssue(
            false, Issues(standard: 1, recovery: 0, lastIssuedAt: Now.AddSeconds(-59)), Now, 60, 5, 1, 7);
        Assert.False(tooSoon.Allowed);
        Assert.Equal(1, tooSoon.RetryAfterSeconds);

        // Exactly 60s → allowed.
        var exact = OtpChallengePolicy.EvaluateIssue(
            false, Issues(standard: 1, recovery: 0, lastIssuedAt: Now.AddSeconds(-60)), Now, 60, 5, 1, 7);
        Assert.True(exact.Allowed);
    }
}
