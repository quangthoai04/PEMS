namespace PEMS.Application.Common.Security;

/// <summary>What a verify request should do, decided from a challenge snapshot.</summary>
public enum OtpVerifyOutcome
{
    Success,
    Invalid,                    // wrong code, attempts remain
    Expired,
    SessionInvalid,             // used / invalidated / binding mismatch
    RetryLater,                 // server cooldown still active — no attempt consumed
    HumanVerificationRequired   // challenge burned by too many wrong codes
}

/// <summary>
/// Pure decision for one verify request. The caller applies the state-mutation flags
/// (<see cref="RegisterWrongAttempt"/>, <see cref="NextCooldownSeconds"/>,
/// <see cref="BurnChallenge"/>) to the tracked entity and OWNS persistence/commit.
/// </summary>
public sealed record OtpVerifyVerdict(
    OtpVerifyOutcome Outcome,
    int RemainingAttempts,
    int RetryAfterSeconds,
    bool HumanVerificationRequired,
    bool RegisterWrongAttempt,
    int NextCooldownSeconds,
    bool BurnChallenge);

/// <summary>Immutable snapshot of the challenge row a verify decision is based on.</summary>
public sealed record OtpChallengeSnapshot(
    DateTime ExpiresAt,
    DateTime? UsedAt,
    DateTime? InvalidatedAt,
    DateTime? HumanVerificationRequiredAt,
    DateTime? NextAttemptAllowedAt,
    int AttemptCount,
    int MaxAttempts);

/// <summary>Decision for one issue (initiate / resend / recover) request.</summary>
public sealed record OtpIssueDecision(bool Allowed, int RetryAfterSeconds);

/// <summary>
/// Pure, deterministic UC-17 OTP challenge rules: progressive cooldown schedule, the
/// 10-wrong-attempts → human-verification transition, and per-email hourly issue quotas.
/// No I/O and no clock reads — fully unit-testable. <c>OtpService</c> (Infrastructure)
/// loads the row under a lock, calls these, applies the verdict and persists.
/// </summary>
public static class OtpChallengePolicy
{
    /// <summary>
    /// Server cooldown seconds imposed AFTER the given wrong attempt (1-based), gating the
    /// next attempt. Attempts before <paramref name="progressiveDelayStartAttempt"/> have no
    /// cooldown; from there the delay doubles (2, 4, 8, …) and is capped at 15 seconds.
    /// The final wrong attempt burns the challenge instead of cooling down.
    /// </summary>
    public static int CooldownSecondsAfterWrongAttempt(
        int wrongAttemptNumber, int progressiveDelayStartAttempt, int maxAttempts)
    {
        if (wrongAttemptNumber >= maxAttempts) return 0;
        if (wrongAttemptNumber < progressiveDelayStartAttempt) return 0;
        var doublings = wrongAttemptNumber - progressiveDelayStartAttempt; // 0,1,2,…
        // 2 → 4 → 8 → 16(capped 15). Guard the shift against pathological configs.
        var seconds = doublings >= 30 ? int.MaxValue : 2 << doublings;
        return Math.Min(seconds, 15);
    }

    /// <summary>
    /// Decides one verify request. The code comparison result is an input so this stays
    /// pure — the caller compares hashes first, then asks for the verdict (this is what
    /// makes "correct code on the 10th try" succeed: the comparison happens BEFORE the
    /// attempt is classified as the final wrong one).
    /// </summary>
    public static OtpVerifyVerdict EvaluateVerify(
        OtpChallengeSnapshot s, bool codeMatches, DateTime now, int progressiveDelayStartAttempt)
    {
        var remaining = Math.Max(0, s.MaxAttempts - s.AttemptCount);

        if (s.UsedAt is not null)
            return new OtpVerifyVerdict(OtpVerifyOutcome.SessionInvalid, remaining, 0, false, false, 0, false);

        // A burned challenge (max wrong attempts reached) never accepts more codes — only
        // human verification recovers it. Checked BEFORE the generic invalidated check
        // because burning sets both human_verification_required_at and invalidated_at.
        if (s.HumanVerificationRequiredAt is not null || s.AttemptCount >= s.MaxAttempts)
            return new OtpVerifyVerdict(OtpVerifyOutcome.HumanVerificationRequired, 0, 0, true, false, 0, false);

        if (s.InvalidatedAt is not null)
            return new OtpVerifyVerdict(OtpVerifyOutcome.SessionInvalid, remaining, 0, false, false, 0, false);

        if (s.ExpiresAt <= now)
            return new OtpVerifyVerdict(OtpVerifyOutcome.Expired, remaining, 0, false, false, 0, false);

        // Cooldown is enforced server-side and consumes NO attempt.
        if (s.NextAttemptAllowedAt is not null && s.NextAttemptAllowedAt > now)
        {
            var wait = (int)Math.Ceiling((s.NextAttemptAllowedAt.Value - now).TotalSeconds);
            return new OtpVerifyVerdict(OtpVerifyOutcome.RetryLater, remaining, Math.Max(wait, 1), false, false, 0, false);
        }

        if (codeMatches)
            return new OtpVerifyVerdict(OtpVerifyOutcome.Success, remaining, 0, false, false, 0, false);

        var wrongNumber = s.AttemptCount + 1;

        if (wrongNumber >= s.MaxAttempts)
            return new OtpVerifyVerdict(
                OtpVerifyOutcome.HumanVerificationRequired, 0, 0, true,
                RegisterWrongAttempt: true, NextCooldownSeconds: 0, BurnChallenge: true);

        var cooldown = CooldownSecondsAfterWrongAttempt(wrongNumber, progressiveDelayStartAttempt, s.MaxAttempts);
        return new OtpVerifyVerdict(
            OtpVerifyOutcome.Invalid, s.MaxAttempts - wrongNumber, cooldown, false,
            RegisterWrongAttempt: true, NextCooldownSeconds: cooldown, BurnChallenge: false);
    }

    /// <summary>
    /// Per-email hourly issue quota. Standard issues (INITIAL/RESEND) have a soft limit;
    /// human-recovery issues have their own (stricter) limit and may exceed the soft
    /// standard limit — but nothing may exceed the absolute hard limit. There is NO
    /// permanent email/account lock: every window slides open again after an hour.
    /// </summary>
    public static OtpIssueDecision EvaluateIssue(
        bool isHumanRecovery,
        int standardIssuesInWindow,
        int recoveryIssuesInWindow,
        DateTime? lastIssuedAt,
        DateTime now,
        int minResendIntervalSeconds,
        int maxStandardPerHour,
        int maxRecoveryPerHour,
        int absoluteMaxPerHour)
    {
        if (standardIssuesInWindow + recoveryIssuesInWindow >= absoluteMaxPerHour)
            return new OtpIssueDecision(false, 3600);

        if (isHumanRecovery)
        {
            if (recoveryIssuesInWindow >= maxRecoveryPerHour)
                return new OtpIssueDecision(false, 3600);
        }
        else if (standardIssuesInWindow >= maxStandardPerHour)
        {
            return new OtpIssueDecision(false, 3600);
        }

        // minResendIntervalSeconds <= 0 disables the interval check entirely. This must be
        // an explicit guard: MySQL DATETIME rounds fractional seconds, so a just-written
        // last-issue timestamp can read back a few ms in the FUTURE and make elapsed
        // slightly negative — which would spuriously trip "elapsed < 0".
        if (minResendIntervalSeconds > 0 && lastIssuedAt is not null)
        {
            var elapsed = (now - lastIssuedAt.Value).TotalSeconds;
            if (elapsed < minResendIntervalSeconds)
                return new OtpIssueDecision(false, (int)Math.Ceiling(minResendIntervalSeconds - elapsed));
        }

        return new OtpIssueDecision(true, 0);
    }
}
