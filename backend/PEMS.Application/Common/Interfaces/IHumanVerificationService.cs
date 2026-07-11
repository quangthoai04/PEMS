namespace PEMS.Application.Common.Interfaces;

/// <summary>Outcome of a server-side human verification (CAPTCHA) check.</summary>
public sealed record HumanVerificationResult(bool Success, string? FailureReason);

/// <summary>
/// Server-side validation of a human-verification token (Cloudflare Turnstile in
/// Infrastructure). The raw token is verified against the provider and never persisted
/// or logged. Production fails CLOSED: verification is rejected when the provider is
/// enabled but mis-configured, and the development bypass can never activate there.
/// </summary>
public interface IHumanVerificationService
{
    Task<HumanVerificationResult> VerifyAsync(
        string token,
        string? ipAddress,
        CancellationToken cancellationToken);
}
