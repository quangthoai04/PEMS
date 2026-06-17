namespace PEMS.Application.Common.Interfaces;

/// <summary>Verified identity extracted from a Google ID token.</summary>
public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string? Name,
    string? Picture);

/// <summary>
/// Validates Google Sign-In ID tokens (signature, issuer, audience, expiry) using
/// Google's published signing keys. Returns null when the token is invalid.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
