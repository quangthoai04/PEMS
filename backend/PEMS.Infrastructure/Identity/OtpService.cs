using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Domain.Entities.Users;

namespace PEMS.Infrastructure.Identity;

/// <summary>
/// Issues and verifies one-time codes. Codes are stored hashed (scoped by
/// email+purpose so values are effectively unique). Enforces resend + attempt limits.
///
/// UC-17 uses the challenge-based V2 flow: every issue returns an opaque CSPRNG session
/// token whose SHA-256 identifies the challenge row; verify locks that row
/// (<c>SELECT … FOR UPDATE</c>) inside the CALLER's transaction so concurrent attempts
/// can never lose an attempt-count update, and the caller commits the wrong-attempt
/// state even though the request itself fails.
/// </summary>
public sealed class OtpService : IOtpService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeService _clock;
    private readonly int _codeMinutes;
    private readonly int _visitRequestCodeMinutes;
    private readonly int _maxAttempts;
    private readonly int _maxResendPerHour;
    private readonly int _progressiveDelayStartAttempt;
    private readonly int _minResendIntervalSeconds;
    private readonly int _maxStandardIssuesPerEmailPerHour;
    private readonly int _maxRecoveryIssuesPerEmailPerHour;
    private readonly int _absoluteMaxIssuesPerEmailPerHour;

    public OtpService(IApplicationDbContext db, IDateTimeService clock, IConfiguration configuration)
    {
        _db                      = db;
        _clock                   = clock;
        _codeMinutes             = ReadInt(configuration, "Otp:CodeMinutes", 15);
        _visitRequestCodeMinutes = ReadInt(configuration, "Otp:VisitRequestCodeMinutes", 5);
        _maxAttempts             = ReadInt(configuration, "Otp:MaxAttempts", 10);
        _maxResendPerHour        = ReadInt(configuration, "Otp:MaxResendPerHour", 5);
        _progressiveDelayStartAttempt     = ReadInt(configuration, "Otp:ProgressiveDelayStartAttempt", 6);
        _minResendIntervalSeconds         = ReadInt(configuration, "Otp:MinResendIntervalSeconds", 60);
        _maxStandardIssuesPerEmailPerHour = ReadInt(configuration, "Otp:MaxStandardIssuesPerEmailPerHour", 5);
        _maxRecoveryIssuesPerEmailPerHour = ReadInt(configuration, "Otp:MaxRecoveryIssuesPerEmailPerHour", 1);
        _absoluteMaxIssuesPerEmailPerHour = ReadInt(configuration, "Otp:AbsoluteMaxIssuesPerEmailPerHour", 7);
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback)
        => int.TryParse(configuration[key], out var value) ? value : fallback;

    /// <inheritdoc />
    public int CodeMinutes => _codeMinutes;

    /// <inheritdoc />
    public int VisitRequestCodeMinutes => _visitRequestCodeMinutes;

    // ── Legacy email/purpose flow (password reset, sensitive actions) ─────────────

    public Task<string> CreateAsync(
        User user, string purpose, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => CreateCoreAsync(user.UserId, user.Email, purpose, _codeMinutes, ipAddress, userAgent, cancellationToken);

    public Task<string> CreateForEmailAsync(
        string email, string purpose, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => CreateCoreAsync(null, email, purpose, _visitRequestCodeMinutes, ipAddress, userAgent, cancellationToken);

    private async Task<string> CreateCoreAsync(
        ulong? userId, string email, string purpose, int expiryMinutes,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var now             = _clock.VietnamNow;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var windowStart     = now.AddHours(-1);

        var recentCount = await _db.OtpTokens.AsNoTracking()
            .CountAsync(t => t.Email == normalizedEmail && t.Purpose == purpose && t.CreatedAt >= windowStart, cancellationToken);

        if (recentCount >= _maxResendPerHour)
            throw new BusinessRuleException("Bạn đã yêu cầu quá nhiều lần. Vui lòng thử lại sau.");

        // Old tokens are NOT invalidated here. VerifyAsync always picks the LATEST
        // non-used token (OrderByDescending CreatedAt), so old codes naturally stop
        // working once a newer token exists. Removing the invalidation step avoids
        // a race condition where concurrent resend requests mark each other's tokens
        // as used before the user has a chance to verify.

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var rawCode = SecureTokenGenerator.GenerateNumericCode(6);
            var token   = new OtpToken
            {
                // OtpTokenId is DB-generated (BIGINT AUTO_INCREMENT).
                UserId      = userId,
                Email       = normalizedEmail,
                TokenType   = OtpTokenTypes.OtpCode,
                Purpose     = purpose,
                TokenHash   = HashCode(normalizedEmail, purpose, rawCode),
                ExpiresAt   = now.AddMinutes(expiryMinutes),
                MaxAttempts = _maxAttempts,
                ResendCount = recentCount,
                IpAddress   = Truncate(ipAddress, 45),
                UserAgent   = Truncate(userAgent, 500),
                CreatedAt   = now
            };

            _db.OtpTokens.Add(token);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return rawCode;
            }
            catch (DbUpdateException)
            {
                _db.OtpTokens.Remove(token);
            }
        }

        throw new BusinessRuleException("Không thể tạo mã xác thực. Vui lòng thử lại.");
    }

    public async Task<OtpVerificationResult> VerifyAsync(
        string email, string purpose, string rawCode, CancellationToken cancellationToken = default)
    {
        var now             = _clock.VietnamNow;
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();

        var token = await _db.OtpTokens
            .Where(t => t.Email == normalizedEmail && t.Purpose == purpose && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
            return new OtpVerificationResult(false, "no_active_token", null);

        if (token.ExpiresAt <= now)
            return new OtpVerificationResult(false, "expired", null);

        if (token.AttemptCount >= token.MaxAttempts)
            return new OtpVerificationResult(false, "max_attempts", null);

        token.AttemptCount += 1;

        var matches = !string.IsNullOrEmpty(rawCode)
                      && string.Equals(token.TokenHash, HashCode(normalizedEmail, purpose, rawCode), StringComparison.Ordinal);

        if (matches)
        {
            token.UsedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new OtpVerificationResult(true, null, token);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new OtpVerificationResult(false, "mismatch", null);
    }

    // ── UC-17 challenge-based flow (V2) ────────────────────────────────────────────

    public async Task<OtpChallengeIssue> CreateChallengeAsync(
        string email, string purpose, string submissionId, string issueReason,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var (_, issue) = await CreateChallengeCoreAsync(
            email, purpose, submissionId, issueReason, ipAddress, userAgent, humanVerifiedAt: null, cancellationToken);
        return issue;
    }

    public async Task<OtpChallengeVerification> VerifyChallengeAsync(
        string sessionToken, string email, string purpose, string submissionId, string rawCode,
        CancellationToken cancellationToken = default)
    {
        var now             = _clock.VietnamNow;
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var challengeHash   = SecureTokenGenerator.Hash(sessionToken ?? string.Empty);

        // Row lock: serializes concurrent attempts on the same challenge so the
        // attempt counter can never suffer a lost update. Requires the caller's
        // ambient transaction — see IOtpService.VerifyChallengeAsync docs.
        // ToListAsync (not FirstOrDefaultAsync) so EF executes the raw SQL verbatim —
        // composing would wrap the FOR UPDATE in a derived table.
        var token = (await _db.OtpTokens
            .FromSqlInterpolated($"SELECT * FROM otp_tokens WHERE challenge_token_hash = {challengeHash} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (token is null)
            return new OtpChallengeVerification(false, OtpErrorCodes.NotFound, 0, 0, false, null);

        // Binding check: the token must belong to this email + purpose + submission intent.
        if (!string.Equals(token.Email, normalizedEmail, StringComparison.Ordinal)
            || !string.Equals(token.Purpose, purpose, StringComparison.Ordinal)
            || !string.Equals(token.SubmissionId, submissionId, StringComparison.Ordinal))
        {
            return new OtpChallengeVerification(false, OtpErrorCodes.SessionInvalid, 0, 0, false, null);
        }

        // Compare BEFORE classifying the attempt — a correct code on the final allowed
        // attempt must succeed (never "burned first, compared later").
        var codeMatches = !string.IsNullOrEmpty(rawCode) && string.Equals(
            token.TokenHash,
            HashChallengeCode(normalizedEmail, purpose, rawCode, challengeHash),
            StringComparison.Ordinal);

        var snapshot = new OtpChallengeSnapshot(
            token.ExpiresAt, token.UsedAt, token.InvalidatedAt, token.HumanVerificationRequiredAt,
            token.NextAttemptAllowedAt, token.AttemptCount, token.MaxAttempts);

        var verdict = OtpChallengePolicy.EvaluateVerify(snapshot, codeMatches, now, _progressiveDelayStartAttempt);

        if (verdict.RegisterWrongAttempt)
        {
            token.AttemptCount += 1;
            token.LastAttemptAt = now;
            token.NextAttemptAllowedAt = verdict.NextCooldownSeconds > 0
                ? now.AddSeconds(verdict.NextCooldownSeconds)
                : null;

            if (verdict.BurnChallenge)
            {
                token.HumanVerificationRequiredAt = now;
                token.InvalidatedAt       = now;
                token.InvalidationReason  = OtpInvalidationReasons.MaxAttempts;
            }

            // Saved but NOT committed — the caller commits so the wrong-attempt state
            // persists even though the HTTP request itself returns an error.
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (verdict.Outcome == OtpVerifyOutcome.HumanVerificationRequired)
        {
            var windowStart = now.AddHours(-1);
            var issuesInWindow = await _db.OtpTokens.AsNoTracking()
                .Where(t => t.Email == normalizedEmail && t.Purpose == purpose && t.CreatedAt >= windowStart)
                .Select(t => new { t.IssueReason, t.CreatedAt })
                .ToListAsync(cancellationToken);

            var tuples = issuesInWindow.Select(i => (i.IssueReason == OtpIssueReasons.HumanRecovery, i.CreatedAt)).ToList();

            var decision = OtpChallengePolicy.EvaluateIssue(
                isHumanRecovery: true, tuples, now,
                _minResendIntervalSeconds,
                _maxStandardIssuesPerEmailPerHour,
                _maxRecoveryIssuesPerEmailPerHour,
                _absoluteMaxIssuesPerEmailPerHour);

            if (!decision.Allowed)
            {
                return new OtpChallengeVerification(
                    false,
                    decision.ErrorCode,
                    0,
                    decision.RetryAfterSeconds,
                    true,
                    null,
                    decision.RetryAt);
            }
        }

        return new OtpChallengeVerification(
            verdict.Outcome == OtpVerifyOutcome.Success,
            verdict.Outcome switch
            {
                OtpVerifyOutcome.Success                   => null,
                OtpVerifyOutcome.Invalid                   => OtpErrorCodes.Invalid,
                OtpVerifyOutcome.Expired                   => OtpErrorCodes.Expired,
                OtpVerifyOutcome.SessionInvalid            => OtpErrorCodes.SessionInvalid,
                OtpVerifyOutcome.RetryLater                => OtpErrorCodes.RetryLater,
                OtpVerifyOutcome.HumanVerificationRequired => OtpErrorCodes.HumanVerificationRequired,
                _                                          => OtpErrorCodes.SessionInvalid
            },
            verdict.RemainingAttempts,
            verdict.RetryAfterSeconds,
            verdict.HumanVerificationRequired,
            verdict.Outcome == OtpVerifyOutcome.Success ? token : null);
    }

    public Task<OtpChallengeIssue> ResendChallengeAsync(
        string oldSessionToken, string purpose, string submissionId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => ReissueChallengeAsync(oldSessionToken, purpose, submissionId,
            ipAddress, userAgent, isHumanRecovery: false, cancellationToken);

    public Task<OtpChallengeIssue> RecoverChallengeAsync(
        string oldSessionToken, string purpose, string submissionId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => ReissueChallengeAsync(oldSessionToken, purpose, submissionId,
            ipAddress, userAgent, isHumanRecovery: true, cancellationToken);

    /// <summary>
    /// Shared resend/recover path: locks the old challenge, validates its binding,
    /// invalidates it and issues a fresh one atomically. Normal resend is refused once the
    /// old challenge requires human verification — resend must never bypass the CAPTCHA.
    /// </summary>
    private async Task<OtpChallengeIssue> ReissueChallengeAsync(
        string oldSessionToken, string purpose, string submissionId,
        string? ipAddress, string? userAgent, bool isHumanRecovery,
        CancellationToken cancellationToken)
    {
        var now           = _clock.VietnamNow;
        var challengeHash = SecureTokenGenerator.Hash(oldSessionToken ?? string.Empty);

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        try
        {
            var oldToken = (await _db.OtpTokens
                .FromSqlInterpolated($"SELECT * FROM otp_tokens WHERE challenge_token_hash = {challengeHash} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

            if (oldToken is null
                || !string.Equals(oldToken.Purpose, purpose, StringComparison.Ordinal)
                || !string.Equals(oldToken.SubmissionId, submissionId, StringComparison.Ordinal))
            {
                throw new OtpChallengeException(
                    StatusCodes.Status400BadRequest,
                    OtpErrorCodes.SessionInvalid,
                    "Phiên xác thực không hợp lệ. Vui lòng gửi lại yêu cầu.");
            }

            if (!isHumanRecovery && oldToken.HumanVerificationRequiredAt is not null)
            {
                throw new OtpChallengeException(
                    StatusCodes.Status428PreconditionRequired,
                    OtpErrorCodes.HumanVerificationRequired,
                    "Bạn đã nhập sai quá nhiều lần. Vui lòng xác minh bạn không phải robot để nhận mã mới.",
                    humanVerificationRequired: true);
            }

            // The old challenge is dead from here on — a new code never re-opens an old one.
            oldToken.InvalidatedAt      ??= now;
            oldToken.InvalidationReason ??= isHumanRecovery
                ? OtpInvalidationReasons.HumanRecovery
                : OtpInvalidationReasons.SupersededByResend;
            if (isHumanRecovery)
                oldToken.HumanVerifiedAt = now;

            var (_, issue) = await CreateChallengeCoreAsync(
                oldToken.Email, purpose, submissionId,
                isHumanRecovery ? OtpIssueReasons.HumanRecovery : OtpIssueReasons.Resend,
                ipAddress, userAgent,
                humanVerifiedAt: isHumanRecovery ? now : null,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return issue;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<(OtpToken Token, OtpChallengeIssue Issue)> CreateChallengeCoreAsync(
        string email, string purpose, string submissionId, string issueReason,
        string? ipAddress, string? userAgent, DateTime? humanVerifiedAt,
        CancellationToken cancellationToken)
    {
        var now             = _clock.VietnamNow;
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var windowStart     = now.AddHours(-1);
        var isRecovery      = issueReason == OtpIssueReasons.HumanRecovery;

        var issuesInWindow = await _db.OtpTokens.AsNoTracking()
            .Where(t => t.Email == normalizedEmail && t.Purpose == purpose && t.CreatedAt >= windowStart)
            .Select(t => new { t.IssueReason, t.CreatedAt })
            .ToListAsync(cancellationToken);

        var recoveryCount = issuesInWindow.Count(t => t.IssueReason == OtpIssueReasons.HumanRecovery);
        var standardCount = issuesInWindow.Count - recoveryCount;
        
        var tuples = issuesInWindow.Select(i => (i.IssueReason == OtpIssueReasons.HumanRecovery, i.CreatedAt)).ToList();

        var decision = OtpChallengePolicy.EvaluateIssue(
            isRecovery, tuples, now,
            _minResendIntervalSeconds,
            _maxStandardIssuesPerEmailPerHour,
            _maxRecoveryIssuesPerEmailPerHour,
            _absoluteMaxIssuesPerEmailPerHour);

        if (!decision.Allowed)
        {
            throw new OtpChallengeException(
                StatusCodes.Status429TooManyRequests,
                decision.ErrorCode ?? OtpErrorCodes.ResendRateLimited,
                "Temporarily unable to issue another verification code.",
                retryAfterSeconds: decision.RetryAfterSeconds,
                retryAt: decision.RetryAt);
        }

        var expiresAt = now.AddMinutes(_visitRequestCodeMinutes);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var sessionToken  = SecureTokenGenerator.GenerateOpaqueToken(32);
            var challengeHash = SecureTokenGenerator.Hash(sessionToken);
            var rawCode       = SecureTokenGenerator.GenerateNumericCode(6);

            var token = new OtpToken
            {
                UserId              = null,
                Email               = normalizedEmail,
                TokenType           = OtpTokenTypes.OtpCode,
                Purpose             = purpose,
                // Salted with the challenge hash so re-issued codes can never collide with
                // uq_otp_tokens_hash across challenges of the same email+purpose.
                TokenHash           = HashChallengeCode(normalizedEmail, purpose, rawCode, challengeHash),
                ChallengeTokenHash  = challengeHash,
                SubmissionId        = submissionId,
                IssueReason         = issueReason,
                ExpiresAt           = expiresAt,
                MaxAttempts         = _maxAttempts,
                ResendCount         = standardCount,
                HumanVerifiedAt     = humanVerifiedAt,
                IpAddress           = Truncate(ipAddress, 45),
                UserAgent           = Truncate(userAgent, 500),
                CreatedAt           = now
            };

            _db.OtpTokens.Add(token);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return (token, new OtpChallengeIssue(
                    sessionToken, rawCode, normalizedEmail, expiresAt, _minResendIntervalSeconds, _maxAttempts));
            }
            catch (DbUpdateException)
            {
                _db.OtpTokens.Remove(token);
            }
        }

        throw new BusinessRuleException("Không thể tạo mã xác thực. Vui lòng thử lại.");
    }

    private static string HashCode(string email, string purpose, string code)
        => SecureTokenGenerator.Hash($"{email}:{purpose}:{code}");

    private static string HashChallengeCode(string email, string purpose, string code, string challengeHash)
        => SecureTokenGenerator.Hash($"{email}:{purpose}:{code}:{challengeHash}");

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
