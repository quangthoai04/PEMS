using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of verifying a one-time code.</summary>
public sealed record OtpVerificationResult(bool Success, string? FailureReason, OtpToken? Token);

/// <summary>
/// A freshly issued UC-17 OTP challenge. <see cref="SessionToken"/> is the raw opaque
/// token (CSPRNG) — returned to the client exactly once; only its SHA-256 is stored.
/// <see cref="Code"/> is the raw 6-digit code for email delivery only. Neither may be
/// logged or persisted raw.
/// </summary>
public sealed record OtpChallengeIssue(
    string SessionToken,
    string Code,
    string Email,
    DateTime ExpiresAt,
    int ResendAfterSeconds,
    int MaxAttempts);

/// <summary>
/// Outcome of verifying a UC-17 OTP challenge. <see cref="ErrorCode"/> is one of
/// <c>OtpErrorCodes</c>; metadata mirrors what the client renders. <see cref="Token"/> is
/// the TRACKED challenge row (locked FOR UPDATE) so the caller can mark it used inside
/// the same transaction.
/// </summary>
public sealed record OtpChallengeVerification(
    bool Success,
    string? ErrorCode,
    int RemainingAttempts,
    int RetryAfterSeconds,
    bool HumanVerificationRequired,
    OtpToken? Token,
    DateTime? RetryAt = null);

/// <summary>
/// Issues and verifies one-time codes (OTP). Codes are stored hashed (SHA-256);
/// the raw code is returned once for delivery.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// How long a code issued by <see cref="CreateAsync"/> stays valid, in minutes
    /// (<c>Otp:CodeMinutes</c>). Exposed because the email that carries the code has to state the same
    /// number the token was actually created with — a hard-coded "15 phút" in a template silently
    /// becomes a lie the moment the setting changes.
    /// </summary>
    int CodeMinutes { get; }

    /// <summary>
    /// How long a visit-request challenge stays valid, in minutes (<c>Otp:VisitRequestCodeMinutes</c>).
    /// Deliberately shorter than <see cref="CodeMinutes"/>, and stated in the email for the same reason.
    /// </summary>
    int VisitRequestCodeMinutes { get; }

    /// <summary>
    /// Creates an OTP tied to an existing <see cref="User"/> (e.g. password reset).
    /// Enforces hourly resend limit and invalidates previous active codes.
    /// </summary>
    Task<string> CreateAsync(
        User user,
        string purpose,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an OTP tied only to an email address — no User entity required.
    /// Used for unauthenticated flows such as visit-request submission.
    /// The OTP expires after the visit-request-specific code duration (5 min).
    /// </summary>
    Task<string> CreateForEmailAsync(
        string email,
        string purpose,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a raw code for an email + purpose. Increments attempt counters and,
    /// on success, marks the token used. Never reveals which check failed to callers.
    /// </summary>
    Task<OtpVerificationResult> VerifyAsync(
        string email,
        string purpose,
        string rawCode,
        CancellationToken cancellationToken = default);

    // ── UC-17 challenge-based flow (V2) ────────────────────────────────────────────

    /// <summary>
    /// Issues a new OTP challenge bound to email + purpose + submissionId. Enforces the
    /// per-email hourly issue quotas and the min resend interval (throws a typed
    /// <c>OtpChallengeException</c> 429 when exceeded). Persists immediately (own
    /// SaveChanges); safe to call outside a transaction.
    /// </summary>
    Task<OtpChallengeIssue> CreateChallengeAsync(
        string email,
        string purpose,
        string submissionId,
        string issueReason,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a raw code against the challenge identified by the opaque session token.
    /// MUST be called inside an ambient transaction owned by the caller: the challenge row
    /// is locked (<c>SELECT … FOR UPDATE</c>) and attempt-state mutations are saved but NOT
    /// committed — the caller commits on BOTH the wrong-code path (so attempts persist even
    /// though the request fails) and the success path (atomically with the created request).
    /// </summary>
    Task<OtpChallengeVerification> VerifyChallengeAsync(
        string sessionToken,
        string email,
        string purpose,
        string submissionId,
        string rawCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Normal resend: supersedes the old challenge (looked up by its raw session token +
    /// submission binding) and issues a fresh one with <c>issue_reason = RESEND</c>.
    /// A challenge that already requires human verification can NOT be resent — resend must
    /// never bypass the CAPTCHA gate (throws typed 428). Issue quotas apply.
    /// </summary>
    Task<OtpChallengeIssue> ResendChallengeAsync(
        string oldSessionToken,
        string purpose,
        string submissionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// After successful human verification: invalidates the old challenge (looked up by its
    /// raw session token + submission binding) and issues a fresh challenge with
    /// <c>issue_reason = HUMAN_RECOVERY</c> and attempt count 0. The old code can never be
    /// used again. Throws typed <c>OtpChallengeException</c> on unknown/mismatched session
    /// or when the recovery issue quota is exhausted.
    /// </summary>
    Task<OtpChallengeIssue> RecoverChallengeAsync(
        string oldSessionToken,
        string purpose,
        string submissionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
