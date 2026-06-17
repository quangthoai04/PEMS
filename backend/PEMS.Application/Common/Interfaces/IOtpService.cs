using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of verifying a one-time code.</summary>
public sealed record OtpVerificationResult(bool Success, string? FailureReason, OtpToken? Token);

/// <summary>
/// Issues and verifies one-time codes (OTP) used for password reset, etc. Codes
/// are stored hashed (SHA-256); the raw code is returned once for delivery.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Creates a new OTP for the given user/purpose, enforcing a resend limit,
    /// and returns the raw code. Persists immediately.
    /// </summary>
    Task<string> CreateAsync(
        User user,
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
}
