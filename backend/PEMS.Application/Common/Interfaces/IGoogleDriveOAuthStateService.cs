namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// What an ADMIN's consent round-trip carries through Google and back: who started it, and when it stops
/// being acceptable. Issued by the ADMIN-only start endpoint, verified by the anonymous callback.
///
/// <para>
/// The callback cannot be authenticated — Google redirects a browser, with no Authorization header — so
/// this is the only thing standing between "an admin pressed reconnect" and "anyone who can reach
/// <c>/api/google-drive/oauth/callback</c> with a code of their own". <see cref="AdminUserId"/> is
/// therefore read from here and never from the query string.
/// </para>
/// </summary>
/// <param name="AdminUserId">The ADMIN who started the flow; recorded as <c>updated_by</c> and in the audit row.</param>
/// <param name="Nonce">Random per-issue value, so two reconnects never produce the same state string.</param>
/// <param name="ExpiresAtUtc">Hard deadline. Google's authorization codes are single-use, so this bounds the window, not the replay.</param>
public sealed record GoogleDriveOAuthState(ulong AdminUserId, string Nonce, DateTime ExpiresAtUtc);

/// <summary>Why a state string was refused, or the payload it carried when it was not.</summary>
/// <param name="IsValid">True only when <paramref name="State"/> is present and unexpired.</param>
/// <param name="State">The decoded payload; null whenever <paramref name="IsValid"/> is false.</param>
/// <param name="FailureReason">
/// One of <see cref="PEMS.Application.Common.Storage.GoogleDriveOAuthRedirectReasons"/> — a slug safe to
/// put in a redirect URL. Never carries crypto detail: "this did not authenticate" is all a caller may learn.
/// </param>
public sealed record GoogleDriveOAuthStateValidation(bool IsValid, GoogleDriveOAuthState? State, string? FailureReason);

/// <summary>
/// Mints and verifies the OAuth <c>state</c> parameter for the Google Drive reconnect flow.
///
/// <para>
/// Implemented over the same <see cref="ISecretProtector"/> that protects credential columns rather than a
/// second key/store: AES-GCM's authentication tag already makes the payload tamper-evident, which is the
/// whole requirement, and a distributed nonce cache would be a new operational dependency bought for
/// nothing — Google's authorization codes are single-use, so a replayed state cannot be exchanged twice.
/// </para>
/// </summary>
public interface IGoogleDriveOAuthStateService
{
    /// <summary>Issues a URL-safe state string bound to <paramref name="adminUserId"/> and to a short deadline.</summary>
    string Create(ulong adminUserId);

    /// <summary>
    /// Decodes and verifies a state string that came back from Google. Never throws on bad input — a
    /// forged, truncated or stale value is an expected outcome of a public endpoint, not an exception.
    /// </summary>
    GoogleDriveOAuthStateValidation Validate(string? state);
}
