using PEMS.Domain.Entities.Users;

namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of verifying a one-time code.</summary>
public sealed record OtpVerificationResult(bool Success, string? FailureReason, OtpToken? Token);

/// <summary>
/// Issues and verifies one-time codes (OTP). Codes are stored hashed (SHA-256);
/// the raw code is returned once for delivery.
/// </summary>
public interface IOtpService
{
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
}
